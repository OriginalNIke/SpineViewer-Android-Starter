using BlendMode = SpineRuntime41.BlendMode;
using System.Runtime.InteropServices;
using Android.App;
using Android.Views;
using Android.Runtime;
using Android.Graphics;
using Microsoft.Maui.Handlers;
using SpineRuntime41;

namespace SpineViewer.Android;

internal static class VulkanNative {
    [DllImport("spine_vulkan", EntryPoint="spine_vk_create_surface")]
    internal static extern IntPtr Create(IntPtr env, IntPtr surface);
    [DllImport("spine_vulkan", EntryPoint="spine_vk_draw_clear")]
    internal static extern int Draw(IntPtr renderer);
    [DllImport("spine_vulkan", EntryPoint="spine_vk_destroy")]
    internal static extern void Destroy(IntPtr renderer);
    [DllImport("spine_vulkan", EntryPoint="spine_vk_set_shaders")]
    internal static extern int SetShaders(IntPtr renderer, byte[] vs, int vsSize, byte[] fs, int fsSize);
    [DllImport("spine_vulkan", EntryPoint="spine_vk_set_texture_rgba", CharSet=CharSet.Ansi)]
    internal static extern int SetTexture(IntPtr renderer, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, byte[] rgba, int width, int height);
    [DllImport("spine_vulkan", EntryPoint="spine_vk_set_frame")]
    internal static extern int SetFrame(IntPtr renderer, float[] vertices, int floatCount, int[] counts, int[] modes, IntPtr[] pages, int batchCount, float centerX, float centerY, float scale);
}

internal sealed class VulkanPreviewCallback : Java.Lang.Object, ISurfaceHolderCallback {
    private IntPtr renderer;
    private readonly Action<string> status;
    private readonly Func<IReadOnlyList<SpineTriangle>> triangles;
    private readonly Func<IReadOnlyDictionary<string, byte[]>> pngs;
    private readonly HashSet<string> uploaded = new(StringComparer.OrdinalIgnoreCase);
    private int width=1,height=1;
    public VulkanPreviewCallback(Action<string> status, Func<IReadOnlyList<SpineTriangle>> triangles, Func<IReadOnlyDictionary<string,byte[]>> pngs) {
        this.status=status;this.triangles=triangles;this.pngs=pngs;
    }
    public void SurfaceCreated(ISurfaceHolder holder) {
        VulkanPreview.SetActive(this);
        try {
            renderer=VulkanNative.Create(JNIEnv.Handle,holder.Surface!.Handle);
            if(renderer==IntPtr.Zero) {status("Vulkan: falha ao inicializar");return;}
            using var vert=Platform.CurrentActivity!.Assets!.Open("vulkan/spine.vert.spv");
            using var frag=Platform.CurrentActivity!.Assets!.Open("vulkan/spine.frag.spv");
            using var vb=new MemoryStream();using var fb=new MemoryStream();
            vert.CopyTo(vb);frag.CopyTo(fb);
            byte[] v=vb.ToArray(),f=fb.ToArray();
            if(VulkanNative.SetShaders(renderer,v,v.Length,f,f.Length)!=1) {status("Vulkan: erro nos shaders/pipelines");return;}
            Render();
            status("Vulkan: pipelines e shaders SPIR-V carregados");
        } catch(Exception ex) {status("Vulkan: "+ex.Message);}
    }
    public void SurfaceChanged(ISurfaceHolder holder, global::Android.Graphics.Format format,int w,int h) {width=Math.Max(1,w);height=Math.Max(1,h);}
    public void SurfaceDestroyed(ISurfaceHolder holder) => Release();
    internal void Release() {
        VulkanPreview.ClearActive(this);
        if(renderer!=IntPtr.Zero) {VulkanNative.Destroy(renderer);renderer=IntPtr.Zero;}
        uploaded.Clear();
    }
    public void Render() {
        if(renderer==IntPtr.Zero)return;
        try {
            foreach(var kv in pngs()) {
                if(uploaded.Contains(kv.Key))continue;
                using var bmp=BitmapFactory.DecodeByteArray(kv.Value,0,kv.Value.Length);
                if(bmp==null)continue;
                int[] pixels=new int[bmp.Width*bmp.Height];
                bmp.GetPixels(pixels,0,bmp.Width,0,0,bmp.Width,bmp.Height);
                byte[] rgba=new byte[pixels.Length*4];
                for(int i=0;i<pixels.Length;i++) {
                    uint p=unchecked((uint)pixels[i]);int j=i*4;
                    rgba[j]=(byte)(p>>16);rgba[j+1]=(byte)(p>>8);rgba[j+2]=(byte)p;rgba[j+3]=(byte)(p>>24);
                }
                if(VulkanNative.SetTexture(renderer,kv.Key,rgba,bmp.Width,bmp.Height)==1)uploaded.Add(kv.Key);
            }
            var tris=triangles();
            if(tris.Count>0) {
                float minX=float.MaxValue,minY=float.MaxValue,maxX=float.MinValue,maxY=float.MinValue;
                foreach(var t in tris)for(int j=0;j<6;j+=2) {
                    minX=Math.Min(minX,t.XY[j]);maxX=Math.Max(maxX,t.XY[j]);
                    minY=Math.Min(minY,t.XY[j+1]);maxY=Math.Max(maxY,t.XY[j+1]);
                }
                float cx=(minX+maxX)*0.5f,cy=(minY+maxY)*0.5f;
                float scale=Math.Clamp(Math.Min((width-32f)/Math.Max(1,maxX-minX),(height-32f)/Math.Max(1,maxY-minY)),0.01f,8f);
                var data=new float[tris.Count*12];var counts=new List<int>();var modes=new List<int>();var names=new List<string>();
                int n=0;
                for(int i=0;i<tris.Count;) {
                    var t=tris[i];int start=n;
                    while(i<tris.Count && tris[i].Blend==t.Blend && string.Equals(tris[i].Page,t.Page,StringComparison.OrdinalIgnoreCase)) {
                        var tri=tris[i++];
                        for(int j=0;j<3;j++) {data[n++]=tri.XY[j*2];data[n++]=tri.XY[j*2+1];data[n++]=tri.UV[j*2];data[n++]=tri.UV[j*2+1];}
                    }
                    counts.Add(n-start);names.Add(t.Page);
                    modes.Add(t.Blend switch {BlendMode.Additive=>1,BlendMode.Multiply=>2,BlendMode.Screen=>3,_=>0});
                }
                IntPtr[] pages=names.Select(Marshal.StringToCoTaskMemUTF8).ToArray();
                try {VulkanNative.SetFrame(renderer,data,n,counts.ToArray(),modes.ToArray(),pages,pages.Length,cx,cy,scale);}
                finally {foreach(var p in pages)Marshal.FreeCoTaskMem(p);}
            }
            if(VulkanNative.Draw(renderer)!=1)status("Vulkan: erro ao apresentar quadro");
        } catch(Exception ex) {status("Vulkan: "+ex.Message);}
    }
}

