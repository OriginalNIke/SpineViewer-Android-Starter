using SpineRuntime42;
namespace SpineViewer.Android;
// Runtime 4.2 real: computes animated bone transforms. Textured meshes are a separate milestone.
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
