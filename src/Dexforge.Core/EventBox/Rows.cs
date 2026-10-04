namespace Dexforge.EventBox;

/// <summary>A tab-separated table with a header, read by column name.</summary>
public sealed class Rows
{
    public readonly string[] Header;
    public readonly List<string[]> All = [];
    private readonly Dictionary<string, int> index = [];

    public Rows(string text)
    {
        var lines = text.Split('\n');
        Header = lines[0].TrimStart('\uFEFF').TrimEnd('\r').Split('\t');
        for (int i = 0; i < Header.Length; i++) index[Header[i]] = i;
        foreach (var l in lines.Skip(1)) if (l.Trim('\r').Length > 0) All.Add(l.TrimEnd('\r').Split('\t'));
    }

    public string Get(string[] row, string column) => index.TryGetValue(column, out var i) && i < row.Length ? row[i] : "";

    /// <summary>The distribution list the program carries (배포목록.tsv of the catalogue, the columns the maker uses).</summary>
    public static Rows Distributions()
    {
        using var stream = typeof(Rows).Assembly.GetManifestResourceStream("events.distributions") ?? throw new InvalidOperationException("no distribution list inside");
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return new Rows(reader.ReadToEnd());
    }
}
