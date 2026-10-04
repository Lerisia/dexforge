using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Dexforge.Gui;

/// <summary>One ribbon on the list: its picture, name and title, and whether it is ticked.</summary>
public sealed class RibbonRow(Ribbon ribbon, bool isChecked) : INotifyPropertyChanged
{
    public Ribbon Ribbon { get; } = ribbon;
    public string Note => Ribbon.Note + (Ribbon.OnlyOlder ? " — 4세대에서 올라온 포켓몬만" : "");
    public Bitmap? Picture { get; } = Load(ribbon.Image);
    private bool @checked = isChecked;
    public bool Checked { get => @checked; set { if (@checked == value) return; @checked = value; Changed(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static Bitmap? Load(string file)
    {
        try { using var s = AssetLoader.Open(new Uri("avares://Dexforge/Assets/ribbons/" + file)); return new Bitmap(s); }
        catch { return null; }
    }
}

/// <summary>The ribbons the national dex can be asked for, ticked and handed back as PKHeX's keys.</summary>
public partial class RibbonWindow : Window
{
    private readonly List<string> chosen;
    public List<RibbonRow> Rows { get; }

    /// <param name="among">The game's list: the Ultra Sun one unless given.</param>
    public RibbonWindow(List<string> chosen, IReadOnlyList<Ribbon>? among = null)
    {
        this.chosen = chosen;
        Rows = (among ?? Ribbons.All).Select(r => new RibbonRow(r, chosen.Contains(r.Key))).ToList();
        DataContext = this;
        InitializeComponent();
        foreach (var r in Rows) r.PropertyChanged += (_, _) => Counted();
        Counted();
    }

    private void Counted() => Note.Text = Rows.Count(r => r.Checked) is var n && n == 0 ? "고른 리본 없음" : $"{n}개 고름";

    private void Clear(object? sender, RoutedEventArgs e) { foreach (var r in Rows) r.Checked = false; }

    private void Done(object? sender, RoutedEventArgs e)
    {
        chosen.Clear();
        chosen.AddRange(Rows.Where(r => r.Checked).Select(r => r.Ribbon.Key));
        Close();
    }
}
