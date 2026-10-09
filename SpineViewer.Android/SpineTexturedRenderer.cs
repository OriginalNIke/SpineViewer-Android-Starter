using BlendMode = SpineRuntime41.BlendMode;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using SpineRuntime41;

namespace SpineViewer.Android;

// Spine 4.1: per-slot geometry in draw order, including weighted meshes.
public sealed class SpineTriangle
{
    public string Page = "";
    public BlendMode Blend = BlendMode.Normal;
    public readonly float[] XY = new float[6];
    public readonly float[] UV = new float[6];
    public readonly float[] Tint = { 1f, 1f, 1f, 1f };
}

public sealed class SpineTexturedRenderer : SKCanvasView
{
    public Func<IReadOnlyList<SpineTriangle>> GetTriangles { get; set; } = () => Array.Empty<SpineTriangle>();
    readonly Dictionary<string, SKBitmap> bitmaps = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, SKShader> shaders = new(StringComparer.OrdinalIgnoreCase);
    readonly SKPaint paint = new() { IsAntialias = false };
    SKPoint[] positions = new SKPoint[1024];
    SKPoint[] texCoords = new SKPoint[1024];
    void EnsureCapacity(int required)
    {
        if (positions.Length >= required) return;
        int capacity = positions.Length;
        while (capacity < required) capacity *= 2;
        Array.Resize(ref positions, capacity);
        Array.Resize(ref texCoords, capacity);
    }
    public SpineTexturedRenderer() { HeightRequest = 420; IgnorePixelScaling = true; }
    public void SetTexture(string name, byte[] bytes)
    {
        var bitmap = SKBitmap.Decode(bytes) ?? throw new InvalidDataException($"PNG inválido: {name}");
        if (shaders.Remove(name, out var oldShader)) oldShader.Dispose();
        if (bitmaps.Remove(name, out var old)) old.Dispose();
        bitmaps[name] = bitmap;
        shaders[name] = SKShader.CreateBitmap(bitmap, SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);
        InvalidateSurface();
    }
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler != null) return;
        foreach (var shader in shaders.Values) shader.Dispose();
        shaders.Clear();
        foreach (var bitmap in bitmaps.Values) bitmap.Dispose();
        bitmaps.Clear();
    }
    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(new SKColor(17,24,39));
        var triangles = GetTriangles();
        if (triangles.Count == 0) return;
        float minX=float.MaxValue,minY=float.MaxValue,maxX=float.MinValue,maxY=float.MinValue;
        foreach(var tri in triangles) for(int i=0;i<tri.XY.Length;i+=2) {
            minX=Math.Min(minX,tri.XY[i]);maxX=Math.Max(maxX,tri.XY[i]);
            minY=Math.Min(minY,tri.XY[i+1]);maxY=Math.Max(maxY,tri.XY[i+1]);
        }
        float scale=Math.Min((e.Info.Width-32f)/Math.Max(1,maxX-minX),(e.Info.Height-32f)/Math.Max(1,maxY-minY));
        scale=Math.Clamp(scale,0.01f,8f);
        float cx=(minX+maxX)/2,cy=(minY+maxY)/2;
        // Batch consecutive triangles with the same atlas page, preserving Spine draw order.
        // SkiaSharp's CreateCopy still allocates a native vertex object once per batch,
        // not once per triangle. Managed coordinate buffers are reused across frames.
        for (int start = 0; start < triangles.Count;)
        {
            string page = triangles[start].Page;
            int end = start + 1;
            while (end < triangles.Count && string.Equals(page, triangles[end].Page, StringComparison.OrdinalIgnoreCase)) end++;
            if (bitmaps.TryGetValue(page, out var bmp) && shaders.TryGetValue(page, out var shader))
            {
                int count = (end - start) * 3;
                EnsureCapacity(count);
                int n = 0;
                for (int t = start; t < end; t++)
                {
                    var tri = triangles[t];
                    for (int j = 0; j < 3; j++)
                    {
                        positions[n] = new SKPoint(e.Info.Width / 2f + (tri.XY[2*j] - cx) * scale,
                                                   e.Info.Height / 2f - (tri.XY[2*j+1] - cy) * scale);
                        texCoords[n] = new SKPoint(tri.UV[2*j] * bmp.Width, tri.UV[2*j+1] * bmp.Height);
                        n++;
                    }
                }
                // CreateCopy requires arrays sized to the vertex count. The reusable
                // buffers avoid per-triangle allocations; batch arrays remain necessary.
                var batchXY = new SKPoint[count];
                var batchUV = new SKPoint[count];
                Array.Copy(positions, batchXY, count);
                Array.Copy(texCoords, batchUV, count);
                using var vertices = SKVertices.CreateCopy(SKVertexMode.Triangles, batchXY, batchUV, null);
                paint.Shader = shader;
                canvas.DrawVertices(vertices, SKBlendMode.Modulate, paint);
            }
            start = end;
        }
    }
}

