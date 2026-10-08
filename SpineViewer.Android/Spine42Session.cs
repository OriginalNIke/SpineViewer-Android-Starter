using SpineRuntime42;
namespace SpineViewer.Android;
// Spine 4.2 playback with textured geometry for the existing Vulkan/OpenGL backends.
public interface ISpinePlayback {
 bool IsLoaded { get; }
 IReadOnlyList<string> Skins { get; }
 IReadOnlyList<string> Animations { get; }
 void SetSkin(string name);
 void SetAnimation(string name);
 void Step(float dt);
 IReadOnlyList<(float x,float y,float px,float py)> BoneLines();
}
public sealed class Spine42Session : ISpinePlayback {
    sealed class DeferredTextureLoader : TextureLoader {
        public void Load(AtlasPage page, string path) { /* Atlas UVs work without GPU texture upload. */ }
        public void Unload(object texture) { }
    }
    Atlas? atlas;
    Skeleton? skeleton;
    AnimationState? state;
    public bool IsLoaded => skeleton != null;
    public IReadOnlyList<SpineTriangle> TexturedTriangles() => Spine42Geometry.Extract(skeleton);
    public IReadOnlyList<string> Skins { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<string> Animations { get; private set; } = Array.Empty<string>();
    public void Load(string atlasText, byte[] skeletonBytes, bool binary) {
        using var reader = new StringReader(atlasText);
        atlas = new Atlas(reader, "", new DeferredTextureLoader());
        SkeletonData data;
        if (binary) { using var input = new MemoryStream(skeletonBytes); data = new SkeletonBinary(atlas).ReadSkeletonData(input); }
        else { using var input = new StreamReader(new MemoryStream(skeletonBytes)); data = new SkeletonJson(atlas).ReadSkeletonData(input); }
        skeleton = new Skeleton(data);
        state = new AnimationState(new AnimationStateData(data));
        Skins = data.Skins.Select(x => x.Name).ToArray();
        Animations = data.Animations.Select(x => x.Name).ToArray();
        skeleton.UpdateWorldTransform(Skeleton.Physics.Update);
    }
    public void SetSkin(string name) { if(skeleton == null) return; skeleton.SetSkin(name); skeleton.SetSlotsToSetupPose(); skeleton.UpdateWorldTransform(Skeleton.Physics.Update); }
    public void SetAnimation(string name) { state?.SetAnimation(0,name,true); }
    public void Step(float dt) { if (skeleton == null || state == null) return; state.Update(Math.Clamp(dt,0,0.1f)); state.Apply(skeleton); skeleton.UpdateWorldTransform(Skeleton.Physics.Update); }
    public IReadOnlyList<(float x,float y,float px,float py)> BoneLines() {
        if(skeleton == null) return Array.Empty<(float,float,float,float)>();
        var lines = new List<(float,float,float,float)>();
        foreach(var bone in skeleton.Bones) if(bone.Parent != null)
            lines.Add((bone.WorldX,bone.WorldY,bone.Parent.WorldX,bone.Parent.WorldY));
        return lines;
    }
}
public sealed class SkeletonDebugDrawable : IDrawable {
    public Func<IReadOnlyList<(float x,float y,float px,float py)>> GetLines {get;set;} = () => Array.Empty<(float,float,float,float)>();
    public void Draw(ICanvas canvas, RectF dirtyRect) {
        canvas.FillColor = Color.FromArgb("#111827"); canvas.FillRectangle(dirtyRect);
        var lines=GetLines(); if(lines.Count==0) return;
        float minX=lines.Min(x=>Math.Min(x.x,x.px)), maxX=lines.Max(x=>Math.Max(x.x,x.px));
        float minY=lines.Min(x=>Math.Min(x.y,x.py)), maxY=lines.Max(x=>Math.Max(x.y,x.py));
        float scale=Math.Min((dirtyRect.Width-32)/Math.Max(1,maxX-minX),(dirtyRect.Height-32)/Math.Max(1,maxY-minY));
        scale=Math.Clamp(scale,0.02f,5f);
        float cx=(minX+maxX)/2, cy=(minY+maxY)/2;
        canvas.StrokeColor=Colors.Cyan;canvas.StrokeSize=2;
        foreach(var line in lines) canvas.DrawLine(dirtyRect.Center.X+(line.px-cx)*scale,dirtyRect.Center.Y-(line.py-cy)*scale,dirtyRect.Center.X+(line.x-cx)*scale,dirtyRect.Center.Y-(line.y-cy)*scale);
    }
}

// Converts Spine 4.2 draw order to the existing GPU triangle representation.
// Per-slot clipping and atlas pages are preserved, as are the four blend modes.
internal sealed class Spine42Geometry
{
    static readonly List<SpineTriangle> pool = new();
    static readonly List<SpineTriangle> active = new();
    static float[] world = Array.Empty<float>();
    static readonly SkeletonClipping clipper = new();
    static readonly int[] quadIndices = { 0, 1, 2, 2, 3, 0 };

