using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using PKHeX.Core;

namespace Dexforge.Gui;

/// <summary>The form: what is asked, as the command line asks it, and the making of it.</summary>
public partial class MainWindow : Window
{
    /// <summary>The balls of this generation, as a bag keeps them. The Cherish Ball is not among them: only cards come in it.</summary>
    public static readonly Ball[] Shelf =
    [
        Ball.Poke, Ball.Great, Ball.Ultra, Ball.Master, Ball.Premier, Ball.Heal, Ball.Net, Ball.Nest, Ball.Dive, Ball.Dusk, Ball.Timer, Ball.Quick, Ball.Repeat, Ball.Luxury,
        Ball.Level, Ball.Lure, Ball.Moon, Ball.Friend, Ball.Love, Ball.Heavy, Ball.Fast, Ball.Safari, Ball.Sport, Ball.Dream, Ball.Beast,
    ];

    public const string DefaultName = "미월";
    public static readonly DateOnly DefaultFrom = new(2018, 1, 1), DefaultTo = new(2018, 12, 31);

    private readonly string[] balls = GameInfo.GetStrings("ko").balllist;
    private string under = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    private bool busy;
    /// <summary>Whether the period was changed by hand; until then each game shows its own default.</summary>
    private bool datesTouched;
    private bool switching;

    /// <summary>The folder the save's own folder is made in.</summary>
    public string Under { get => under; set { under = value; WhereBox.Text = value; } }
    /// <summary>The making that is going on, or the last there was.</summary>
    public Task Working { get; private set; } = Task.CompletedTask;
    /// <summary>What came of the last making.</summary>
    public Made? Last { get; private set; }

