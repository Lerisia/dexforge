using PKHeX.Core;

namespace AlolaDexMaker;

/// <summary>How a Pokemon of the template came to be, which decides how it is drawn again.</summary>
public enum Kind
{
    /// <summary>Hatched or met in this generation, by whoever: nothing about it hangs on anything else.</summary>
    Plain,
    /// <summary>From a card. What the card fixes stays; the rest is drawn as the card says.</summary>
    Card,
    /// <summary>From an older generation, where the personality value and the individual values come off one line of random numbers.</summary>
    Older,
}

public sealed class Generator
{
    private readonly SAV7 sav;
    private readonly Options opt;
    private readonly Draw draw;
    private readonly GameStrings ko = GameInfo.GetStrings("ko");
    private readonly Trainer was;
    public Trainer Was => was;
    public IReadOnlyDictionary<string, DateOnly> Days => days;
    public Trainer Me { get; }
    public Trainer Moon { get; private set; } = null!;
    public DateOnly Began { get; private set; }
    public List<string> Report { get; } = [];
    public List<string> Problems { get; } = [];
    public IEnumerable<string> Notes => older?.Notes ?? [];
    /// <summary>All of them: what this generation drew, and what the sixth did.</summary>
    public IEnumerable<Origin> AllOrigins => Origins;
    /// <summary>The session and the moment each caught Pokemon came of, and the ones that were drawn without one.</summary>
    public IEnumerable<string> Sessions => caught.Notes;
    /// <summary>The state of the egg generator each egg came of, and the two it came from.</summary>
    public IEnumerable<string> Eggs => nursery.Notes;
    private readonly Nursery7 nursery;
    /// <summary>Where the values of each Pokemon drawn from the game's random numbers came from.</summary>
    public List<Origin> Origins { get; } = [];
    private readonly Caught7 caught;
    public Dictionary<Kind, int> Done { get; } = new() { [Kind.Plain] = 0, [Kind.Card] = 0, [Kind.Older] = 0 };
    public Dictionary<Kind, int> Left { get; } = new() { [Kind.Plain] = 0, [Kind.Card] = 0, [Kind.Older] = 0 };
    /// <summary>Told, as the boxes are gone through, how many are done and of how many.</summary>
    public Action<int, int>? Step { get; init; }
    /// <summary>The day the adventure began, where whoever asks fixes it instead of leaving it to the days asked for.</summary>
    public DateOnly? BeganOn { get; init; }

    private static readonly DateOnly Released = new(2017, 11, 17);
    /// <summary>The days the console's clock and the save hold.</summary>
    public static readonly DateOnly Earliest = new(2000, 1, 1), Latest = new(2099, 12, 31);
    /// <summary>Where the Festival Plaza keeps the player's own record: the block, and where in it the record begins.</summary>
    public static readonly (uint Block, int At)[] PlazaRecords = [(20, 0x0), (21, 0x2E80)];
    private readonly Dictionary<uint, Trainer> others = [];
    private HashSet<(ushort, byte)> both = [];
    /// <summary>Whether what is being drawn is of the party, which keeps the sex it has.</summary>
    private bool forParty;

    /// <summary>The sex a Pokemon is to be drawn with: as asked where it is free, as it was where it is not; none where either will do.</summary>
    private byte? SexOf(PK7 old, IEncounterTemplate enc) => keepCast || forParty ? old.Gender : Sexes.Wanted(opt.Sex, old, enc, both);
    private readonly HashSet<uint> used = [];

    public Generator(byte[] template, Options options)
    {
        if (!SaveUtil.TryGetSaveFile(template.ToArray(), out var s) || s is not SAV7 s7) throw new InvalidOperationException("the template is not a save of this generation");
        sav = s7; opt = options; draw = new Draw(new Random(options.Seed));
        caught = new Caught7(draw, options);
        nursery = new Nursery7(draw, options);
        was = new Trainer(sav.OT, sav.Gender, sav.TID16, sav.SID16, sav.Language, sav.Version, sav.ConsoleRegion, sav.Country, sav.Region);
        if (Ids7.Refused(options.Tid, options.Sid) is { } refused) throw new ArgumentException(refused);
        var (tid, sid) = Ids7.Halves(options.Tid, options.Sid, draw);
        Me = was with { Name = options.Name, Tid = tid, Sid = sid };
    }

    /// <summary>
    /// Whether the one made in a Pokemon's place is shiny. What is not shiny in the template cannot be;
    /// what a card makes shiny is shiny whatever is asked for; the rest is as asked.
    /// </summary>
    public static bool Shines(Options opt, PKM old, IEncounterTemplate enc) =>
        old.IsShiny && (opt.Shiny || enc is MysteryGift card and not PGT { IsManaphyEgg: true } && card.Shiny != Shiny.Random);

    private bool IsHis(PKM pk, Trainer t) => pk.ID32 == t.Id32 && pk.OriginalTrainerName == t.Name && pk.Version == t.Version;

    /// <summary>The national dex: the template's boxes, each Pokemon drawn again for the trainer asked for.</summary>
    public byte[] Run()
    {
        var boxes = Prologue();
        FillDex(boxes);
        Album();
        // The Pokedex has seen whatever sex each one now is.
        if (opt.Sex != SexChoice.Female)
            for (int b = 0; b < sav.BoxCount; b++) foreach (var p in sav.GetBoxData(b)) if (p.Species != 0) sav.Zukan.SetDex(p);
        return Finish();
    }

