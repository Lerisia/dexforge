namespace Dexforge;

/// <summary>A distribution: which card, in which game it was received, and the days it could be had.</summary>
/// <param name="Group">What was handed out together is collected on one day.</param>
/// <param name="Album">Whether the card sits in this save's album (only this generation's cards, received by this save itself).</param>
public sealed record Event(int Generation, int Card, ushort Species, byte Form, string Group, DateOnly From, DateOnly To, bool Album = false);

/// <summary>
/// The distributions the template holds, as they were given out to consoles of the Korean region
/// (and, where Korea had none, as they could be had on a visit or from somebody abroad).
/// The days are from Bulbapedia's lists of event distributions, checked one by one when each was added.
/// </summary>
public static class Events
{
    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    public static readonly Event[] All =
    [
        new(5, 70,   648, 0, "movie15-meloetta", D(2012, 12, 19), D(2013, 2, 28)),
        new(5, 136,  649, 0, "movie16-genesect", D(2014, 1, 9),   D(2014, 1, 15)),
        new(6, 1503, 666, 18, "gts-fancy-vivillon", D(2014, 7, 8), D(2014, 7, 31)),
        // A visit to a Pokemon Center in Japan in August 2014: both were handed out at the counter that month.
        new(6, 34,   385, 0, "japan-2014-08", D(2014, 8, 1),  D(2014, 8, 31)),
        new(6, 36,   666, 19, "japan-2014-08", D(2014, 8, 1), D(2014, 8, 31)),
        new(6, 81,   493, 0, "movie18-arceus", D(2015, 3, 7),  D(2015, 8, 31)),
        new(6, 1023, 647, 0, "guide-keldeo",   D(2015, 2, 25), D(2016, 1, 31)),
        new(6, 1054, 720, 0, "movie18-hoopa",  D(2015, 12, 15), D(2016, 2, 29)),
        new(6, 1060, 716, 0, "xyz", D(2016, 3, 22), D(2016, 6, 30)),
        new(6, 1061, 717, 0, "xyz", D(2016, 4, 22), D(2016, 6, 30)),
        new(6, 1064, 718, 0, "xyz", D(2016, 4, 20), D(2016, 6, 30)),
        new(6, 1071, 719, 0, "all-star-diancie", D(2016, 8, 13), D(2016, 8, 14)),
        new(6, 1076, 494, 0, "preorder-victini", D(2016, 10, 1), D(2016, 11, 30)),
        new(6, 1078, 721, 0, "movie19-volcanion", D(2016, 12, 22), D(2017, 2, 28)),
        new(7, 2046, 658, 1, "demo-greninja", D(2016, 11, 18), D(2017, 11, 16)),
        // The twentieth film: one card of codes, and the code the scanner reads.
        new(7, 1123, 802, 0, "movie20", D(2017, 12, 21), D(2018, 2, 28), Album: true),
        new(7, 1122, 25, 1,  "movie20", D(2017, 12, 21), D(2018, 2, 28), Album: true),
        new(7, 1131, 25, 2,  "movie20", D(2017, 12, 21), D(2018, 2, 28), Album: true),
        new(7, 1132, 25, 3,  "movie20", D(2017, 12, 21), D(2018, 2, 28), Album: true),
        new(7, 1133, 25, 4,  "movie20", D(2017, 12, 21), D(2018, 2, 28), Album: true),
        new(7, 1134, 25, 5,  "movie20", D(2017, 12, 21), D(2018, 2, 28), Album: true),
        new(7, 1135, 25, 6,  "movie20", D(2017, 12, 21), D(2018, 2, 28), Album: true),
        new(7, 0,    25, 7,  "movie20", D(2017, 12, 21), D(2018, 2, 28)),
        new(7, 1146, 490, 0, "summer-manaphy", D(2018, 7, 13), D(2018, 7, 22), Album: true),
        new(7, 1633, 786, 0, "pgl-tapu-lele", D(2018, 12, 18), D(2019, 1, 31), Album: true),
        new(7, 1148, 807, 0, "movie21-zeraora", D(2018, 12, 19), D(2019, 2, 28), Album: true),
        new(7, 1634, 787, 0, "pgl-tapu-bulu", D(2019, 3, 19), D(2019, 4, 29), Album: true),
        new(7, 1635, 788, 0, "pgl-tapu-fini", D(2019, 6, 11), D(2019, 7, 30), Album: true),
        new(7, 1637, 785, 0, "pgl-tapu-koko", D(2019, 10, 18), D(2019, 11, 30), Album: true),
        new(7, 1158, 792, 0, "eclipse", D(2019, 11, 15), D(2019, 12, 31), Album: true),
        new(7, 1158, 791, 0, "eclipse", D(2019, 11, 15), D(2019, 12, 31)),
        new(7, 1159, 800, 0, "eclipse", D(2019, 11, 15), D(2019, 12, 31), Album: true),
    ];

    public static Event? Find(int generation, int card, ushort species, byte form) =>
        All.FirstOrDefault(e => e.Generation == generation && e.Card == card && e.Species == species && e.Form == form);

    /// <summary>The last day the first of this save's own cards could still be had: the adventure has to have begun by then.</summary>
    public static DateOnly FirstAlbumDeadline => All.Where(e => e.Album).Min(e => e.To);
}