// Embedded Vulkan surface: MAUI owns the native SurfaceView and its lifetime.
public sealed class SpineVulkanView : Microsoft.Maui.Controls.View
{
    internal SpineVulkanSurface? Surface;
    public Func<IReadOnlyList<SpineTriangle>> GetTriangles { get; set; } = () => Array.Empty<SpineTriangle>();
    public Func<IReadOnlyDictionary<string, byte[]>> GetTextures { get; set; } = () => new Dictionary<string, byte[]>();
    public Action<string> OnStatus { get; set; } = _ => { };
    public SpineVulkanView() { HeightRequest = 520; }
    public void InvalidateSurface() => Surface?.Render();
}

public sealed class SpineVulkanHandler : ViewHandler<SpineVulkanView, SpineVulkanSurface>
{
    public static readonly IPropertyMapper<SpineVulkanView, SpineVulkanHandler> Mapper =
        new PropertyMapper<SpineVulkanView, SpineVulkanHandler>(ViewMapper);
    public SpineVulkanHandler() : base(Mapper) { }
    protected override SpineVulkanSurface CreatePlatformView() => new(Context);
    protected override void ConnectHandler(SpineVulkanSurface platformView)
    {
        base.ConnectHandler(platformView);
        VirtualView.Surface = platformView;
        platformView.Attach(VirtualView);
    }
    protected override void DisconnectHandler(SpineVulkanSurface platformView)
    {
        platformView.Detach();
        VirtualView.Surface = null;
        base.DisconnectHandler(platformView);
    }
}

public sealed class SpineVulkanSurface : SurfaceView
{
    VulkanPreviewCallback? callback;
    public SpineVulkanSurface(global::Android.Content.Context context) : base(context) { }
    public void Attach(SpineVulkanView view)
    {
        Detach();
        callback = new VulkanPreviewCallback(view.OnStatus, () => view.GetTriangles(), () => view.GetTextures());
        Holder?.AddCallback(callback);
    }
    public void Detach()
    {
        if (callback is null) return;
        Holder?.RemoveCallback(callback);
        callback.Release();
        callback = null;
    }
    public void Render() => callback?.Render();
}

internal static class VulkanPreview
{
    private static VulkanPreviewCallback? active;
    internal static void SetActive(VulkanPreviewCallback callback) => active = callback;
    internal static void ClearActive(VulkanPreviewCallback callback)
    {
        if (ReferenceEquals(active, callback)) active = null;
    }
    public static void Render() => active?.Render();
}