    /// <summary>
    /// Something else in the template's boxes: the same trainer, adventure and party as the national dex gets, and then whatever
    /// <paramref name="fill"/> puts in the boxes (and the album) in place of the dex. The Pokedex is marked for what is there.
    /// </summary>
    public byte[] RunWith(Action<SAV7, Trainer, DateOnly> fill)
    {
        Prologue();
        fill(sav, Me, Began);
        for (int b = 0; b < sav.BoxCount; b++) foreach (var p in sav.GetBoxData(b)) if (p.Species != 0) sav.Zukan.SetDex(p);
        return Finish();
    }

    /// <summary>The trainer asked for, the day the adventure began, the party drawn again for that trainer; the template's boxes as they are, for whoever fills them.</summary>
    private List<(int box, int slot, PK7 pk, LegalityAnalysis la)> Prologue()
    {
        if (ForeignNames.Refused(opt) is { } refused) throw new ArgumentException(refused);
        // The console's clock can be set to any day, so any period will do that the clock and the save can hold.
        if (opt.From <= Earliest || opt.To > Latest) throw new ArgumentException($"the days must be within {Earliest:yyyy-MM-dd} and {Latest:yyyy-MM-dd}");
        if (opt.To < opt.From) throw new ArgumentException("the last day is before the first");

        var boxes = new List<(int box, int slot, PK7 pk, LegalityAnalysis la)>();
        for (int b = 0; b < sav.BoxCount; b++)
        {
            var data = sav.GetBoxData(b);
            for (int i = 0; i < data.Length; i++) if (data[i].Species != 0) boxes.Add((b, i, (PK7)data[i], new LegalityAnalysis(data[i])));
        }
        var party = sav.PartyData.Select((p, i) => (i, pk: (PK7)p, la: new LegalityAnalysis(p))).ToList();
        both = Sexes.Both(boxes.Select(x => x.pk));

        // Who is who. The other version's trainer is the same person with another game; the friends are strangers, each with a game of their own.
        var moonWas = boxes.Select(x => x.pk).First(p => p.Version == GameVersion.UM && p.OriginalTrainerName == was.Name);
        Moon = Me with { Tid = draw.Id16(), Sid = draw.Id16(), Version = GameVersion.UM };
        others[moonWas.ID32] = Moon;

        // The adventure begins three weeks or more before anything of the trainer's own, and a week or more before the first card this save
        // received stopped being handed out. Where the period asked for leaves room, after the game came out; the clock can be set to any day.
        var latest = opt.From.AddDays(-21);
        var deadline = Events.FirstAlbumDeadline.AddDays(-7);
        if (deadline < latest) latest = deadline;
        if (latest <= Earliest) latest = opt.From.AddDays(-1);
        var first = opt.From.AddDays(-45);
        if (first > latest) first = Released < latest ? Released : latest.AddDays(-24);
        if (first < Earliest) first = Earliest;
        Began = BeganOn ?? draw.Day(first, latest);

        // The save's own trainer.
        sav.OT = Me.Name; sav.TID16 = Me.Tid; sav.SID16 = Me.Sid;
        // The save's own number, as a new game would have drawn one: eight bytes, written twice over.
        var nex = new byte[8]; draw.Rnd.NextBytes(nex);
        var status = sav.AllBlocks.Single(b => b.ID == 3);
        var wasNex = sav.MyStatus.NexUniqueID;
        if (wasNex[..16] != wasNex[16..]) throw new InvalidOperationException("the template's own number is not laid out as expected");
        sav.MyStatus.NexUniqueID = Convert.ToHexString(nex) + Convert.ToHexString(nex);
        nexWanted = Convert.ToHexString(nex) + Convert.ToHexString(nex);
        sav.MyStatus.FestaID = (uint)draw.Rnd.NextInt64(1, uint.MaxValue);
        var startedAt = new DateTime(2000, 1, 1).AddSeconds(sav.GameTime.SecondsToStart);
        var wasBegan = DateOnly.FromDateTime(startedAt);
        sav.GameTime.SecondsToStart = (uint)(Began.ToDateTime(TimeOnly.FromDateTime(startedAt)) - new DateTime(2000, 1, 1)).TotalSeconds;
        RecentTrainerCache.SetRecentTrainer(sav);
        // The name is kept in three more places: what Rotom calls the player, and the two Festival Plaza records of the player.
        if (sav.FieldMenu.RotomOT != was.Name) throw new InvalidOperationException("Rotom does not call the template's player by the trainer's name");
        sav.FieldMenu.RotomOT = Me.Name;
        foreach (var (id, at) in PlazaRecords)
        {
            var blk = sav.AllBlocks.Single(b => b.ID == id);
            var field = sav.Data.Slice(blk.Offset + at + 8, 0x1A);
            if (StringConverter7.GetString(field) != was.Name) throw new InvalidOperationException($"the Festival Plaza record in block {id} is not the template's player");
            // As the game writes a name: the letters and the end of them. Whatever of the old name would show past that is struck out.
            var wide = System.Text.Encoding.Unicode.GetBytes(Me.Name + "\0");
            int oldEnd = System.Text.Encoding.Unicode.GetByteCount(was.Name + "\0");
            wide.CopyTo(field);
            for (int i = wide.Length; i < oldEnd; i++) field[i] = 0;
        }

        foreach (var (i, pk, la) in party)
        {
            var day = Began.AddDays(pk.MetDate!.Value.DayNumber - wasBegan.DayNumber);
            caught.Charm = false; forParty = true; // the first days of the adventure
            var made = Again(pk, la, day) ?? throw new InvalidOperationException($"{ko.Species[pk.Species]} of the party could not be drawn again");
            caught.Charm = true; forParty = false;
            made = Dress(made);
            made.ResetPartyStats();
            made.RefreshChecksum();
            sav.SetPartySlotAtIndex(made, i, EntityImportSettings.None);
        }
        // What came up from older games together keeps one day: the letters of Unown, and the two from the one Japanese Platinum.
        var lettersDay = draw.Day(opt.From, opt.To);
        var platinumDay = draw.Day(opt.From, opt.To);
        older = new Older(draw, sav, Me, was, ko, opt, lettersDay, platinumDay);

        return boxes;
    }

