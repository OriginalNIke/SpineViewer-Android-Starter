using SpineRuntime40;
using BlendMode = SpineRuntime40.BlendMode;
namespace SpineViewer.Android;
public static class Spine40Geometry
{
    // Reused geometry storage: grows only when the animation needs more triangles.
    // This cache belongs to the single UI-thread playback session.
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
            if (slot.Attachment is ClippingAttachment clipping) {
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
            else { clipper.ClipEnd(slot); continue; }
            if (region == null || uv == null || indices == null) { clipper.ClipEnd(slot); continue; }
            float[] xy = world;
            if (clipper.IsClipping) {
                clipper.ClipTriangles(world, length, indices, indices.Length, uv);
                xy = clipper.ClippedVertices.Items;
                uv = clipper.ClippedUVs.Items;
                indices = clipper.ClippedTriangles.Items;
                length = clipper.ClippedVertices.Count;
                // ExposedList backing arrays can exceed their active count.
                int triangleCount = clipper.ClippedTriangles.Count;
                Emit(xy, length, uv, indices, triangleCount, region.page.name, slot.Data.BlendMode);
            } else {
                Emit(xy, length, uv, indices, indices.Length, region.page.name, slot.Data.BlendMode);
            }
            clipper.ClipEnd(slot);
        }
        clipper.ClipEnd();
        return active;
    }
    static void Emit(float[] xy, int length, float[] uv, int[] indices, int count, string page, BlendMode blend)
    {
            for (int i = 0; i + 2 < count; i += 3)
            {
                int k0 = indices[i] * 2, k1 = indices[i+1] * 2, k2 = indices[i+2] * 2;
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
                    triangle.XY[j*2] = xy[k]; triangle.XY[j*2+1] = xy[k+1];
                    triangle.UV[j*2] = uv[k]; triangle.UV[j*2+1] = uv[k+1];
                }
                active.Add(triangle);
            }
    }
}
