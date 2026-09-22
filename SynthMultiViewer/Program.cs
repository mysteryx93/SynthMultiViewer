using Avalonia;
using ReactiveUI.Avalonia;
using ReactiveUI.Builder;

namespace HanumanInstitute.SynthMultiViewer;

internal static class Program
{
    /// <summary>
    /// Configures ReactiveUI and starts the desktop application.
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        RxAppBuilder.CreateReactiveUIBuilder()
            .WithAvalonia()
            .WithCoreServices()
            .BuildApp();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Configures Avalonia for desktop startup and the previewer.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        // AppImage starts through an AppRun symlink, so the process name is AppRun.
        // RESOURCE_NAME is the X11 instance; WmClass is the class. Both must match
        // StartupWMClass and the desktop file id, or the shell keeps a private icon.
        Environment.SetEnvironmentVariable("RESOURCE_NAME", DesktopId);
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new X11PlatformOptions { WmClass = DesktopId })
            .LogToTrace()
            .UseReactiveUI(_ => { });
    }

    private const string DesktopId = "synthmultiviewer";
}