    private void FillDex(List<(int box, int slot, PK7 pk, LegalityAnalysis la)> boxes)
    {
        // Shaymin before Manaphy: whoever hatches the egg is the trainer Shaymin was caught by.
        int gone = 0;
        foreach (var (box, slot, pk, la) in boxes.OrderBy(x => KindOf(x.pk, x.la) == Kind.Older && x.pk.Species == 490 ? 1 : 0))
        {
            Step?.Invoke(gone++, boxes.Count);
            var kind = KindOf(pk, la);
            PK7? made;
            try
            {
                made = kind switch
                {
                    Kind.Plain => Again(pk, la, pk.Species == 489 ? default : draw.Day(opt.From, opt.To)),
                    Kind.Card => AgainFromCard(pk, la),
                    _ => older.Again(pk, la),
                };
            }
            catch (Exception ex) { Problems.Add($"{Name(pk)}: {ex.Message}"); continue; }
            if (made is null) { Problems.Add($"{Name(pk)}: 다시 뽑지 못함"); continue; }
            var sex = SexOf(pk, la.EncounterMatch);
            if (!Same(pk, made, opt.Ball is null, out var what, sex == pk.Gender)) { Problems.Add($"{Name(pk)}: 바뀌면 안 되는 것이 바뀜 ({what})"); continue; }
            if (pk.Species != 489 && made.IsShiny != Shines(opt, pk, la.EncounterMatch)) { Problems.Add($"{Name(pk)}: 색이 요청과 다름"); continue; }
            if (pk.Species != 489 && sex is { } s && made.Gender != s) { Problems.Add($"{Name(pk)}: 성별이 요청과 다름"); continue; }
            // Phione waits for its day and is drawn, and put in its ball, further down.
            sav.SetBoxSlotAtIndex(pk.Species == 489 ? made : Raise(Dress(made)), box, slot, EntityImportSettings.None);
            Done[kind]++;
        }

        // Phione hatches only of a Manaphy: its day is after the Manaphy came.
        {
            var (box, slot, pk, la) = boxes.Single(x => x.pk.Species == 489);
            var manaphy = (PK7)sav.GetBoxData(boxes.Single(x => x.pk.Species == 490).box)[boxes.Single(x => x.pk.Species == 490).slot];
            var from = manaphy.MetDate!.Value > opt.From ? manaphy.MetDate!.Value : opt.From;
            var made = Again(pk, la, draw.Day(from, opt.To));
            if (made is null) Problems.Add("피오네: 다시 뽑지 못함"); else sav.SetBoxSlotAtIndex(Raise(Dress(made)), box, slot, EntityImportSettings.None);
        }

    }

    private byte[] Finish()
    {
        var written = sav.Write().ToArray();
        if (!SaveUtil.TryGetSaveFile(written.ToArray(), out var again) || again is not SAV7 s2) throw new InvalidOperationException("what was written does not read back");
        var block = s2.AllBlocks.Single(b => b.ID == 3);
        var half = Convert.FromHexString(nexWanted[..16]);
        // PKHeX's setter writes the first eight bytes only; the second eight go in here, and the save is signed again.
        var raw = s2.Write().ToArray();
        if (s2.MyStatus.NexUniqueID != nexWanted)
        {
            var data = (byte[])written.Clone();
            Array.Copy(half, 0, data, block.Offset + 0x20, 8);
            if (!SaveUtil.TryGetSaveFile(data, out var patched) || patched is not SAV7 s3) throw new InvalidOperationException("the patched save does not read back");
            raw = s3.Write().ToArray();
            if (!SaveUtil.TryGetSaveFile(raw.ToArray(), out var check) || check is not SAV7 s4 || s4.MyStatus.NexUniqueID != nexWanted || !s4.ChecksumsValid) throw new InvalidOperationException("the save's own number did not take");
        }
        return raw;
    }

    /// <summary>How the balls came out when one ball was asked for: by ball, and how many gave up a hidden ability for it.</summary>
    public Dictionary<int, int> Balls { get; } = [];
    public int AbilityGivenUp { get; private set; }

