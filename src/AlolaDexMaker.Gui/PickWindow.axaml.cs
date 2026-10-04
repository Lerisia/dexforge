using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PKHeX.Core;

namespace AlolaDexMaker.Gui;

/// <summary>One thing that may be picked for the spare room.</summary>
public sealed record PickRow(string Key, string Label);

/// <summary>The list of what the event box leaves out, to pick from for the spare room: searched, ticked, and handed back as keys.</summary>
public partial class PickWindow : Window
{
    private readonly List<string> picks;
    private readonly List<PickRow> all;
    public ObservableCollection<PickRow> Shown { get; } = [];
    public ObservableCollection<PickRow> Chosen { get; } = [];
    private readonly HashSet<string> chosenKeys;
    private bool filling;

    public PickWindow(List<string> picks)
    {
        this.picks = picks;
        var ko = GameInfo.GetStrings("ko");
        var numbers = new Dictionary<string, ushort>(); for (ushort i = 1; i <= 807; i++) numbers[ko.Species[i]] = i;
        all = EventBox.Custom.Candidates(EventBox.Rows.Distributions(), numbers).Select(c => new PickRow(c.Key, c.Label)).ToList();
        chosenKeys = picks.ToHashSet();
        DataContext = this;
        InitializeComponent();
        SearchBox.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Fill(); };
        Chosen.CollectionChanged += (_, e) =>
        {
            if (filling) return;
            if (e.NewItems is not null) foreach (PickRow r in e.NewItems) chosenKeys.Add(r.Key);
            if (e.OldItems is not null) foreach (PickRow r in e.OldItems) chosenKeys.Remove(r.Key);
            Counted();
        };
        Fill();
    }

    /// <summary>The rows the search leaves, with the ones already picked ticked.</summary>
    private void Fill()
    {
        filling = true;
        var q = (SearchBox.Text ?? "").Trim();
        var words = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Shown.Clear(); Chosen.Clear();
        foreach (var r in all.Where(r => words.All(w => r.Label.Contains(w, StringComparison.OrdinalIgnoreCase)))) Shown.Add(r);
        foreach (var r in Shown.Where(r => chosenKeys.Contains(r.Key))) Chosen.Add(r);
        filling = false;
        Counted();
    }

    private void Counted()
    {
        int room = EventBox.EventBoxMaking.Room;
        Count.Text = $"{chosenKeys.Count} / {room}";
        Note.Text = chosenKeys.Count > room ? $"빈 칸은 {room}개입니다. {chosenKeys.Count - room}개를 빼 주세요." : $"{Shown.Count}개 보임 · {all.Count}개 중";
    }

    private void Clear(object? sender, RoutedEventArgs e) { chosenKeys.Clear(); Fill(); }

    private void Done(object? sender, RoutedEventArgs e)
    {
        if (chosenKeys.Count > EventBox.EventBoxMaking.Room) return;
        picks.Clear();
        picks.AddRange(all.Where(r => chosenKeys.Contains(r.Key)).Select(r => r.Key));
        Close();
    }
}