    public static IReadOnlyList<SpineTriangle> Extract(Skeleton? skeleton)
    {
        active.Clear();
        if (skeleton == null) return active;
        clipper.ClipEnd();
        foreach (var slot in skeleton.DrawOrder)
        {
            if (slot.Attachment is ClippingAttachment clipping)
            {
                clipper.ClipStart(slot, clipping);
                continue;
            }
            float[] uv;
            int[] indices;
            AtlasRegion? region;
            int length;
            if (slot.Attachment is RegionAttachment quad)
            {
                length = 8;
                if (world.Length < length) Array.Resize(ref world, length);
                quad.ComputeWorldVertices(slot, world, 0, 2);
                uv = quad.UVs;
                indices = quadIndices;
                region = quad.Region as AtlasRegion;
            }
            else if (slot.Attachment is MeshAttachment mesh)
            {
                length = mesh.WorldVerticesLength;
                if (world.Length < length) Array.Resize(ref world, length);
                mesh.ComputeWorldVertices(slot, 0, length, world, 0, 2);
                uv = mesh.UVs;
                indices = mesh.Triangles;
                region = mesh.Region as AtlasRegion;
            }
            else
            {
                clipper.ClipEnd(slot);
                continue;
            }
            if (region == null || uv == null || indices == null)
            {
                clipper.ClipEnd(slot);
                continue;
            }
            if (clipper.IsClipping)
            {
                clipper.ClipTriangles(world, indices, indices.Length, uv);
                Emit(clipper.ClippedVertices.Items, clipper.ClippedVertices.Count,
                    clipper.ClippedUVs.Items, clipper.ClippedTriangles.Items,
                    clipper.ClippedTriangles.Count, region.page.name, slot.Data.BlendMode);
            }
            else Emit(world, length, uv, indices, indices.Length, region.page.name, slot.Data.BlendMode);
            clipper.ClipEnd(slot);
        }
        clipper.ClipEnd();
        return active;
    }

    static void Emit(float[] xy, int length, float[] uv, int[] indices, int count, string page, SpineRuntime42.BlendMode blend)
    {
        for (int i = 0; i + 2 < count; i += 3)
        {
            int k0 = indices[i] * 2, k1 = indices[i + 1] * 2, k2 = indices[i + 2] * 2;
            if (k0 < 0 || k1 < 0 || k2 < 0 || k0 + 1 >= length || k1 + 1 >= length || k2 + 1 >= length ||
                k0 + 1 >= uv.Length || k1 + 1 >= uv.Length || k2 + 1 >= uv.Length) continue;
            int index = active.Count;
            if (index == pool.Count) pool.Add(new SpineTriangle());
            var triangle = pool[index];
            triangle.Page = page;
            triangle.Blend = (SpineRuntime41.BlendMode)(int)blend;
            for (int j = 0; j < 3; j++)
            {
                int k = j == 0 ? k0 : j == 1 ? k1 : k2;
                triangle.XY[j * 2] = xy[k];
                triangle.XY[j * 2 + 1] = xy[k + 1];
                triangle.UV[j * 2] = uv[k];
                triangle.UV[j * 2 + 1] = uv[k + 1];
            }
            active.Add(triangle);
        }
    }
}