public static class Spine41Geometry
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
            float ar, ag, ab, aa;
            if (slot.Attachment is RegionAttachment quad)
            {
                length = 8;
                if (world.Length < length) Array.Resize(ref world, length);
                quad.ComputeWorldVertices(slot, world, 0, 2);
                uv = quad.UVs;
                indices = quadIndices;
                region = quad.Region as AtlasRegion;
                ar = quad.R; ag = quad.G; ab = quad.B; aa = quad.A;
            }
            else if (slot.Attachment is MeshAttachment mesh)
            {
                length = mesh.WorldVerticesLength;
                if (world.Length < length) Array.Resize(ref world, length);
                mesh.ComputeWorldVertices(slot, 0, length, world, 0, 2);
                uv = mesh.UVs;
                indices = mesh.Triangles;
                region = mesh.Region as AtlasRegion;
                ar = mesh.R; ag = mesh.G; ab = mesh.B; aa = mesh.A;
            }
            else { clipper.ClipEnd(slot); continue; }
            if (region == null || uv == null || indices == null) { clipper.ClipEnd(slot); continue; }
            float cr = Math.Clamp(skeleton.R * slot.R * ar, 0f, 1f);
            float cg = Math.Clamp(skeleton.G * slot.G * ag, 0f, 1f);
            float cb = Math.Clamp(skeleton.B * slot.B * ab, 0f, 1f);
            float ca = Math.Clamp(skeleton.A * slot.A * aa, 0f, 1f);
            if (ca <= 0f) { clipper.ClipEnd(slot); continue; }
            float[] xy = world;
            if (clipper.IsClipping) {
                clipper.ClipTriangles(world, length, indices, indices.Length, uv);
                xy = clipper.ClippedVertices.Items;
                uv = clipper.ClippedUVs.Items;
                indices = clipper.ClippedTriangles.Items;
                length = clipper.ClippedVertices.Count;
                // ExposedList backing arrays can exceed their active count.
                int triangleCount = clipper.ClippedTriangles.Count;
                Emit(xy, length, uv, indices, triangleCount, region.page.name, slot.Data.BlendMode, cr, cg, cb, ca);
            } else {
                Emit(xy, length, uv, indices, indices.Length, region.page.name, slot.Data.BlendMode, cr, cg, cb, ca);
            }
            clipper.ClipEnd(slot);
        }
        clipper.ClipEnd();
        return active;
    }
    static void Emit(float[] xy, int length, float[] uv, int[] indices, int count, string page, BlendMode blend, float cr, float cg, float cb, float ca)
    {
            for (int i = 0; i + 2 < count; i += 3)
            {
                int k0 = indices[i] * 2, k1 = indices[i+1] * 2, k2 = indices[i+2] * 2;
                if (k0 < 0 || k1 < 0 || k2 < 0 || k0 + 1 >= length || k1 + 1 >= length || k2 + 1 >= length ||
                    k0 + 1 >= uv.Length || k1 + 1 >= uv.Length || k2 + 1 >= uv.Length) continue;
                // Corrupted or degenerate mesh triangles can produce huge GPU artifacts.
                if (!float.IsFinite(xy[k0]) || !float.IsFinite(xy[k0+1]) ||
                    !float.IsFinite(xy[k1]) || !float.IsFinite(xy[k1+1]) ||
                    !float.IsFinite(xy[k2]) || !float.IsFinite(xy[k2+1]) ||
                    !float.IsFinite(uv[k0]) || !float.IsFinite(uv[k0+1]) ||
                    !float.IsFinite(uv[k1]) || !float.IsFinite(uv[k1+1]) ||
                    !float.IsFinite(uv[k2]) || !float.IsFinite(uv[k2+1])) continue;
                float cross = (xy[k1]-xy[k0]) * (xy[k2+1]-xy[k0+1]) -
                              (xy[k1+1]-xy[k0+1]) * (xy[k2]-xy[k0]);
                if (!float.IsFinite(cross) || Math.Abs(cross) < 0.000001f) continue;
                int index = active.Count;
                if (index == pool.Count) pool.Add(new SpineTriangle());
                var triangle = pool[index];
                triangle.Page = page;
                triangle.Blend = blend;
                triangle.Tint[0] = cr; triangle.Tint[1] = cg; triangle.Tint[2] = cb; triangle.Tint[3] = ca;
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
