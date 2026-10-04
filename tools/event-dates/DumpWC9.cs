using System.Text;
using PKHeX.Core;
// The ninth generation's gift cards as a catalogue in the event box's layout, with the names tables the matcher reads.
var ko = GameInfo.GetStrings("ko"); var en = GameInfo.GetStrings("en"); var ja = GameInfo.GetStrings("ja");
var dir = "/home/elyss/storage/sv-dex/events/";
static string GenderName(int g) => g switch { 0 => "수", 1 => "암", 2 => "무", _ => "랜덤" };
static string AbilityName(AbilityPermission a) => a switch { AbilityPermission.OnlyFirst => "1", AbilityPermission.OnlySecond => "2", AbilityPermission.OnlyHidden => "숨", AbilityPermission.Any12 => "1~2", _ => "랜덤" };
var rows = new List<string[]>();
var cards = EncounterEvent.MGDB_G9.OfType<WC9>().Where(c => c.IsEntity && c.Species != 0).ToList();
int i = 0;
foreach (var c in cards)
{
    bool sw = c.Version is GameVersion.SV or GameVersion.SL, sh = c.Version is GameVersion.SV or GameVersion.VL;
    string ver = sw && sh ? "스칼렛/바이올렛" : sw ? "스칼렛" : sh ? "바이올렛" : "없음";
    string extra = (c.IsHOMEGift ? "HOME 선물" : "") + (ver != "스칼렛/바이올렛" ? (c.IsHOMEGift ? ", " : "") + ver : "");
    string otKo = c.GetOT(8), otEn = c.GetOT(2), otJa = c.GetOT(1);
    rows.Add(["9", "WC9", c.CardTitle, ko.specieslist[c.Species], c.Form.ToString(), c.Shiny.ToString(), c.Level.ToString(), otEn.Length > 0 ? otEn : otKo, c.TID16.ToString(), c.SID16.ToString(), "", ko.balllist[(int)c.Ball], c.IsEgg ? "알" : "",
        string.Join("/", new[] { c.Move1, c.Move2, c.Move3, c.Move4 }.Where(m => m != 0).Select(m => ko.movelist[m])), c.HeldItem != 0 ? ko.itemlist[c.HeldItem] : "", extra, $"{i}:{c.CardID}", GenderName(c.Gender),
        (int)c.Nature >= 25 ? "랜덤" : ko.natures[(int)c.Nature], AbilityName(c.Ability), c.IsNicknamed ? "닉네임" : "", "게임", otKo, otJa, c.CardID.ToString()]);
    i++;
}
var sb = new StringBuilder("세대\t종류\t카드 제목\t포켓몬\t폼\t이로치\t레벨\t어버이\tTID\tSID\t지역\t볼\t알\t기술\t도구\t비고\t파일\t성별\t성격\t특성\t닉네임\t언어\t어버이(한)\t어버이(일)\t카드번호\n");
foreach (var r in rows) sb.Append(string.Join('\t', r)).Append('\n');
File.WriteAllText(dir + "카탈로그.tsv", sb.ToString(), new UTF8Encoding(true));
Console.WriteLine($"{rows.Count} cards; HOME {rows.Count(r => r[15].Contains("HOME"))}; not in Scarlet {rows.Count(r => r[15].Contains("바이올렛") && !r[15].Contains("스칼렛"))}");
// names tables for the matcher
var sp = new StringBuilder("번호\t한국어\t영어\t일본어\n"); for (ushort s = 1; s <= 1025; s++) sp.Append($"{s}\t{ko.specieslist[s]}\t{en.specieslist[s]}\t{ja.specieslist[s]}\n");
File.WriteAllText(dir + "sources/species.tsv", sp.ToString(), new UTF8Encoding(true));
var mv = new StringBuilder("번호\t한국어\t영어\n"); for (int m = 1; m < ko.movelist.Length; m++) if (ko.movelist[m].Length > 0) mv.Append($"{m}\t{ko.movelist[m]}\t{en.movelist[m]}\n");
File.WriteAllText(dir + "sources/moves.tsv", mv.ToString(), new UTF8Encoding(true));
var it = new StringBuilder("번호\t한국어\t영어\n"); for (int m = 1; m < ko.itemlist.Length; m++) if (ko.itemlist[m].Length > 0) it.Append($"{m}\t{ko.itemlist[m]}\t{en.itemlist[m]}\n");
File.WriteAllText(dir + "sources/items.tsv", it.ToString(), new UTF8Encoding(true));
var ab = new StringBuilder("번호\t한국어\t영어\n"); for (int m = 1; m < ko.abilitylist.Length; m++) if (ko.abilitylist[m].Length > 0) ab.Append($"{m}\t{ko.abilitylist[m]}\t{en.abilitylist[m]}\n");
File.WriteAllText(dir + "sources/abilities.tsv", ab.ToString(), new UTF8Encoding(true));