    /// <summary>To level 100 where that is asked for, by Rare Candies: the moves stay as they are.</summary>
    private PK7 Raise(PK7 made)
    {
        if (opt.Level != LevelChoice.Hundred) return made;
        made.CurrentLevel = 100;
        made.RefreshChecksum();
        return made;
    }

    private static bool Stands(PK7 pk, IEncounterTemplate was)
    {
        pk.RefreshChecksum();
        var la = new LegalityAnalysis(pk);
        var e = la.EncounterMatch;
        return la.Valid && e.GetType() == was.GetType() && e.Species == was.Species && e.Form == was.Form && e.LevelMin == was.LevelMin && e.Version == was.Version;
    }

    /// <summary>
    /// Into the one ball asked for, where the Pokemon can be in it; into a Poke Ball where it cannot.
    /// What came on a card stays in the card's ball. A hidden ability that alone stands in the way gives place to an ordinary one.
    /// </summary>
    private PK7 Dress(PK7 made)
    {
        if (opt.Ball is not { } want) return made;
        var was = new LegalityAnalysis(made).EncounterMatch;
        PK7 Count(PK7 pk) { Balls[pk.Ball] = Balls.GetValueOrDefault(pk.Ball) + 1; return pk; }
        if (was is MysteryGift) return Count(made);
        // What hatched was put in its ball when it was drawn: its ability came with its other values and is not to be changed after.
        if (was is IEncounterEgg) return Count(made);
        foreach (var ball in new[] { want, (int)Ball.Poke })
        {
            if (made.Ball == ball) return Count(made);
            var pk = (PK7)made.Clone(); pk.Ball = (byte)ball;
            if (Stands(pk, was)) return Count(pk);
            if (pk.AbilityNumber != 4) continue;
            int first = draw.Rnd.Next(100) < 77 ? 0 : 1;
            foreach (var index in new[] { first, 1 - first })
            {
                var plain = (PK7)pk.Clone(); plain.RefreshAbility(index);
                if (!Stands(plain, was)) continue;
                AbilityGivenUp++;
                return Count(plain);
            }
        }
        return Count(made);
    }

    private bool keepCast;

