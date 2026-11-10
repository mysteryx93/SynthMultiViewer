using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Headless;
using ReactiveUI.Avalonia;
using ReactiveUI.Builder;

[assembly: AvaloniaTestApplication(typeof(HanumanInstitute.SynthMultiViewer.Tests.TestApplication))]

namespace HanumanInstitute.SynthMultiViewer.Tests;

public static class TestApplication
{
    [ModuleInitializer]
    internal static void InitReactiveUI() =>
        RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseHeadless(new() { UseHeadlessDrawing = false })
            .UseSkia()
            .WithDataAnnotationsValidation()
            .UseReactiveUI(_ => { });
}
