using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Platform;

namespace AlolaDexMaker.Gui;

/// <summary>
/// The help: what the program is for and how it makes what it makes. The words are kept in Assets/help.md,
/// written with headings (#, ##, ###), lists (-) and **strong** words, and nothing else.
/// </summary>
public partial class HelpWindow : Window
{
    public static readonly Uri Words = new("avares://AlolaDexMaker/Assets/help.md");

    public HelpWindow()
    {
        InitializeComponent();
        foreach (var block in Read(Text())) Page.Children.Add(block);
    }

    public static string Text()
    {
        using var reader = new StreamReader(AssetLoader.Open(Words));
        return reader.ReadToEnd();
    }

    private IEnumerable<Control> Read(string text)
    {
        var muted = (IBrush)this.FindResource("Muted")!;
        var sun = (IBrush)this.FindResource("Sun")!;
        bool first = true;
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("### "))
                yield return Said(line[4..], 14.5, FontWeight.SemiBold, new Thickness(0, 16, 0, 5));
            else if (line.StartsWith("## "))
                yield return Said(line[3..], 17, FontWeight.SemiBold, new Thickness(0, first ? 0 : 28, 0, 8));
            else if (line.StartsWith("# "))
                yield return Said(line[2..], 22, FontWeight.SemiBold, new Thickness(0, 0, 0, 18));
            else if (line.StartsWith("- "))
            {
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("18,*"), Margin = new Thickness(2, 0, 0, 6) };
                row.Children.Add(new TextBlock { Text = "•", Foreground = sun });
                var said = Said(line[2..], 14, FontWeight.Normal, default);
                Grid.SetColumn(said, 1);
                row.Children.Add(said);
                yield return row;
            }
            else
                yield return Said(line, 14, FontWeight.Normal, new Thickness(0, 0, 0, 9));
            if (!line.StartsWith("# ")) first = false;
        }
    }

    /// <summary>A line of the help, with whatever stands between two pairs of stars said strongly.</summary>
    private static SelectableTextBlock Said(string line, double size, FontWeight weight, Thickness margin)
    {
        var block = new SelectableTextBlock { FontSize = size, FontWeight = weight, Margin = margin, TextWrapping = TextWrapping.Wrap, LineHeight = size * 1.6 };
        var parts = line.Split("**");
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0) continue;
            block.Inlines!.Add(new Run(parts[i]) { FontWeight = i % 2 == 1 ? FontWeight.SemiBold : weight });
        }
        return block;
    }
}