    /// <summary>
    /// The save as it is, with only this put right: whatever hatched or was met in this generation is drawn again from the game's
    /// random numbers - by the trainer it already has, on the day it already has, in the ball it already has.
    /// What came on a card or up from an older game is left alone, and so is everything else in the save.
    /// The options must name the save's own trainer.
    /// </summary>
    /// <param name="only">Species to draw again; none, and everything hatched or met in this generation is. The rest stays as it is, byte for byte.</param>
    public byte[] Refresh(IReadOnlySet<ushort>? only = null)
    {
        if (Me.Name != was.Name || Me.Id32 != was.Id32) throw new ArgumentException("the trainer asked for is not the save's own");
        if (opt.Ball is not null || opt.Ivs != IvChoice.Random || !opt.Shiny) throw new ArgumentException("nothing but the values is to change");
        keepCast = true;
        Moon = Me; // never used: everybody keeps the numbers they have
        Began = DateOnly.FromDateTime(new DateTime(2000, 1, 1).AddSeconds(sav.GameTime.SecondsToStart));

        var party = sav.PartyData.Select((p, i) => (i, pk: (PK7)p, la: new LegalityAnalysis(p))).ToList();
        foreach (var (i, pk, la) in party)
        {
            if (KindOf(pk, la) != Kind.Plain || only is not null && !only.Contains(pk.Species)) continue;
            caught.Charm = false; // the first days of the adventure
            var made = Again(pk, la, pk.MetDate!.Value);
            caught.Charm = true;
            if (made is null) { Problems.Add($"{Name(pk)} (파티): 다시 뽑지 못함"); continue; }
            if (!Same(pk, made, true, out var what) || made.MetDate != pk.MetDate) { Problems.Add($"{Name(pk)} (파티): 바뀌면 안 되는 것이 바뀜 ({what})"); continue; }
            made.ResetPartyStats();
            made.RefreshChecksum();
            sav.SetPartySlotAtIndex(made, i, EntityImportSettings.None);
            Done[Kind.Plain]++;
        }
        for (int b = 0; b < sav.BoxCount; b++)
        {
            var data = sav.GetBoxData(b);
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i].Species == 0) continue;
                var pk = (PK7)data[i]; var la = new LegalityAnalysis(pk);
                var kind = KindOf(pk, la);
                if (kind != Kind.Plain || only is not null && !only.Contains(pk.Species)) { Left[kind]++; continue; }
                PK7? made;
                try { made = Again(pk, la, pk.MetDate!.Value); }
                catch (Exception ex) { Problems.Add($"{Name(pk)}: {ex.Message}"); continue; }
                if (made is null) { Problems.Add($"{Name(pk)}: 다시 뽑지 못함"); continue; }
                if (!Same(pk, made, true, out var what)) { Problems.Add($"{Name(pk)}: 바뀌면 안 되는 것이 바뀜 ({what})"); continue; }
                if (made.MetDate != pk.MetDate || made.EggMetDate != pk.EggMetDate || made.ID32 != pk.ID32 || made.OriginalTrainerName != pk.OriginalTrainerName
                    || made.HandlingTrainerName != pk.HandlingTrainerName || made.IsShiny != pk.IsShiny)
                { Problems.Add($"{Name(pk)}: 날짜나 어버이나 색이 바뀜"); continue; }
                sav.SetBoxSlotAtIndex(made, b, i, EntityImportSettings.None);
                Done[Kind.Plain]++;
            }
        }
        return sav.Write().ToArray();
    }

    private Older older = null!;
    private string nexWanted = "";
    private readonly Dictionary<string, DateOnly> days = [];
    private readonly Dictionary<string, SimpleTrainerInfo> receivers = [];

    /// <summary>The day everything of one distribution was collected.</summary>
    private DateOnly DayOf(Event ev)
    {
        if (days.TryGetValue(ev.Group, out var known)) return known;
        var group = Events.All.Where(e => e.Group == ev.Group).ToList();
        var from = group.Max(e => e.From); var to = group.Min(e => e.To);
        // What this save received itself it received after the adventure had begun, and far enough in to have reached where it is handed over.
        if (group.Any(e => e.Album) && from < Began.AddDays(3)) from = Began.AddDays(3);
        if (to < from) throw new InvalidOperationException($"{ev.Group}: no day is left inside its window");
        return days[ev.Group] = draw.Day(from, to);
    }

    /// <summary>Whoever received a card: this save, the other version, or the same person's older game.</summary>
    private SimpleTrainerInfo Receiver(PK7 old)
    {
        if (old.Version == Me.Version && old.ConsoleRegion == Me.ConsoleRegion) return Me.Info;
        if (old.Version == Moon.Version && old.ConsoleRegion == Me.ConsoleRegion) return Moon.Info;
        string key = $"{old.Version}/{old.ConsoleRegion}/{old.Language}";
        if (receivers.TryGetValue(key, out var known)) return known;
        return receivers[key] = new SimpleTrainerInfo(old.Version)
        {
            OT = Me.Name, Gender = Me.Gender, TID16 = draw.Id16(), SID16 = draw.Id16(), Language = old.Language,
            ConsoleRegion = old.ConsoleRegion, Country = old.Country, Region = old.Region,
        };
    }

    /// <summary>The same card, received again: what the card leaves to chance is drawn as the card says, by PKHeX's own hand.</summary>
    private PK7? AgainFromCard(PK7 old, LegalityAnalysis la)
    {
        var card = (MysteryGift)la.EncounterMatch;
        var ev = Events.Find(card.Generation, card.CardID, old.Species, old.Form);
        bool named = old.OriginalTrainerName == was.Name; // the card gives it the receiver's own name
        DateOnly day;
        if (ev is not null) day = DayOf(ev);
        else if (named && card is WC7 { CardID: 0 }) day = draw.Day(opt.From, opt.To); // what the game itself hands over
        else throw new InvalidOperationException($"card #{card.CardID} is not in the list of distributions");

        var receiver = Receiver(old);
        bool shines = Shines(opt, old, card);
        for (int attempt = 0; attempt < 60; attempt++)
        {
            PKM first;
            try { first = card.ConvertToPKM(receiver, EncounterCriteria.Unrestricted with { Nature = draw.Nature() }); } catch { continue; }
            first.MetDate = day;
            var donor = first as PK7 ?? EntityConverter.ConvertToType(first, typeof(PK7), out _) as PK7;
            if (donor is null) continue;

            var pk = (PK7)old.Clone();
            if (named) { var owner = Owner(old); pk.OriginalTrainerName = owner.Name; pk.OriginalTrainerGender = owner.Gender; pk.TID16 = owner.Tid; pk.SID16 = owner.Sid; }
            if (pk.HandlingTrainerName == was.Name) { pk.HandlingTrainerName = Me.Name; pk.HandlingTrainerGender = Me.Gender; }
            pk.PID = donor.PID;
            pk.EncryptionConstant = donor.EncryptionConstant;
            for (int i = 0; i < 6; i++) pk.SetIV(i, donor.GetIV(i));
            pk.Nature = donor.Nature;
            pk.RefreshAbility(donor.AbilityNumber >> 1);
            pk.MetDate = day;
            if (card.Shiny == Shiny.Random)
            {
                // Left to chance by the card. What was shiny stays shiny: by the name on it, and not by whoever receives it - that one the game would have drawn again.
                pk.PID = shines ? draw.ShinyPid(pk.TID16, pk.SID16) : draw.PlainPid(pk.TID16, pk.SID16);
                if (ShinyUtil.GetIsShiny6(receiver.ID32, pk.PID) && receiver.ID32 != pk.ID32) continue;
            }
            pk.RefreshChecksum();

            var now = new LegalityAnalysis(pk);
            if (!now.Valid || now.EncounterMatch is not MysteryGift g) continue;
            if (g.CardID != card.CardID || g.OriginalTrainerName != card.OriginalTrainerName || g.Species != card.Species || g.Form != card.Form) continue;
            if (pk.IsShiny != shines) continue;
            return pk;
        }
        return null;
    }

    /// <summary>The album lists the cards this save received, each on the day it was received, in the order they came.</summary>
    private void Album()
    {
        var album = (MysteryBlock7)((IMysteryGiftStorageProvider)sav).MysteryGiftStorage;
        var cards = new List<WC7>();
        for (int i = 0; i < album.GiftCountMax; i++)
        {
            var c = (WC7)album.GetMysteryGift(i);
            if (c.IsEmpty) continue;
            var ev = Events.All.FirstOrDefault(e => e.Album && e.Generation == 7 && e.Card == c.CardID && e.Species == c.Species && e.Form == c.Form)
                     ?? throw new InvalidOperationException($"card #{c.CardID} in the album is not in the list of distributions");
            c.Date = DayOf(ev);
            cards.Add(c);
        }
        var ordered = cards.OrderBy(c => c.Date).ToList();
        for (int i = 0; i < album.GiftCountMax; i++) album.SetMysteryGift(i, i < ordered.Count ? ordered[i] : new WC7());
    }

    /// <summary>What may not differ between a Pokemon of the template and the one made in its place.</summary>
    /// <param name="ball">Whether the ball is among them: it is where each keeps the ball picked for it, and not where one ball was asked for.</param>
    /// <param name="sex">Whether the sex is among them: it is where the Pokemon is not free to be of another, or is asked to stay as it was.</param>
    private static bool Same(PK7 a, PK7 b, bool ball, out string what, bool sex = true)
    {
        var diff = new List<string>();
        void C(string n, object x, object y) { if (!Equals(x, y)) diff.Add($"{n} {x}→{y}"); }
        C("species", a.Species, b.Species); C("form", a.Form, b.Form); C("level", a.CurrentLevel, b.CurrentLevel); C("met level", a.MetLevel, b.MetLevel);
        if (ball) C("ball", SexBalls.Expected(a, b.Gender), (int)b.Ball);
        if (sex) C("gender", a.Gender, b.Gender);
        C("language", a.Language, b.Language); C("version", a.Version, b.Version);
        C("move1", a.Move1, b.Move1); C("move2", a.Move2, b.Move2); C("move3", a.Move3, b.Move3); C("move4", a.Move4, b.Move4);
        C("relearn1", a.RelearnMove1, b.RelearnMove1); C("relearn2", a.RelearnMove2, b.RelearnMove2);
        C("met place", a.MetLocation, b.MetLocation); C("egg place", a.EggLocation, b.EggLocation);
        C("handler", a.CurrentHandler, b.CurrentHandler); C("item", a.HeldItem, b.HeldItem); C("fateful", a.FatefulEncounter, b.FatefulEncounter);
        what = string.Join(", ", diff);
        return diff.Count == 0;
    }

    private string Name(PKM pk) => ko.Species[pk.Species] + (pk.Form != 0 ? $"-{pk.Form}" : "");

    private static Kind KindOf(PKM pk, LegalityAnalysis la) => la.EncounterMatch switch
    {
        PGT { IsManaphyEgg: true } => Kind.Older,
        MysteryGift => Kind.Card,
        _ when pk.Generation < 7 => Kind.Older,
        _ => Kind.Plain,
    };

    /// <summary>Whoever a Pokemon of the template belongs to, as they are in the new save.</summary>
    private Trainer Owner(PK7 pk)
    {
        if (IsHis(pk, was)) return Me;
        if (others.TryGetValue(pk.ID32, out var known)) return known;
        if (keepCast)
            return others[pk.ID32] = new Trainer(pk.OriginalTrainerName, pk.OriginalTrainerGender, pk.TID16, pk.SID16, pk.Language, pk.Version, pk.ConsoleRegion, pk.Country, pk.Region);
        // A friend: the same name and place, a game of their own.
        var friend = new Trainer(ForeignNames.Of(opt, pk.OriginalTrainerName), pk.OriginalTrainerGender, draw.Id16(), draw.Id16(), pk.Language, pk.Version, pk.ConsoleRegion, pk.Country, pk.Region);
        return others[pk.ID32] = friend;
    }

    /// <summary>The same Pokemon, met standing still or handed over as before, in a session of the game that could have been played.</summary>
    private PK7? AgainStill(PK7 old, LegalityAnalysis la, DateOnly day, Met7 met)
    {
        var enc = la.EncounterMatch;
        var owner = Owner(old);
        bool mine = ReferenceEquals(owner, Me);
        bool shines = Shines(opt, old, enc);
        for (int attempt = 0; attempt < 8; attempt++)
        {
            if (caught.Still(met, owner.Tid, owner.Sid, shines, SexOf(old, enc), used) is not var (drawn, seed)) return null;
            var pk = (PK7)old.Clone();
            pk.OriginalTrainerName = owner.Name; pk.OriginalTrainerGender = owner.Gender;
            pk.TID16 = owner.Tid; pk.SID16 = owner.Sid;
            if (pk.HandlingTrainerName == was.Name) { pk.HandlingTrainerName = Me.Name; pk.HandlingTrainerGender = Me.Gender; }
            Caught7.Put(pk, drawn);
            pk.MetDate = day;
            pk.RefreshChecksum();

            var now = new LegalityAnalysis(pk);
            if (!now.Valid) continue;
            var e = now.EncounterMatch;
            if (e.GetType() != enc.GetType() || e.Species != enc.Species || e.Form != enc.Form || e.LevelMin != enc.LevelMin || e.Version != enc.Version) continue;
            if (pk.IsShiny != shines || pk.CurrentHandler != old.CurrentHandler) continue;
            if (mine && pk.CurrentHandler != 0) continue;
            used.Add(pk.PID); used.Add(pk.EncryptionConstant);
            caught.Notes.Add($"{Name(old)}: {(old.Version == GameVersion.UM ? "울트라문" : "울트라썬")} {owner.Name} {owner.Tid:00000}/{owner.Sid:00000} — 시드 {seed:X8}, {drawn.Index}번째 수에서 버튼, 그 뒤 {drawn.Used}개를 지나 생성; 곁의 캐릭터 {met.Npc}, 빛나는부적 {(caught.Charm ? "있음" : "없음")}");
            Origins.Add(new StillOrigin(Name(old), pk.EncryptionConstant, owner.Tid, owner.Sid, met, seed, drawn.Index, caught.Charm));
            return pk;
        }
        return null;
    }

    /// <summary>
    /// The same Pokemon, hatched as before, of an egg the Nursery could have handed over. Its ball is settled first, since what ability it may
    /// have hangs on the ball: the one asked for where the species can be in it, a Poke Ball where it cannot, or the one picked for it.
    /// </summary>
    private PK7? AgainEgg(PK7 old, LegalityAnalysis la, DateOnly day)
    {
        var enc = la.EncounterMatch;
        var owner = Owner(old);
        bool mine = ReferenceEquals(owner, Me);
        bool shines = Shines(opt, old, enc);
        int had = old.AbilityNumber switch { 1 => 0, 2 => 1, _ => 2 };
        // Shedinja has no sex, whatever the Nincada it came of had: either will do, and it is left with none.
        bool shed = old.Gender == 2 && PersonalTable.USUM.GetFormEntry(enc.Species, enc.Form).Gender != 0xFF;
        var sex = shed ? null : SexOf(old, enc);
        var balls = opt.Ball is { } want ? new[] { want, (int)Ball.Poke }.Distinct().ToArray() : [old.Ball];
        int tries = opt.Ivs == IvChoice.Six ? (shines ? 400000 : 4000) : shines ? 40000 : 400;

        foreach (int ball in balls)
        {
            // The parent that passes its ability on has the ability this one had; where that will not do in the ball, an ordinary one.
            foreach (int ability in new[] { had, 0, 1 }.Distinct())
            {
                var parents = nursery.Pair(enc.Species, enc.Form, ability);
                PK7? made = null;
                bool Stands(Hatched7 egg)
                {
                    if (used.Contains(egg.Pid) || used.Contains(egg.Ec)) return false;
                    var pk = (PK7)old.Clone();
                    pk.OriginalTrainerName = owner.Name; pk.OriginalTrainerGender = owner.Gender;
                    pk.TID16 = owner.Tid; pk.SID16 = owner.Sid;
                    if (pk.HandlingTrainerName == was.Name) { pk.HandlingTrainerName = Me.Name; pk.HandlingTrainerGender = Me.Gender; }
                    Nursery7.Put(pk, egg);
                    if (shed) pk.Gender = old.Gender;
                    pk.Ball = (byte)(opt.Ball is null ? SexBalls.Expected(old, pk.Gender) : ball);
                    pk.MetDate = day;
                    if (old.EggMetDate is not null) pk.EggMetDate = day;
                    pk.RefreshChecksum();
                    var now = new LegalityAnalysis(pk);
                    if (!now.Valid) return false;
                    var e = now.EncounterMatch;
                    if (e.GetType() != enc.GetType() || e.Species != enc.Species || e.Form != enc.Form || e.LevelMin != enc.LevelMin || e.Version != enc.Version) return false;
                    if (pk.IsShiny != shines || (shed ? pk.Gender != old.Gender : sex is { } s && pk.Gender != s) || pk.CurrentHandler != old.CurrentHandler) return false;
                    if (mine && pk.CurrentHandler != 0) return false;
                    made = pk;
                    return true;
                }
                // A ball or an ability that will not do shows at once: every egg is turned down. A few are enough to tell.
                var probe = nursery.Lay(parents, owner.Tid, owner.Sid, shines, sex, Stands, tries);
                if (probe is null || made is null) continue;
                used.Add(made.PID); used.Add(made.EncryptionConstant);
                if (opt.Ball is not null && made.Ball != old.Ball && ability != had) AbilityGivenUp++;
                Origins.Add(new EggOrigin(Name(old), made.EncryptionConstant, owner.Tid, owner.Sid, probe.Seed, parents));
                nursery.Notes.Add($"{Name(old)}: 알 시드 {string.Join(",", probe.Seed.Select(x => x.ToString("X8")))} — {owner.Name} {owner.Tid:00000}/{owner.Sid:00000}; " +
                    $"부모 개체값 수 {string.Join("/", parents.MaleIvs)} 암 {string.Join("/", parents.FemaleIvs)}, 도구 수 {(int)parents.MaleHolds} 암 {(int)parents.FemaleHolds}, 물려주는 특성 {parents.Ability}, " +
                    $"성비 {parents.Ratio}, 둘 중 하나 {(parents.EitherSex ? 1 : 0)}, 암컷이 메타몽 {(parents.FemaleIsDitto ? 1 : 0)}");
                return made;
            }
        }
        return null;
    }

    /// <summary>The same Pokemon, come out of the same grass as before, in a session of the game that could have been played.</summary>
    private PK7? AgainGrass(PK7 old, LegalityAnalysis la, DateOnly day)
    {
        var enc = la.EncounterMatch;
        bool moon = old.Version == GameVersion.UM;
        int speciesForm = enc.Species + (enc.Form << 11);
        byte level = old.MetLevel;
        var beast = Caught7.Beasts.FirstOrDefault(b => b.Species == enc.Species && b.Version == old.Version && b.Location == old.MetLocation);
        bool isBeast = beast.Species != 0;
        if (enc is not EncounterSlot7 && !isBeast) return null;

        // The patch of grass: where it was met, holding the species at the level it was met at. Where the tool's patch holds the species
        // but not the level, the levels are PKHeX's, which has them from the game itself: the game has more patches than the tool lists.
        var here = Area7.All.Where(a => a.Location == old.MetLocation).ToList();
        Area7? area; (byte Min, byte Max)? levels = null; string about = "";
        if (isBeast) area = here.FirstOrDefault();
        else
        {
            var holding = here.Where(a => new[] { false, true }.Any(night => a.Slots(moon, night)?.Contains(speciesForm) == true)).ToList();
            area = holding.FirstOrDefault(a => a.Levels(moon).Min <= level && level <= a.Levels(moon).Max);
            if (area is null && holding.Count != 0)
            {
                area = holding[0];
                levels = (enc.LevelMin, enc.LevelMax);
                about = $"; 레벨 범위 {enc.LevelMin}~{enc.LevelMax} 는 PKHeX 의 표에서 (도구의 표에는 {area.Levels(moon).Min}~{area.Levels(moon).Max} 만 있음)";
            }
        }
        if (area is null) return null;

        var owner = Owner(old);
        bool mine = ReferenceEquals(owner, Me);
        bool shines = Shines(opt, old, enc);
        for (int attempt = 0; attempt < 8; attempt++)
        {
            var got = caught.Grass(area, moon, speciesForm, level, levels, owner.Tid, owner.Sid, shines, SexOf(old, enc), used, isBeast ? (beast.Level, beast.Rate) : null);
            if (got is not var (drawn, seed, night)) return null;
            var pk = (PK7)old.Clone();
            pk.OriginalTrainerName = owner.Name; pk.OriginalTrainerGender = owner.Gender;
            pk.TID16 = owner.Tid; pk.SID16 = owner.Sid;
            if (pk.HandlingTrainerName == was.Name) { pk.HandlingTrainerName = Me.Name; pk.HandlingTrainerGender = Me.Gender; }
            Caught7.Put(pk, drawn);
            pk.MetDate = day;
            pk.RefreshChecksum();

            var now = new LegalityAnalysis(pk);
            if (!now.Valid) continue;
            var e = now.EncounterMatch;
            if (e.GetType() != enc.GetType() || e.Species != enc.Species || e.Form != enc.Form || e.Version != enc.Version) continue;
            if (pk.IsShiny != shines || pk.CurrentHandler != old.CurrentHandler) continue;
            if (mine && pk.CurrentHandler != 0) continue;
            used.Add(pk.PID); used.Add(pk.EncryptionConstant);
            Origins.Add(new GrassOrigin(Name(old), pk.EncryptionConstant, owner.Tid, owner.Sid, area, moon, night, speciesForm, level, levels, isBeast ? (beast.Level, beast.Rate) : null, seed, drawn.Index, caught.Charm));
            caught.Notes.Add($"{Name(old)}: {(moon ? "울트라문" : "울트라썬")} {owner.Name} {owner.Tid:00000}/{owner.Sid:00000} — 풀숲(꿀) 장소 {area.Location}{(area.Index != 0 ? $"-{area.Index}" : "")} {(night ? "밤" : "낮")}, 시드 {seed:X8}, {drawn.Index}번째 수에서 버튼, 그 뒤 {drawn.Used}개를 지나 생성; {(isBeast ? "울트라비스트" : $"{drawn.Slot}번 칸")} Lv{drawn.Level}, 곁의 캐릭터 {area.Npc}, 빛나는부적 {(caught.Charm ? "있음" : "없음")}{about}");
            return pk;
        }
        return null;
    }

    /// <summary>The same Pokemon, met the same way, as somebody else's luck would have had it.</summary>
    private PK7? Again(PK7 old, LegalityAnalysis la, DateOnly day)
    {
        if (day == default) return old; // its day hangs on another's; it is drawn once that one is known
        var enc = la.EncounterMatch;
        if (enc is not IEncounterEgg)
        {
            if (enc is EncounterStatic7 && Met7.Find(enc.Species, enc.Form, old.Version, enc.LevelMin) is { } met) return AgainStill(old, la, day, met);
            if (enc is EncounterSlot7 or EncounterStatic7 && AgainGrass(old, la, day) is { } grown) return grown;
        }
        else if (enc is EncounterEgg7) return AgainEgg(old, la, day);
        // Nothing is drawn but from a state the game could be in: what no session is known for is not made at all.
        return null;
    }
}
