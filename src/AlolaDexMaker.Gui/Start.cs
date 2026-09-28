using System.Text;
using Avalonia;
using Avalonia.Media;

namespace AlolaDexMaker.Gui;

// Alola Dex Maker    the window: what the command line asks, asked on a form
internal static class Start
{
    /// <summary>Where what went wrong is written down: a window that dies has nowhere else to say it.</summary>
    public static string FaultLog => Path.Combine(AppContext.BaseDirectory, "AlolaDexMaker-오류.txt");

    [STAThread]
    public static int Main(string[] args)
    {
        try { Build().StartWithClassicDesktopLifetime(args); return 0; }
        catch (Exception ex) { Note(ex); return 1; }
    }

    public static AppBuilder Build() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .With(new FontManagerOptions { DefaultFamilyName = App.Letters })
        .LogToTrace();

    public static void Note(Exception ex)
    {
        try { File.AppendAllText(FaultLog, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{ex}\n\n", new UTF8Encoding(true)); }
        catch (Exception) { /* nowhere to write either */ }
    }
}
