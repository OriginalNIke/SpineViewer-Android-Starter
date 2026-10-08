namespace SpineViewer.Android;
public class App : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
        => new Window(new NavigationPage(new MainPage()));
}
