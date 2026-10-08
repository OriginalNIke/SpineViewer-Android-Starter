using Android.Graphics;
namespace SpineViewer.Android;
internal static class SpineBackgroundImage
{
    internal const string TextureKey = "__spineviewer_background__";
    private static byte[]? png;
    private static int version;
    internal static byte[]? Png => System.Threading.Volatile.Read(ref png);
    internal static int Version => System.Threading.Volatile.Read(ref version);
    internal static int Width { get; private set; }
    internal static int Height { get; private set; }
    internal static void Set(byte[]? data)
    {
        if (data != null) {
            using var bitmap = BitmapFactory.DecodeByteArray(data, 0, data.Length)
                ?? throw new InvalidOperationException("Imagem inválida.");
            if (bitmap.Width > 4096 || bitmap.Height > 4096)
                throw new InvalidOperationException("Imagem muito grande. Máximo: 4096 × 4096.");
            Width = bitmap.Width; Height = bitmap.Height;
        } else { Width = 0; Height = 0; }
        System.Threading.Volatile.Write(ref png, data);
        System.Threading.Interlocked.Increment(ref version);
    }
    internal static float[] Quad(float cx, float cy, float scale, int width, int height)
    {
        // Preencher sem deformar (crop central) e ignorar zoom/pan do personagem.
        float screenAspect = (float)width / Math.Max(1, height);
        float imageAspect = (float)Width / Math.Max(1, Height);
        float u0=0, u1=1, v0=0, v1=1;
        if (imageAspect > screenAspect) { float f=screenAspect/imageAspect; u0=(1-f)/2;u1=1-u0; }
        else { float f=imageAspect/screenAspect; v0=(1-f)/2;v1=1-v0; }
        float hx=width/(2*scale), hy=height/(2*scale);
        float l=cx-hx,rr=cx+hx,b=cy-hy,t=cy+hy;
        return new float[] {l,b,u0,v1, rr,b,u1,v1, rr,t,u1,v0,
                            l,b,u0,v1, rr,t,u1,v0, l,t,u0,v0};
    }
}
