using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using PKHeX.Core;
using Xunit;

namespace AlolaDexMaker.Gui.Tests;

public class TheWindow
{
    /// <summary>The TID the game shows, as typed.</summary>
    private const uint Shown = 567890;

    private static T The<T>(Window w, string name) where T : Control => w.FindControl<T>(name) ?? throw new InvalidOperationException($"no {name} on the form");

    private static string Fresh(string what)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"alola-dex-maker-{what}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>Where a picture of the window goes, when one is asked for.</summary>
    private static void Picture(Window w, string name)
    {
        if (Environment.GetEnvironmentVariable("DEXGEN_GUI_SHOTS") is not { Length: > 0 } dir) return;
        Dispatcher.UIThread.RunJobs();
        Directory.CreateDirectory(dir);
        w.CaptureRenderedFrame()?.Save(Path.Combine(dir, name + ".png"));
    }

    [AvaloniaFact]
    public void OpensOnWhatWasDecided()
    {
        var w = new MainWindow(); w.Show();
        Assert.True(w.Read(out var asked, out _));
        Assert.Equal("미월", asked.Name);
        Assert.Null(asked.Tid); Assert.Null(asked.Sid);
        Assert.Equal((int)Ball.Poke, asked.Ball);
        Assert.True(asked.Shiny);
        Assert.Equal(IvChoice.Random, asked.Ivs);
        Assert.Equal(new DateOnly(2018, 1, 1), asked.From);
        Assert.Equal(new DateOnly(2018, 12, 31), asked.To);
        Assert.Equal(SexChoice.Random, asked.Sex);
        Assert.Equal(LevelChoice.Lowest, asked.Level);
        Assert.Equal(("Selene", "ミヅキ", "美月"), (asked.English, asked.Japanese, asked.Chinese));
        // Plain before shiny, as the colours were ordered.
        var plain = The<RadioButton>(w, "Plain"); var shiny = The<RadioButton>(w, "Shiny");
        var row = (Panel)plain.Parent!;
        Assert.True(row.Children.IndexOf(plain) < row.Children.IndexOf(shiny));
        Picture(w, "1-opened");
        w.Close();
    }

    [AvaloniaFact]
    public void OffersEveryBallOfTheGenerationButTheCherishBall()
    {
        var w = new MainWindow(); w.Show();
        var names = The<ComboBox>(w, "BallBox").Items.Cast<string>().ToList();
        var all = GameInfo.GetStrings("ko").balllist;
        var wanted = Enumerable.Range(1, 26).Where(i => i != (int)Ball.Cherish).Select(i => all[i]).ToList();
        Assert.Equal(wanted.Order(), names.Order());
        Assert.Equal(all[(int)Ball.Poke], names[0]);
        w.Close();
    }

    [AvaloniaFact]
    public void ReadsWhatIsFilledIn()
    {
        var w = new MainWindow(); w.Show();
        The<TextBox>(w, "NameBox").Text = "  달님 ";
        The<TextBox>(w, "TidBox").Text = "567890";
        The<TextBox>(w, "SidBox").Text = "1234";
        The<RadioButton>(w, "PickedBalls").IsChecked = true;
        The<RadioButton>(w, "Plain").IsChecked = true;
        The<RadioButton>(w, "IvFive").IsChecked = true;
        The<RadioButton>(w, "SexMale").IsChecked = true;
        The<RadioButton>(w, "LevelHundred").IsChecked = true;
        The<TextBox>(w, "EnglishBox").Text = " Luna ";
        The<TextBox>(w, "JapaneseBox").Text = "ルナ";
        The<TextBox>(w, "ChineseBox").Text = "";
        The<DayBox>(w, "FromBox").Day = new DateOnly(2018, 3, 31);
        The<DayBox>(w, "ToBox").Day = new DateOnly(2020, 2, 29);
        Assert.True(w.Read(out var asked, out _));
        Assert.Equal("달님", asked.Name);
        Assert.Equal(567890u, asked.Tid); Assert.Equal(1234u, asked.Sid);
        Assert.Null(asked.Ball);
        Assert.False(asked.Shiny);
        Assert.Equal(IvChoice.FiveFromEggs, asked.Ivs);
        Assert.Equal(SexChoice.Male, asked.Sex);
        Assert.Equal(LevelChoice.Hundred, asked.Level);
        // Left empty, a name is the one it was.
        Assert.Equal(("Luna", "ルナ", "美月"), (asked.English, asked.Japanese, asked.Chinese));
        Assert.Equal(new DateOnly(2018, 3, 31), asked.From);
        Assert.Equal(new DateOnly(2020, 2, 29), asked.To);
        Assert.Contains("[SID]TID", The<TextBlock>(w, "IdHint").Text);
        // A ball picked from the list is a ball asked for.
        var shelf = The<ComboBox>(w, "BallBox");
        shelf.SelectedIndex = Array.IndexOf(MainWindow.Shelf, Ball.Luxury);
        Assert.True(w.Read(out asked, out _));
        Assert.Equal((int)Ball.Luxury, asked.Ball);
        Picture(w, "2-filled");
        w.Close();
    }

