using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using SpineRuntime41;

namespace SpineViewer.Android;

// Spine 4.1: per-slot geometry in draw order, including weighted meshes.
public sealed record SpineTriangle(string Page, float[] XY, float[] UV);

public sealed class SpineTexturedRenderer : SKCanvasView
{
    public Func<IReadOnlyList<SpineTriangle>> GetTriangles { get; set; } = () => Array.Empty<SpineTriangle>();
    readonly Dictionary<string, SKBitmap> bitmaps = new(StringComparer.OrdinalIgnoreCase);
    public SpineTexturedRenderer() { HeightRequest = 420; IgnorePixelScaling = true; }
    public void SetTexture(string name, byte[] bytes)
    {
        var bitmap = SKBitmap.Decode(bytes) ?? throw new InvalidDataException($"PNG inválido: {name}");
        if (bitmaps.Remove(name, out var old)) old.Dispose();
        bitmaps[name] = bitmap;
        InvalidateSurface();
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
        foreach(var tri in triangles) {
            if (!bitmaps.TryGetValue(tri.Page,out var bmp)) continue;
            var points = new SKPoint[3]; var uvs = new SKPoint[3];
            for(int i=0;i<3;i++) {
                points[i]=new SKPoint(e.Info.Width/2f+(tri.XY[2*i]-cx)*scale,e.Info.Height/2f-(tri.XY[2*i+1]-cy)*scale);
                uvs[i]=new SKPoint(tri.UV[2*i]*bmp.Width,tri.UV[2*i+1]*bmp.Height);
            }
            using var vertices=SKVertices.CreateCopy(SKVertexMode.Triangles, points, uvs, null);
            using var shader=SKShader.CreateBitmap(bmp,SKShaderTileMode.Clamp,SKShaderTileMode.Clamp);
            using var paint=new SKPaint { Shader=shader,IsAntialias=true };
            canvas.DrawVertices(vertices,SKBlendMode.Modulate,paint);
        }
    }
}

public static class Spine41Geometry
{
    public static IReadOnlyList<SpineTriangle> Extract(Skeleton? skeleton)
    {
        var output=new List<SpineTriangle>();
        if(skeleton==null)return output;
        foreach(var slot in skeleton.DrawOrder) {
            float[] xy,uv;int[] indices; AtlasRegion? region;
            if(slot.Attachment is RegionAttachment quad) {
                xy=new float[8];quad.ComputeWorldVertices(slot,xy,0,2);
                uv=quad.UVs;indices=new[]{0,1,2,2,3,0};region=quad.Region as AtlasRegion;
            } else if(slot.Attachment is MeshAttachment mesh) {
                xy=new float[mesh.WorldVerticesLength];mesh.ComputeWorldVertices(slot,0,xy.Length,xy,0,2);
                uv=mesh.UVs;indices=mesh.Triangles;region=mesh.Region as AtlasRegion;
            } else continue;
            if(region==null || uv==null || indices==null)continue;
            for(int i=0;i+2<indices.Length;i+=3) {
                var coords=new float[6];var tex=new float[6];bool valid=true;
                for(int j=0;j<3;j++) {
                    int k=indices[i+j]*2;
                    if(k+1>=xy.Length || k+1>=uv.Length){valid=false;break;}
                    coords[j*2]=xy[k];coords[j*2+1]=xy[k+1];
                    tex[j*2]=uv[k];tex[j*2+1]=uv[k+1];
                }
                if(valid)output.Add(new SpineTriangle(region.page.name,coords,tex));
            }
        }
        return output;
    }
}
