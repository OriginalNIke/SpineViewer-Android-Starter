using SkiaSharp.Views.Maui.Controls.Hosting;
namespace SpineViewer.Android;
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.ConfigureMauiHandlers(h => { h.AddHandler<SpineGLView, SpineGLHandler>(); h.AddHandler<SpineVulkanView, SpineVulkanHandler>(); });
        return builder.Build();
    }
}
