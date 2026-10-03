namespace AlolaDexMaker.Sword;

/// <summary>What the Sword maker carries inside: the template save and its sidecar files, the ball table, the trackers.</summary>
public static class Resources
{
    public static byte[] Bytes(string name)
    {
        using var stream = typeof(Resources).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException($"no {name} inside");
        using var buffer = new MemoryStream(); stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    public static IEnumerable<string> Lines(string name) => System.Text.Encoding.UTF8.GetString(Bytes(name)).Split('\n');
}
