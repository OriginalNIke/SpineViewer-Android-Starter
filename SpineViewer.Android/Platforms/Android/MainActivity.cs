using Android.App;
using Android.Content.PM;
using Microsoft.Maui;

namespace SpineViewer.Android;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation |
    ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize |
    ConfigChanges.Density, Exported = true)]
public class MainActivity : MauiAppCompatActivity
{
    const int FolderRequest = 4182;
    static TaskCompletionSource<global::Android.Net.Uri?>? pendingFolder;

    public static Task<global::Android.Net.Uri?> SelectFolderAsync()
    {
        if (pendingFolder is not null)
            throw new InvalidOperationException("Já existe uma seleção de pasta em andamento.");
        var activity = Platform.CurrentActivity as MainActivity
            ?? throw new InvalidOperationException("Activity Android indisponível.");
        pendingFolder = new TaskCompletionSource<global::Android.Net.Uri?>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var intent = new global::Android.Content.Intent(global::Android.Content.Intent.ActionOpenDocumentTree);
            intent.AddFlags(global::Android.Content.ActivityFlags.GrantReadUriPermission |
                            global::Android.Content.ActivityFlags.GrantPersistableUriPermission);
            activity.StartActivityForResult(intent, FolderRequest);
        }
        catch
        {
            pendingFolder = null;
            throw;
        }
        return pendingFolder.Task;
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, global::Android.Content.Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode != FolderRequest) return;
        var pending = pendingFolder;
        pendingFolder = null;
        var uri = resultCode == Result.Ok ? data?.Data : null;
        if (uri is not null)
        {
            try { ContentResolver?.TakePersistableUriPermission(uri, global::Android.Content.ActivityFlags.GrantReadUriPermission); }
            catch (Java.Lang.SecurityException) { /* Leitura permanece disponível nesta sessão. */ }
        }
        pending?.TrySetResult(uri);
    }
}
