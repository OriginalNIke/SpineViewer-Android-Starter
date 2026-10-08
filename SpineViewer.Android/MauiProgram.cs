using SkiaSharp.Views.Maui.Controls.Hosting;
namespace SpineViewer.Android;
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().UseSkiaSharp();
        return builder.Build();
    }
}
