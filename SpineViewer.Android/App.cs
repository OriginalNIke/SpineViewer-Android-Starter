namespace SpineViewer.Android;
public class App : Application
{
    public App() { MainPage = new NavigationPage(new MainPage()); }
}