    [AvaloniaTheory]
    [InlineData("4295", "", "SID")] [InlineData("-1", "", "SID")] [InlineData("12a", "", "SID")] [InlineData("12345", "", "SID")]
    [InlineData("", "1000000", "TID")] [InlineData("", "1.5", "TID")] [InlineData("4294", "967296", "967295")]
    public void RefusesAnIdThatIsNotOne(string sid, string tid, string said)
    {
        var w = new MainWindow(); w.Show();
        The<TextBox>(w, "SidBox").Text = sid;
        The<TextBox>(w, "TidBox").Text = tid;
        Assert.False(w.Read(out _, out var why));
        Assert.Contains(said, why);
        Assert.Contains(said, The<TextBlock>(w, "IdHint").Text);
        The<Button>(w, "MakeButton").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Null(w.Last);
        Assert.Contains(said, The<TextBlock>(w, "Status").Text);
        Picture(w, "3-refused-id");
        w.Close();
    }

    [AvaloniaFact]
    public void OpensTheHelpOnce()
    {
        var w = new MainWindow(); w.Show();
        var button = The<Button>(w, "HelpButton");
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        var help = w.Helping;
        Assert.NotNull(help);
        Assert.True(help!.IsVisible);
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Same(help, w.Helping);
        // Everything written is on the page, and none of the marks it was written with.
        var page = help.FindControl<StackPanel>("Page")!;
        var shown = string.Join("\n", page.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Inlines is { Count: > 0 } runs ? string.Concat(runs.OfType<Avalonia.Controls.Documents.Run>().Select(r => r.Text)) : t.Text));
        foreach (var line in HelpWindow.Text().Split('\n').Select(l => l.Trim()).Where(l => l.Length != 0))
            Assert.Contains(line.TrimStart('#', '-', ' ').Replace("**", ""), shown);
        Assert.DoesNotContain("**", shown);
        Assert.DoesNotContain("#", shown);
        Assert.Contains("개발자의 미적 감각으로 임의로 정한 것입니다", shown);
        Picture(help, "7-help");
        help.Close();
        Assert.Null(w.Helping);
        w.Close();
    }

    [AvaloniaFact]
    public void SaysWhatEachChoiceOfValuesMeans()
    {
        var w = new MainWindow(); w.Show();
        var hint = The<TextBlock>(w, "IvHint");
        Assert.Equal("적법한 선에서 완전 랜덤입니다.", hint.Text);
        The<RadioButton>(w, "IvFive").IsChecked = true;
        Assert.Equal("알에서 나온 포켓몬은 5V가 됩니다.", hint.Text);
        Assert.Equal("볼맞춤", The<RadioButton>(w, "PickedBalls").Content);
        w.Close();
    }

    [AvaloniaFact]
    public void SaysHowManyCannotGoInTheBallPicked()
    {
        var w = new MainWindow(); w.Show();
        var hint = The<TextBlock>(w, "BallHint");
        Assert.DoesNotContain("마리", hint.Text);            // a Poke Ball holds everything
        The<ComboBox>(w, "BallBox").SelectedIndex = Array.IndexOf(MainWindow.Shelf, Ball.Sport);
        Assert.Contains($"{BallFits.CannotGoIn[Ball.Sport]}마리는", hint.Text);
        Assert.Contains("컴퍼티션볼", hint.Text);
        The<ComboBox>(w, "BallBox").SelectedIndex = Array.IndexOf(MainWindow.Shelf, Ball.Luxury);
        Assert.Contains($"{BallFits.CannotGoIn[Ball.Luxury]}마리는", hint.Text);
        The<RadioButton>(w, "PickedBalls").IsChecked = true;
        Assert.DoesNotContain("마리", hint.Text);
        Picture(w, "8-ball-count");
        w.Close();
    }

    [AvaloniaFact]
    public void ADayKeepsToItsMonth()
    {
        var box = new DayBox { Day = new DateOnly(2020, 2, 29) };
        Assert.Equal(new DateOnly(2020, 2, 29), box.Day);
        box.Day = new DateOnly(2018, 1, 31);
        var month = ((Panel)box.Content!).Children.OfType<ComboBox>().ElementAt(1);
        month.SelectedIndex = 1; // February 2018
        Assert.Equal(new DateOnly(2018, 2, 28), box.Day);
    }

    [AvaloniaFact]
    public async Task MakesTheSaveWhenTheButtonIsPressed()
    {
        var dir = Fresh("made");
        var w = new MainWindow { Under = dir }; w.Show();
        The<TextBox>(w, "NameBox").Text = "달님";
        The<TextBox>(w, "TidBox").Text = "567890";
        The<TextBox>(w, "SidBox").Text = "1234";
        The<ComboBox>(w, "BallBox").SelectedIndex = Array.IndexOf(MainWindow.Shelf, Ball.Luxury);
        The<RadioButton>(w, "SexFemale").IsChecked = true;
        The<RadioButton>(w, "LevelHundred").IsChecked = true;
        The<Button>(w, "MakeButton").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.False(The<Button>(w, "MakeButton").IsEnabled);
        Assert.True(The<ProgressBar>(w, "Bar").IsVisible);
        Picture(w, "4-making");
        await w.Working;
        Assert.False(The<ProgressBar>(w, "Bar").IsVisible);

        Assert.NotNull(w.Last);
        Assert.Equal(0, w.Last!.Code);
        Assert.Equal(Path.Combine(dir, $"AlolaDexMaker-달님-{Shown:000000}"), w.Last.Folder);
        Assert.True(File.Exists(w.Last.Record));
        Assert.True(SaveUtil.TryGetSaveFile(File.ReadAllBytes(w.Last.Save!), out var read));
        var sav = Assert.IsType<SAV7USUM>(read);
        Assert.Equal("달님", sav.OT); Assert.Equal(567890u, sav.TrainerTID7); Assert.Equal(1234u, sav.TrainerSID7);
        var all = sav.BoxData.Concat(sav.PartyData).Where(p => p.Species != 0).ToList();
        Assert.Equal(954, all.Count);
        Assert.All(all, p => Assert.True(new LegalityAnalysis(p).Valid, $"{p.Species} is not legal"));
        Assert.Equal(807, all.Select(p => p.Species).Distinct().Count());
        // One ball was asked for: whatever is not on a card is in it, or in a Poke Ball.
        Assert.All(all.Where(p => p.Ball != (int)Ball.Cherish && !p.FatefulEncounter), p => Assert.Contains(p.Ball, new[] { (byte)Ball.Luxury, (byte)Ball.Poke }));
        Assert.Contains(all, p => p.Ball == (int)Ball.Luxury);
        // Level 100 in the boxes, the party as it was.
        Assert.All(sav.BoxData.Where(p => p.Species != 0), p => Assert.Equal(100, p.CurrentLevel));
        Assert.Contains(sav.PartyData, p => p.CurrentLevel < 100);

        Assert.True(The<Button>(w, "MakeButton").IsEnabled);
        Assert.True(The<Button>(w, "OpenButton").IsVisible);
        Assert.Contains("다 만들었습니다", The<TextBlock>(w, "Status").Text);
        Picture(w, "5-made");
        w.Close();
        Directory.Delete(dir, true);
    }

    [AvaloniaFact]
    public async Task SaysWhyWhenThePeriodCannotBe()
    {
        var dir = Fresh("refused");
        var w = new MainWindow { Under = dir }; w.Show();
        The<DayBox>(w, "FromBox").Day = new DateOnly(2019, 1, 1);
        The<DayBox>(w, "ToBox").Day = new DateOnly(2018, 1, 1);
        The<Button>(w, "MakeButton").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        await w.Working;
        Assert.Equal(2, w.Last!.Code);
        Assert.Contains("마지막 날이 첫날보다 앞섭니다", The<TextBlock>(w, "Status").Text);
        Assert.False(The<Button>(w, "OpenButton").IsVisible);
        Assert.Empty(Directory.GetFileSystemEntries(dir));
        Picture(w, "6-refused-period");
        w.Close();
        Directory.Delete(dir, true);
    }
}
