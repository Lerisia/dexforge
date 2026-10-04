using Avalonia.Controls;
using Avalonia.Layout;

namespace Dexforge.Gui;

/// <summary>A day, picked as a year, a month, and a day of that month.</summary>
public sealed class DayBox : UserControl
{
    /// <summary>The years the console's clock can be set to.</summary>
    public const int FirstYear = 2000, LastYear = 2099;

    private readonly ComboBox year = new() { MinWidth = 92 }, month = new() { MinWidth = 72 }, day = new() { MinWidth = 72 };
    private bool setting;

    public event EventHandler? Changed;

    public DayBox()
    {
        year.ItemsSource = Enumerable.Range(FirstYear, LastYear - FirstYear + 1).ToList();
        month.ItemsSource = Enumerable.Range(1, 12).ToList();
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (box, unit) in new[] { (year, "년"), (month, "월"), (day, "일") })
        {
            row.Children.Add(box);
            var said = new TextBlock { Text = unit };
            said.Classes.Add("unit");
            row.Children.Add(said);
        }
        Content = row;
        Day = DateOnly.FromDateTime(DateTime.Today);
        year.SelectionChanged += (_, _) => Picked();
        month.SelectionChanged += (_, _) => Picked();
        day.SelectionChanged += (_, _) => Picked();
    }

    public DateOnly Day
    {
        get => new(FirstYear + year.SelectedIndex, month.SelectedIndex + 1, day.SelectedIndex + 1);
        set
        {
            setting = true;
            year.SelectedIndex = Math.Clamp(value.Year - FirstYear, 0, LastYear - FirstYear);
            month.SelectedIndex = value.Month - 1;
            Days(value.Day);
            setting = false;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The days the month has, and the one picked among them: the last, where the month is shorter than the one before.</summary>
    private void Days(int wanted)
    {
        int has = DateTime.DaysInMonth(FirstYear + year.SelectedIndex, month.SelectedIndex + 1);
        day.ItemsSource = Enumerable.Range(1, has).ToList();
        day.SelectedIndex = Math.Min(wanted, has) - 1;
    }

    private void Picked()
    {
        if (setting || year.SelectedIndex < 0 || month.SelectedIndex < 0) return;
        setting = true;
        if (day.ItemCount != DateTime.DaysInMonth(FirstYear + year.SelectedIndex, month.SelectedIndex + 1)) Days(Math.Max(day.SelectedIndex, 0) + 1);
        setting = false;
        if (day.SelectedIndex >= 0) Changed?.Invoke(this, EventArgs.Empty);
    }
}
