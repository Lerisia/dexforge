using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace AlolaDexMaker.Gui;

public partial class App : Application
{
    /// <summary>The letters the window is set in, carried inside: the same on every computer, with or without a Korean font of its own.</summary>
    public const string Letters = "avares://AlolaDexMaker/Assets#Pretendard JP";

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.MainWindow = new MainWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
