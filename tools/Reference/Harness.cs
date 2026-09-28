using System.Text.Json;
using Pk3DSRNGTool;
using Pk3DSRNGTool.Core;
using Pk3DSRNGTool.RNG;

// The reference: 3DSRNGTool's own generation code, compiled as it is, driven the way its main window drives it
// (getsetting, getStaSettings and Search7_Normal of MainForm). What it prints is what the tool would list.
//
//   ref7 sta <resources> <species> <forme> <US|UM> <seed hex> <min> <max> <tsv> <trv> <charm 0|1>
Pk3DSRNGTool.Properties.Resources.Folder = args[1];
if (args[0] == "list")
{
    foreach (var group in PKM7.Species_USUM)
        foreach (var p in group.List.OfType<PKM7>())
            if (!p.Conceptual)
                Console.WriteLine(JsonSerializer.Serialize(new { group = group.Text, species = (int)p.Species, forme = (int)p.Forme, level = (int)p.Level, version = p.Version.ToString(), npc = (int)p.NPC, delay = (int)p.Delay, delayType = (int)p.DelayType,
                    gift = p.Gift, alwaysSync = p.AlwaysSync, syncable = p.Syncable, iv3 = p.IV3 || p.iv3, shinyLocked = p.ShinyLocked, ability = (int)p.Ability, nature = (int)p.Nature, settingGender = (int)p.SettingGender, randomGender = p.IsRandomGender,
                    ivs = p.IVs, ottsv = p.OTTSV, pelago = p.IsPelago, raining = p.Raining, totem = p.Totem, ultraWormhole = p.UltraWormhole, egg = p.Egg, unstable = p.Unstable }));
    return 0;
}
if (args[0] == "sta")
{
    int species = int.Parse(args[2]), forme = int.Parse(args[3]); string version = args[4];
    uint seed = Convert.ToUInt32(args[5], 16); int min = int.Parse(args[6]), max = int.Parse(args[7]);
    int tsv = int.Parse(args[8]); byte trv = byte.Parse(args[9]); bool charm = args[10] == "1";
    string wantGroup = args.Length > 11 ? args[11] : null;
    var ver = version == "US" ? GameVersion.US : GameVersion.UM;
    var pm = PKM7.Species_USUM.Where(g => wantGroup is null ? !g.Text.Contains("First Encounter") : g.Text == wantGroup).SelectMany(g => g.List).OfType<PKM7>()
        .First(p => !p.Conceptual && p.Species == species && p.Forme == forme && p.Version.Contains(ver));

    // MainForm: the template fills the window's fields
    int timedelay = pm.Delay;            // Timedelay.Value = FormPM.Delay
    byte modelnum = (byte)(pm.NPC + 1);  // Modelnum => NPC.Value + 1
    bool raining = pm.Raining;

    // getStaSettings
    var setting = new Stationary7();
    setting.Synchro_Stat = unchecked((byte)(0 - 1)); // no Synchronize lead chosen
    setting.TSV = tsv; setting.TRV = trv; setting.Level = pm.Level; setting.ShinyCharm = charm;
    RNGPool.Clear();
    RNGPool.PM = pm;
    setting.UseTemplate(pm);

    // getsetting
    var sfmt = new SFMT(seed);
    for (int i = 0; i < min; i++) sfmt.Next();
    var status = new ModelStatus(modelnum, sfmt); var stmp = new ModelStatus(modelnum, sfmt);
    status.raining = stmp.raining = raining;
    RNGPool.igenerator = setting;
    int buffersize = 150;
    RNGPool.modelnumber = modelnum;
    RNGPool.DelayTime = timedelay / 2 + 2;
    RNGPool.raining = raining;
    if (RNGPool.DelayType == 4 && (timedelay & 1) == 1) RNGPool.DelayType = 6;
    RNGPool.Considerdelay = true;
    buffersize += RNGPool.modelnumber * RNGPool.DelayTime;
    if (pm.DelayType > 0) buffersize += 3 * RNGPool.DelayTime;
    RNGPool.CreateBuffer(sfmt, buffersize);

    // Search7_Normal
    int frameadvance;
    for (int i = min; i <= max;)
    {
        do { frameadvance = status.NextState(); } while (frameadvance == 0);
        bool first = true;
        do
        {
            RNGPool.CopyStatus(stmp);
            var r = (Result7)RNGPool.Generate7();
            RNGPool.AddNext(sfmt);
            frameadvance--; i++;
            if (i > max + 1) continue;
            Console.WriteLine(JsonSerializer.Serialize(new { frame = i - 1, first, ec = r.EC, pid = r.PID, ivs = r.IVs, ability = (int)r.Ability, nature = (int)r.Nature, gender = (int)r.Gender, shiny = r.Shiny, sync = r.Synchronize, used = r.FrameDelayUsed }));
            first = false;
        }
        while (frameadvance > 0);
        status.CopyTo(stmp);
    }
    return 0;
}
if (args[0] == "areas")
{
    foreach (var ea in LocationTable7.USUMTable)
        Console.WriteLine(JsonSerializer.Serialize(new { location = (int)ea.Location, idx = (int)ea.idx, npc = (int)ea.NPC, correction = (int)ea.Correction, levelMin = (int)ea.LevelMin, levelMax = (int)ea.LevelMax, lvldiff = (int)ea.lvldiff,
            raining = ea.Raining, reverse = ea.Reverse, versionDifference = ea.VersionDifference, dayNightDifference = ea.DayNightDifference, raw = ea.Species,
            usDay = ea.getSpecies(7, false), usNight = ea.getSpecies(7, true), umDay = ea.getSpecies(8, false), umNight = ea.getSpecies(8, true) }));
    Console.WriteLine(JsonSerializer.Serialize(new { slotTypes = EncounterArea7.SlotType, distributions = WildRNG.SlotDistribution }));
    return 0;
}
if (args[0] == "wild")
{
    //   ref7 wild <resources> <location> <idx> <US|UM> <night 0|1> <ub species or 0> <seed hex> <min> <max> <tsv> <trv> <charm 0|1>
    int location = int.Parse(args[2]), idx = int.Parse(args[3]); int ver = args[4] == "US" ? 7 : 8; bool night = args[5] == "1";
    int ub = int.Parse(args[6]);
    uint seed = Convert.ToUInt32(args[7], 16); int min = int.Parse(args[8]), max = int.Parse(args[9]);
    int tsv = int.Parse(args[10]); byte trv = byte.Parse(args[11]); bool charm = args[12] == "1";
    var ea = LocationTable7.USUMTable.First(t => t.Location == location && t.idx == idx);
    var slotspecies = ea.getSpecies(ver, night);
    PKMW7 pm = ub == 0 ? (PKMW7)PKMW7.Species_USUM[0].List[0] : PKMW7.Species_USUM[1].List.OfType<PKMW7>().First(p => p.Species == ub);

    // MainForm: the area and the template fill the window's fields
    byte modelnum = (byte)(ea.NPC + 1);
    int correction = ea.Correction;
    bool raining = ea.Raining;
    int special = ub == 0 ? 0 : pm.Rate[Array.IndexOf(pm.Location, location)];
    bool moon = ea.VersionDifference && ver == 8;
    byte lvmin = moon ? ea.LevelMinMoon : ea.LevelMin, lvmax = moon ? ea.LevelMaxMoon : ea.LevelMax;
    int timedelay = pm.Delay;

    // getWildSetting
    RNGPool.Clear();
    RNGPool.PM = pm;
    var setting = new Wild7();
    setting.Synchro_Stat = unchecked((byte)(0 - 1));
    setting.TSV = tsv; setting.TRV = trv; setting.ShinyCharm = charm;
    setting.Levelmin = lvmin; setting.Levelmax = lvmax;
    setting.SpecialEnctr = (byte)special;
    setting.UB = ub != 0;
    setting.CompoundEye = false;
    int slottype = 0;
    RNGPool.DelayType = 0;
    setting.SpecForm = new int[11];
    if (ea.Locationidx == 1190) slottype = 1;
    for (int i = 1; i < 11; i++) setting.SpecForm[i] = slotspecies[EncounterArea7.SlotType[slotspecies[0]][i - 1]];
    if (setting.SpecialEnctr > 0) { setting.SpecForm[0] = pm.SpecForm; setting.SpecialLevel = pm.Level; }
    setting.Markslots();
    setting.SlotSplitter = WildRNG.SlotDistribution[slottype];

    // getsetting
    var sfmt = new SFMT(seed);
    for (int i = 0; i < min; i++) sfmt.Next();
    var status = new ModelStatus(modelnum, sfmt); var stmp = new ModelStatus(modelnum, sfmt);
    status.raining = stmp.raining = raining;
    RNGPool.igenerator = setting;
    int buffersize = 150;
    RNGPool.modelnumber = modelnum;
    RNGPool.DelayTime = timedelay / 2 + 2;
    RNGPool.raining = raining;
    RNGPool.PreHoneyCorrection = correction;
    RNGPool.HoneyDelay = 63;
    RNGPool.ultrawild = true;
    buffersize += RNGPool.modelnumber * 500;
    RNGPool.Considerdelay = true;
    buffersize += RNGPool.modelnumber * RNGPool.DelayTime;
    RNGPool.CreateBuffer(sfmt, buffersize);

    int frameadvance;
    for (int i = min; i <= max;)
    {
        do { frameadvance = status.NextState(); } while (frameadvance == 0);
        bool first = true;
        do
        {
            RNGPool.CopyStatus(stmp);
            var r = (ResultW7)RNGPool.Generate7();
            RNGPool.AddNext(sfmt);
            frameadvance--; i++;
            if (i > max + 1) continue;
            Console.WriteLine(JsonSerializer.Serialize(new { frame = i - 1, first, special = r.IsSpecial, slot = (int)r.Slot, species = (int)r.Species, level = (int)r.Level, ec = r.EC, pid = r.PID, ivs = r.IVs, ability = (int)r.Ability, nature = (int)r.Nature, gender = (int)r.Gender, shiny = r.Shiny, sync = r.Synchronize, item = (int)r.Item, used = r.FrameDelayUsed }));
            first = false;
        }
        while (frameadvance > 0);
        status.CopyTo(stmp);
    }
    return 0;
}
if (args[0] == "eggs7")
{
    // Many eggs at once: each line of the file is the arguments of egg7 after the resources, with the first number only.
    foreach (var line in File.ReadAllLines(args[2]))
    {
        var a = line.Split(' ');
        var status = a[0].Split(',').Select(x => Convert.ToUInt32(x, 16)).ToArray();
        var setting = new Egg7();
        setting.TSV = ushort.Parse(a[1]); setting.TRV = byte.Parse(a[2]);
        setting.ShinyCharm = a[3] == "1"; setting.MMethod = a[4] == "1";
        setting.Gender = FuncUtil.getGenderRatio(int.Parse(a[5]));
        setting.NidoType = a[6] == "1";
        setting.MaleItem = byte.Parse(a[7]); setting.FemaleItem = byte.Parse(a[8]);
        setting.MaleIVs = a[9].Split('/').Select(int.Parse).ToArray();
        setting.FemaleIVs = a[10].Split('/').Select(int.Parse).ToArray();
        setting.InheritAbility = byte.Parse(a[11]);
        setting.Homogeneous = a[12] == "1";
        setting.FemaleIsDitto = a[13] == "1";
        setting.ConsiderOtherTSV = false; setting.OtherTSVs = new int[0];
        setting.MarkItem();
        RNGPool.Clear();
        var rng = new TinyMT(status);
        RNGPool.igenerator = setting;
        RNGPool.CreateBuffer(rng, 100);
        var r = (EggResult)RNGPool.GenerateEgg7();
        Console.WriteLine(JsonSerializer.Serialize(new { gender = (int)r.Gender, nature = (int)r.Nature, ability = (int)r.Ability, ivs = r.IVs, ec = r.EC, pid = r.PID, shiny = r.Shiny, used = r.FramesUsed }));
    }
    return 0;
}
if (args[0] == "egg7")
{
    // An egg of Sun, Moon, Ultra Sun or Ultra Moon, as Search7_Egg lists it.
    //   ref7 egg7 <resources> <s0,s1,s2,s3 hex> <min> <max> <tsv> <trv> <charm 0|1> <masuda 0|1> <gender ratio> <nido 0|1>
    //             <male item> <female item> <male ivs a/b/c/d/e/f> <female ivs> <ability of the parent that passes it 0|1|2> <same species 0|1> <female is ditto 0|1>
    var status = args[2].Split(',').Select(x => Convert.ToUInt32(x, 16)).ToArray();
    int min = int.Parse(args[3]), max = int.Parse(args[4]);
    var setting = new Egg7();
    setting.TSV = ushort.Parse(args[5]); setting.TRV = byte.Parse(args[6]);
    setting.ShinyCharm = args[7] == "1"; setting.MMethod = args[8] == "1";
    setting.Gender = FuncUtil.getGenderRatio(int.Parse(args[9]));
    setting.NidoType = args[10] == "1";
    setting.MaleItem = byte.Parse(args[11]); setting.FemaleItem = byte.Parse(args[12]);
    setting.MaleIVs = args[13].Split('/').Select(int.Parse).ToArray();
    setting.FemaleIVs = args[14].Split('/').Select(int.Parse).ToArray();
    setting.InheritAbility = byte.Parse(args[15]);
    setting.Homogeneous = args[16] == "1";
    setting.FemaleIsDitto = args[17] == "1";
    setting.ConsiderOtherTSV = false; setting.OtherTSVs = new int[0];
    setting.MarkItem();

    RNGPool.Clear();
    var rng = new TinyMT(status);
    for (int i = 0; i < min; i++) rng.Next();
    RNGPool.igenerator = setting;
    RNGPool.CreateBuffer(rng, 100);
    for (int i = min; i <= max; i++, RNGPool.AddNext(rng))
    {
        var r = (EggResult)RNGPool.GenerateEgg7();
        Console.WriteLine(JsonSerializer.Serialize(new { frame = i, gender = (int)r.Gender, nature = (int)r.Nature, ability = (int)r.Ability, ivs = r.IVs, from = r.InheritMaleIV.Select(x => x is null ? 0 : x == true ? 1 : 2).ToArray(),
            ec = r.EC, pid = r.PID, shiny = r.Shiny, ball = (int)r.Ball, used = r.FramesUsed }));
    }
    return 0;
}
return 2;
