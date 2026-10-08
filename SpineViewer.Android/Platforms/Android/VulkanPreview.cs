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
    private readonly Action presented;
    private readonly Func<IReadOnlyList<SpineTriangle>> triangles;
    private readonly Func<IReadOnlyDictionary<string, byte[]>> pngs;
    private readonly HashSet<string> uploaded = new(StringComparer.OrdinalIgnoreCase);
    // Reuse managed frame buffers and UTF-8 page pointers across animation frames.
    private float[] vertexScratch = Array.Empty<float>();
    private int[] countScratch = Array.Empty<int>();
    private int[] modeScratch = Array.Empty<int>();
    private IntPtr[] pageScratch = Array.Empty<IntPtr>();
    private readonly Dictionary<string, IntPtr> pagePointers = new(StringComparer.OrdinalIgnoreCase);
    private int width=1,height=1;
    private static int Grow(int current, int required) {
        int size = Math.Max(16, current);
        while (size < required) size = checked(size * 2);
        return size;
    }
    private void EnsureFrameCapacity(int triangles) {
        int floats = checked(triangles * 12);
        if (vertexScratch.Length < floats) Array.Resize(ref vertexScratch, Grow(vertexScratch.Length, floats));
        // Worst case: every triangle changes its texture or blend mode.
        if (countScratch.Length < triangles) Array.Resize(ref countScratch, Grow(countScratch.Length, triangles));
        if (modeScratch.Length < triangles) Array.Resize(ref modeScratch, Grow(modeScratch.Length, triangles));
        if (pageScratch.Length < triangles) Array.Resize(ref pageScratch, Grow(pageScratch.Length, triangles));
    }
    private IntPtr PagePointer(string page) {
        if (pagePointers.TryGetValue(page, out var pointer)) return pointer;
        pointer = Marshal.StringToCoTaskMemUTF8(page);
        pagePointers.Add(page, pointer);
        return pointer;
    }
    public VulkanPreviewCallback(Action<string> status, Action presented, Func<IReadOnlyList<SpineTriangle>> triangles, Func<IReadOnlyDictionary<string,byte[]>> pngs) {
        this.status=status;this.presented=presented;this.triangles=triangles;this.pngs=pngs;
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
        foreach (var pointer in pagePointers.Values) Marshal.FreeCoTaskMem(pointer);
        pagePointers.Clear();
    }
    public void Render() {
        if(renderer==IntPtr.Zero)return;
        try {
            foreach(var kv in pngs()) {
                if(uploaded.Contains(kv.Key))continue;
                byte[] rgba = SpineAtlasPixels.Decode(kv.Value, out int textureWidth, out int textureHeight);
                if(VulkanNative.SetTexture(renderer,kv.Key,rgba,textureWidth,textureHeight)==1)uploaded.Add(kv.Key);
            }
            var tris=triangles();
            if(tris.Count>0) {
                float minX=float.MaxValue,minY=float.MaxValue,maxX=float.MinValue,maxY=float.MinValue;
                foreach(var t in tris)for(int j=0;j<6;j+=2) {
                    minX=Math.Min(minX,t.XY[j]);maxX=Math.Max(maxX,t.XY[j]);
                    minY=Math.Min(minY,t.XY[j+1]);maxY=Math.Max(maxY,t.XY[j+1]);
                }
                float cx=(minX+maxX)*0.5f,cy=(minY+maxY)*0.5f;
                // Fit uniformly into the actual Vulkan surface without stretching X or Y.
                float scale=Math.Clamp(Math.Min(Math.Max(1f,width*0.90f)/Math.Max(1f,maxX-minX),Math.Max(1f,height*0.90f)/Math.Max(1f,maxY-minY)),0.01f,8f);
                EnsureFrameCapacity(tris.Count);
                int n=0, batchCount=0;
                for(int i=0;i<tris.Count;) {
                    var t=tris[i];int start=n;
                    while(i<tris.Count && tris[i].Blend==t.Blend && string.Equals(tris[i].Page,t.Page,StringComparison.OrdinalIgnoreCase)) {
                        var tri=tris[i++];
                        for(int j=0;j<3;j++) {vertexScratch[n++]=tri.XY[j*2];vertexScratch[n++]=tri.XY[j*2+1];vertexScratch[n++]=tri.UV[j*2];vertexScratch[n++]=tri.UV[j*2+1];}
                    }
                    countScratch[batchCount] = n-start;
                    pageScratch[batchCount] = PagePointer(t.Page);
                    modeScratch[batchCount] = t.Blend switch {BlendMode.Additive=>1,BlendMode.Multiply=>2,BlendMode.Screen=>3,_=>0};
                    batchCount++;
                }
                var camera = SpineCamera.Snapshot();
                float effectiveScale = scale * camera.Zoom;
                VulkanNative.SetFrame(renderer,vertexScratch,n,countScratch,modeScratch,pageScratch,batchCount,
                    cx-camera.PanX/effectiveScale,cy+camera.PanY/effectiveScale,effectiveScale);
            }
            if(VulkanNative.Draw(renderer)!=1)status("Vulkan: erro ao apresentar quadro");
            else presented();
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
    public Action OnFramePresented { get; set; } = () => { };
    public SpineVulkanView() { HeightRequest = -1; }
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
    readonly SpineTouch touch;
    public SpineVulkanSurface(global::Android.Content.Context context) : base(context)
    {
        touch = new SpineTouch(Render);
    }
    public override bool OnTouchEvent(MotionEvent? e) => touch.Handle(this, e);
    public void Attach(SpineVulkanView view)
    {
        Detach();
        callback = new VulkanPreviewCallback(view.OnStatus, view.OnFramePresented, () => view.GetTriangles(), () => view.GetTextures());
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