    public MainWindow()
    {
        InitializeComponent();
        BallBox.ItemsSource = Shelf.Select(b => balls[(int)b]).ToList();
        BallBox.SelectedIndex = 0;
        FromBox.Day = DefaultFrom; ToBox.Day = DefaultTo;
        WhereBox.Text = under;
        foreach (var box in new[] { FromBox, ToBox }) box.Changed += (_, _) => { if (!switching) datesTouched = true; };

        foreach (var box in new[] { SidBox, TidBox }) box.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Ids(); };
        foreach (var r in new[] { OneBall, PickedBalls, Plain, Shiny, IvRandom, IvFive, SexMale, SexFemale, SexRandom, LevelLowest, LevelHundred }) r.IsCheckedChanged += (_, _) => Hints();
        foreach (var r in new[] { GameUltraSun, GameSword, GameEventBox, GameArceus }) r.IsCheckedChanged += (_, _) => { GameChanged(); Hints(); };
        foreach (var r in new[] { SizeSmallest, SizeAlpha, SizeRandom }) r.IsCheckedChanged += (_, _) => Hints();
        foreach (var r in new[] { ReceivedAnyDay, ReceivedFirstDays }) r.IsCheckedChanged += (_, _) => Hints();
        FirstDaysBox.GotFocus += (_, _) => ReceivedFirstDays.IsChecked = true;
        BallBox.SelectionChanged += (_, _) => { OneBall.IsChecked = true; Hints(); };
        Ids(); GameChanged(); Hints(); Rest();
    }

    private static bool Id(string? text, int digits, out uint? id)
    {
        id = null;
        var s = (text ?? "").Trim();
        if (s.Length == 0) return true;
        if (s.Length > digits || !uint.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var v)) return false;
        id = v;
        return true;
    }

    /// <summary>Why the SID and TID on the form cannot be one trainer's, or nothing.</summary>
    private string? IdsRefused()
    {
        if (!Id(SidBox.Text, 4, out var sid)) return "SID는 0000에서 4294 사이의 네 자리 수로 적어 주세요.";
        if (!Id(TidBox.Text, 6, out var tid)) return "TID는 000000에서 999999 사이의 여섯 자리 수로 적어 주세요.";
        return Ids7.Refused(tid, sid);
    }

    private void Ids()
    {
        var why = IdsRefused();
        IdHint.Foreground = why is null ? (IBrush)this.FindResource("Muted")! : (IBrush)this.FindResource("Fault")!;
        IdHint.Text = why ?? "게임과 PKHeX가 보여 주는 [SID]TID입니다. 비워 두면 무작위로 정합니다.";
    }

    private void Hints()
    {
        var chosen = (IsArceus ? Arceus.Making8a.Balls : Shelf)[Math.Max(BallBox.SelectedIndex, 0)];
        BallHint.Text = IsArceus
            ? "히스이 지방의 볼. 조우가 볼을 정한 것(스타팅, 디아루가·펄기아의 오리진볼)은 그 볼입니다."
            : IsSword
            ? (PickedBalls.IsChecked == true ? "포켓몬마다 골라 둔 볼. 선물·화석·배포는 정해진 볼입니다." : "넣을 수 없는 포켓몬은 몬스터볼에 넣습니다. 선물·화석·배포는 정해진 볼입니다.")
            : PickedBalls.IsChecked == true || chosen == Ball.Poke
            ? "배포 포켓몬은 카드가 정한 볼에 넣습니다."
            : BallFits.CannotGoIn[chosen] is var cannot and > 0
                ? $"{BallFits.Boxed}마리 중 {cannot}마리는 {balls[(int)chosen]}에 넣을 수 없어 몬스터볼에 넣습니다. 배포 포켓몬은 카드가 정한 볼에 넣습니다."
                : "배포 포켓몬은 카드가 정한 볼에 넣습니다.";
        ColourHint.Text = Shiny.IsChecked == true
            ? (IsArceus ? "전설·스타팅 같은 고정 조우는 이로치가 막혀 일반 색입니다." : IsSword ? "막힌 것과 마휘핑 크림 9폼은 일반 색입니다." : "이로치가 막힌 포켓몬은 일반 색입니다.")
            : IsArceus ? "의뢰의 포니타는 늘 이로치입니다." : "카드가 이로치로 정한 배포는 이로치입니다.";
        SizeHint.Text = SizeSmallest.IsChecked == true ? "키 0, 무게 0 (XXXS). 우두머리와 고정 조우는 게임이 정한 크기입니다. 시드를 찾느라 1분쯤 걸립니다."
            : SizeAlpha.IsChecked == true ? "우두머리가 있는 종은 전부 우두머리로 (크기 최대). 없는 종은 게임이 뽑은 대로입니다."
            : "게임이 뽑은 대로입니다.";
        IvHint.Text = IvFive.IsChecked == true ? "알에서 나온 포켓몬은 5V가 됩니다."
            : "적법한 선에서 완전 랜덤입니다.";
        SexHint.Text = SexRandom.IsChecked == true ? "종마다 원래 성비대로 정해집니다." : "무성이거나 성별이 정해진 포켓몬은 그대로입니다.";
        LevelHint.Text = LevelHundred.IsChecked == true ? "기술은 그대로입니다." : IsArceus ? "잡은 레벨 그대로, 진화에 필요한 만큼만 올립니다." : "포켓몬마다 가질 수 있는 가장 낮은 레벨입니다.";
        if (!busy && Last is null) Rest();
    }

    /// <summary>Before anything is made: how long it is going to take.</summary>
    private void Rest()
    {
        Say(IsEventBox ? "만드는 데 1분쯤 걸립니다." : IsSword ? "만드는 데 몇 분 걸립니다." : IsArceus && SizeSmallest.IsChecked == true ? "만드는 데 1분쯤 걸립니다." : "만드는 데 몇 초 걸립니다.", "Muted");
    }

    /// <summary>Legends: Arceus: the Hisui dex, caught from seeds.</summary>
    public bool IsArceus => GameArceus.IsChecked == true;

    /// <summary>Sword or Ultra Sun: which rows there are to fill.</summary>
    public bool IsSword => GameSword.IsChecked == true;
    /// <summary>The event box: an Ultra Sun save of every distribution.</summary>
    public bool IsEventBox => GameEventBox.IsChecked == true;

    /// <summary>What was picked for the event box's spare room, by key.</summary>
    public List<string> Picks { get; } = [];
    /// <summary>The ribbons picked for the national dex, by PKHeX's keys.</summary>
    public List<string> RibbonKeys { get; } = [];
    /// <summary>The ribbons picked for the Sword dex, by PKHeX's keys.</summary>
    public List<string> SwordRibbonKeys { get; } = [];
    private RibbonWindow? ribbonPicker;

    private List<string> RibbonKeysOfGame => IsSword ? SwordRibbonKeys : RibbonKeys;
    private IReadOnlyList<Ribbon> RibbonsOfGame => IsSword ? Ribbons.Sword : Ribbons.All;

    private async void PickRibbons(object? sender, RoutedEventArgs e)
    {
        if (ribbonPicker is not null) { ribbonPicker.Activate(); return; }
        ribbonPicker = new RibbonWindow(RibbonKeysOfGame, RibbonsOfGame);
        ribbonPicker.Closed += (_, _) => { ribbonPicker = null; ShowRibbons(); };
        await ribbonPicker.ShowDialog(this);
    }

    private void ShowRibbons()
    {
        var keys = RibbonKeysOfGame; var among = RibbonsOfGame;
        RibbonCount.Text = keys.Count == 0 ? "고른 리본 없음" : string.Join(", ", keys.Select(k => Ribbons.Find(k, among)?.Name ?? k));
    }

    private void GameChanged()
    {
        bool sword = IsSword, events = IsEventBox, arceus = IsArceus;
        foreach (var row in new Control[] { ForeignRow, IvRow }) row.IsVisible = !sword && !events && !arceus;
        RibbonRow.IsVisible = !events && !arceus;
        ShowRibbons();
        foreach (var row in new Control[] { SexRow, LevelRow, PeriodRow }) row.IsVisible = !sword && !events;
        foreach (var row in new Control[] { BallRow, ColourRow }) row.IsVisible = !events;
        YearRow.IsVisible = sword;
        SizeRow.IsVisible = arceus;
        PickedBalls.IsVisible = !arceus;
        ReceivedRow.IsVisible = events; CustomRow.IsVisible = events;
        // the shelf of balls is the game's
        var shelf = arceus ? Arceus.Making8a.Balls : Shelf;
        if (!ReferenceEquals(BallBox.Tag, shelf)) { BallBox.Tag = shelf; BallBox.ItemsSource = shelf.Select(b => balls[(int)b]).ToList(); BallBox.SelectedIndex = 0; OneBall.IsChecked = true; }
        switching = true;
        if (arceus && !datesTouched) { FromBox.Day = Arceus.Making8a.Released; ToBox.Day = new DateOnly(2022, 12, 31); }
        else if (!arceus && !datesTouched) { FromBox.Day = DefaultFrom; ToBox.Day = DefaultTo; }
        switching = false;
        Subtitle.Text = arceus
            ? "LEGENDS 아르세우스 히스이도감 세이브 만들기 · 242종 313마리 (폼까지) · 한국어"
            : events
            ? "배포 박스 세이브 만들기 · 3~7세대 배포 751건 + 최종 진화체 · 울트라썬, 한국어"
            : sword
            ? "소드 전국도감 세이브 만들기 · 663종 760마리 (폼까지) + 배포 159마리 · 한국어"
            : "울트라썬 전국도감 세이브 만들기 · 807종 · 한국 본체, 한국어, 여자 주인공";
        GameHint.Text = arceus
            ? "히스이도감 242종의 전 폼. 야생은 스포너의 시드에서 뽑아(슬롯 추첨까지 맞음), 진화체는 야생에서 진화, 전설·스타팅은 고정 조우, 폼 체인지는 잡은 폼에서. 연구는 전 종 10. JKSV 로 복원하는 폴더가 나옵니다."
            : events
            ? "3세대부터 7세대까지의 모든 배포 카드(한국 > 일본 > 미국 > 유럽 순으로 하나씩), 알은 부화시켜, 미진화체는 최종 진화체도. 전부 배포 기간 안의 날짜로 받아 7세대까지 올린 것으로 만듭니다."
            : sword
            ? "가라르·갑옷섬·왕관설원 도감의 전 종과 폼. 알이 되는 것은 알, 화석은 화석, 전설은 고정 조우와 다이맥스 어드벤처, 환상은 배포 카드. 소드가 받은 배포 81건과 그 최종 진화체도 도감 뒤에. JKSV 로 복원하는 폴더가 나옵니다."
            : "전국도감 807종. 알이 되는 것은 알, 나머지는 이 게임에서 잡거나 받은 것, 배포, 이전 게임에서 온 것.";
        WhereHint.Text = arceus
            ? "이 안에 'Dexforge-Arceus-이름-TID' 폴더를 만들어 JKSV 백업(main 등 네 파일)과 기록을 씁니다."
            : events
            ? "이 안에 'Dexforge-EventBox-이름-TID' 폴더를 만들어 세이브(main)와 기록을 씁니다."
            : sword
            ? "이 안에 'Dexforge-Sword-이름-TID' 폴더를 만들어 JKSV 백업(main 등 네 파일)과 기록을 씁니다."
            : "이 안에 'Dexforge-이름-TID' 폴더를 만들어 세이브(main)와 기록을 씁니다.";
    }

    /// <summary>What the form asks for the event box; or what on it cannot be read.</summary>
    public bool ReadEvents(out EventBox.EventOptions asked, out string why)
    {
        asked = null!; why = "";
        var name = (NameBox.Text ?? "").Trim();
        if (name.Length == 0) name = DefaultName;
        if (name.Length > 6) { why = "어버이 이름은 6글자까지입니다."; NameBox.Focus(); return false; }
        if (IdsRefused() is { } badId) { why = badId; (Id(SidBox.Text, 4, out _) ? TidBox : SidBox).Focus(); return false; }
        Id(SidBox.Text, 4, out var sid); Id(TidBox.Text, 6, out var tid);
        int firstDays = 0;
        if (ReceivedFirstDays.IsChecked == true && (!int.TryParse((FirstDaysBox.Text ?? "").Trim(), out firstDays) || firstDays < 1 || firstDays > 999))
        { why = "처음 며칠인지 1 에서 999 사이의 수로 적어 주세요."; FirstDaysBox.Focus(); return false; }
        asked = new EventBox.EventOptions(name, tid, sid, Random.Shared.Next(), firstDays, Picks.ToList());
        return true;
    }

    private PickWindow? picker;

    /// <summary>The spare room's list, beside the form.</summary>
    private async void Pick(object? sender, RoutedEventArgs e)
    {
        if (picker is not null) { picker.Activate(); return; }
        picker = new PickWindow(Picks);
        picker.Closed += (_, _) =>
        {
            picker = null;
            PickCount.Text = Picks.Count == 0 ? "고른 것 없음" : $"{Picks.Count}개 고름 (빈 칸 {EventBox.EventBoxMaking.Room}개)";
        };
        await picker.ShowDialog(this);
    }

    /// <summary>What the form asks for Legends: Arceus; or what on it cannot be read.</summary>
    public bool Read8a(out Options8a asked, out string why)
    {
        asked = null!; why = "";
        var name = (NameBox.Text ?? "").Trim();
        if (name.Length == 0) name = DefaultName;
        if (!Id(SidBox.Text, 4, out var sid)) { why = "SID 는 네 자리 수입니다."; return false; }
        if (!Id(TidBox.Text, 6, out var tid)) { why = "TID 는 여섯 자리 수입니다."; return false; }
        int ball = (int)Arceus.Making8a.Balls[Math.Max(BallBox.SelectedIndex, 0)];
        var size = SizeSmallest.IsChecked == true ? SizeChoice.Smallest : SizeAlpha.IsChecked == true ? SizeChoice.Alpha : SizeChoice.Random;
        var sex = SexMale.IsChecked == true ? SexChoice.Male : SexFemale.IsChecked == true ? SexChoice.Female : SexChoice.Random;
        var level = LevelHundred.IsChecked == true ? LevelChoice.Hundred : LevelChoice.Lowest;
        asked = new Options8a(name, tid, sid, FromBox.Day, ToBox.Day, Random.Shared.Next(), ball, Shiny.IsChecked == true, size, level, sex);
        return true;
    }

    public bool Read8(out Options8 asked, out string why)
    {
        asked = null!; why = "";
        var name = (NameBox.Text ?? "").Trim();
        if (name.Length == 0) name = DefaultName;
        if (name.Length > 6) { why = "어버이 이름은 6글자까지입니다."; NameBox.Focus(); return false; }
        if (IdsRefused() is { } badId) { why = badId; (Id(SidBox.Text, 4, out _) ? TidBox : SidBox).Focus(); return false; }
        Id(SidBox.Text, 4, out var sid); Id(TidBox.Text, 6, out var tid);
        if (!int.TryParse((YearBox.Text ?? "").Trim(), out var year) || year is < 2019 or > 2099) { why = "해는 2019 부터 2099 까지입니다."; YearBox.Focus(); return false; }
        int? ball = PickedBalls.IsChecked == true ? null : (int)Shelf[Math.Max(BallBox.SelectedIndex, 0)];
        asked = new Options8(name, tid, sid, year, Random.Shared.Next(), ball, Shiny.IsChecked == true) { Ribbons = SwordRibbonKeys.ToList() };
        return true;
    }

    private void Say(string what, string colour, bool strong = false)
    {
        Status.Text = what;
        Status.Foreground = (IBrush)this.FindResource(colour)!;
        Status.FontWeight = strong ? FontWeight.SemiBold : FontWeight.Normal;
    }

    /// <summary>What the form asks for; or what on it cannot be read.</summary>
    public bool Read(out Options asked, out string why)
    {
        asked = null!; why = "";
        var name = (NameBox.Text ?? "").Trim();
        if (name.Length == 0) name = DefaultName;
        if (name.Length > 6) { why = "어버이 이름은 6글자까지입니다."; NameBox.Focus(); return false; }
        if (IdsRefused() is { } badId) { why = badId; (Id(SidBox.Text, 4, out _) ? TidBox : SidBox).Focus(); return false; }
        Id(SidBox.Text, 4, out var sid); Id(TidBox.Text, 6, out var tid);
        string Foreign(TextBox box, string otherwise) => (box.Text ?? "").Trim() is { Length: > 0 } t ? t : otherwise;
        string english = Foreign(EnglishBox, ForeignNames.English), japanese = Foreign(JapaneseBox, ForeignNames.Japanese), chinese = Foreign(ChineseBox, ForeignNames.Chinese);
        int? ball = PickedBalls.IsChecked == true ? null : (int)Shelf[Math.Max(BallBox.SelectedIndex, 0)];
        var ivs = IvFive.IsChecked == true ? IvChoice.FiveFromEggs : IvChoice.Random;
        var sex = SexMale.IsChecked == true ? SexChoice.Male : SexFemale.IsChecked == true ? SexChoice.Female : SexChoice.Random;
        var level = LevelHundred.IsChecked == true ? LevelChoice.Hundred : LevelChoice.Lowest;
        asked = new Options(name, tid, sid, FromBox.Day, ToBox.Day, Random.Shared.Next(), ball, ivs, Shiny.IsChecked == true, level, sex, english, japanese, chinese) { Ribbons = RibbonKeys.ToList() };
        if (ForeignNames.Refused(asked) is { } refused) { why = refused; return false; }
        return true;
    }

    private void Make(object? sender, RoutedEventArgs e)
    {
        if (busy) return;
        if (IsEventBox)
        {
            if (!ReadEvents(out var askedE, out var whyE)) { Refuse([whyE]); return; }
            Working = Work(into => EventBox.EventBoxMaking.Run(askedE, null, into, (done, of) => Dispatcher.UIThread.Post(() => Going(done, of))));
            return;
        }
        if (IsArceus)
        {
            if (!Read8a(out var askedA, out var whyA)) { Refuse([whyA]); return; }
            Working = Work(into => Arceus.Making8a.Run(askedA, null, into, (done, of) => Dispatcher.UIThread.Post(() => Going(done, of))));
            return;
        }
        if (IsSword)
        {
            if (!Read8(out var asked8, out var why8)) { Refuse([why8]); return; }
            Working = Work(into => Sword.Making8.Run(asked8, null, into, (done, of) => Dispatcher.UIThread.Post(() => Going(done, of))));
            return;
        }
        if (!Read(out var asked, out var why)) { Refuse([why]); return; }
        Working = Work(into => Making.Run(asked, null, into, (done, of) => Dispatcher.UIThread.Post(() => Going(done, of))));
    }

    private async Task Work(Func<string, Made> run)
    {
        Busy(true);
        Last = null;
        Detail.IsVisible = false; OpenButton.IsVisible = false;
        Bar.Value = 0; Bar.IsVisible = true;
        Say("만드는 중입니다…", "Ink");
        var into = under;
        try
        {
            var made = await Task.Run(() => run(into));
            Last = made;
            Bar.IsVisible = false;
            if (made.Code == 0) Finished(made); else Refuse(made.Refused);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            Bar.IsVisible = false;
            Refuse([$"'{into}' 에 쓸 수 없습니다. 저장할 곳을 바꿔 주세요.", ex.Message]);
        }
        catch (Exception ex)
        {
            Start.Note(ex);
            Bar.IsVisible = false;
            Refuse(["만들다가 멈췄습니다.", ex.Message, $"자세한 내용: {Start.FaultLog}"]);
        }
        finally { Busy(false); }
    }

    private void Going(int done, int of)
    {
        if (!busy) return;
        Bar.Maximum = of; Bar.Value = done;
        Status.Text = $"만드는 중입니다… {done} / {of}";
    }

    private void Finished(Made made)
    {
        Say("다 만들었습니다.", "Done", strong: true);
        var lines = new List<string>();
        if (made.Me is { } me) lines.Add($"{me.Name} · SID {me.Sid7:0000} · TID {me.Shown:000000}");
        if (made.Checked is { } c) lines.Add($"포켓몬 {c.Count}마리 · {c.Species}종 · 이로치 {c.Shiny}마리 · 합법 {c.Legal} / {c.Count}");
        else lines.AddRange(made.Lines.Where(l => l.StartsWith("포켓몬") || l.StartsWith("박스") || l.StartsWith("합법") || l.StartsWith("이로치") || l.StartsWith("도감") || l.StartsWith("앨범")).Select(l => string.Join(' ', l.Split(' ', StringSplitOptions.RemoveEmptyEntries))));
        lines.Add(made.Folder!);
        Detail.Text = string.Join("\n", lines); Detail.IsVisible = true;
        OpenButton.IsVisible = true;
    }

    private void Refuse(IReadOnlyList<string> why)
    {
        Say(why.Count == 0 ? "만들지 못했습니다." : why[0].Trim(), "Fault", strong: true);
        Detail.Text = string.Join("\n", why.Skip(1).Select(l => l.Trim()));
        Detail.IsVisible = why.Count > 1;
        OpenButton.IsVisible = false;
    }

    private void Busy(bool now)
    {
        busy = now;
        MakeButton.IsEnabled = !now;
        foreach (var c in new Control[] { GameUltraSun, GameSword, GameEventBox, NameBox, EnglishBox, JapaneseBox, ChineseBox, SidBox, TidBox, OneBall, BallBox, PickedBalls, Plain, Shiny, IvRandom, IvFive, SexMale, SexFemale, SexRandom, LevelLowest, LevelHundred, YearBox, FromBox, ToBox, ChooseButton, ReceivedAnyDay, ReceivedFirstDays, FirstDaysBox, PickButton }) c.IsEnabled = !now;
    }

    private HelpWindow? help;

    /// <summary>The help, beside the form: one of it, brought to the front if it is open already.</summary>
    private void Help(object? sender, RoutedEventArgs e)
    {
        if (help is not null) { help.Activate(); return; }
        help = new HelpWindow();
        help.Closed += (_, _) => help = null;
        help.Show(this);
    }

    /// <summary>The help, if it is open.</summary>
    public HelpWindow? Helping => help;

    private async void Choose(object? sender, RoutedEventArgs e)
    {
        var picked = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "세이브를 저장할 곳", AllowMultiple = false });
        if (picked.Count != 0 && picked[0].TryGetLocalPath() is { } path) Under = path;
    }

    private async void Open(object? sender, RoutedEventArgs e)
    {
        if (Last?.Folder is not { } folder) return;
        try { await Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(folder)); }
        catch (Exception ex) { Start.Note(ex); }
    }
}
