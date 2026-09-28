using Avalonia;
using Avalonia.Headless;
using Avalonia.Media;
using AlolaDexMaker.Gui.Tests;

[assembly: AvaloniaTestApplication(typeof(Setup))]

namespace AlolaDexMaker.Gui.Tests;

/// <summary>The window's own application, drawn for real but onto no screen.</summary>
public static class Setup
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
        .With(new FontManagerOptions { DefaultFamilyName = App.Letters });
}
