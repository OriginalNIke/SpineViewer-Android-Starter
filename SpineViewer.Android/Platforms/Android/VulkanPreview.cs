using System.Runtime.InteropServices;
using Android.App;
using Android.Views;
using Android.Runtime;

namespace SpineViewer.Android;

internal static class VulkanNative {
    [DllImport("spine_vulkan", EntryPoint = "spine_vk_create_surface")]
    internal static extern IntPtr Create(IntPtr env, IntPtr surface);
    [DllImport("spine_vulkan", EntryPoint = "spine_vk_draw_clear")]
    internal static extern int Draw(IntPtr renderer);
    [DllImport("spine_vulkan", EntryPoint = "spine_vk_destroy")]
    internal static extern void Destroy(IntPtr renderer);
}

internal sealed class VulkanPreviewCallback : Java.Lang.Object, ISurfaceHolderCallback {
    private IntPtr renderer;
    private readonly Action<string> status;
    public VulkanPreviewCallback(Action<string> status) => this.status = status;
    public void SurfaceCreated(ISurfaceHolder holder) {
        try {
            // Java.Interop exposes the thread's JNIEnv pointer via JNIEnv.Handle.
            renderer = VulkanNative.Create(JNIEnv.Handle, holder.Surface!.Handle);
            status(renderer == IntPtr.Zero ? "Vulkan: falha ao inicializar" :
                VulkanNative.Draw(renderer) == 1 ? "Vulkan: quadro apresentado" : "Vulkan: falha na apresentação");
        } catch (Exception ex) { status("Vulkan: " + ex.Message); }
    }
    public void SurfaceChanged(ISurfaceHolder holder, global::Android.Graphics.Format format, int width, int height) {
        // Recreate swapchain on resize in a subsequent stage.
    }
    public void SurfaceDestroyed(ISurfaceHolder holder) {
        if (renderer != IntPtr.Zero) { VulkanNative.Destroy(renderer); renderer = IntPtr.Zero; }
    }
}

internal static class VulkanPreview {
    public static void Show(Action<string> status) {
        var activity = Platform.CurrentActivity ?? throw new InvalidOperationException("Activity indisponível");
        var view = new SurfaceView(activity);
        var callback = new VulkanPreviewCallback(status);
        view.Holder!.AddCallback(callback);
        var dialog = new Dialog(activity);
        dialog.SetContentView(view, new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, 600));
        dialog.Show();
    }
}
