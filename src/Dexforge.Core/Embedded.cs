namespace Dexforge;

/// <summary>What the program carries inside, by the logical name the project file gives it.</summary>
public static class Embedded
{
    public static byte[] Bytes(string name)
    {
        using var stream = typeof(Embedded).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException($"no {name} inside");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    public static string Text(string name) => System.Text.Encoding.UTF8.GetString(Bytes(name)).TrimStart('﻿');
}
