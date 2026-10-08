namespace SpineViewer.Core;

public sealed record AtlasPage(string Name, IReadOnlyList<string> Regions);
public sealed record AtlasCatalog(IReadOnlyList<AtlasPage> Pages);

public static class SpineAtlasCatalog
{
    // Atlas page sections start with a file name and page metadata (size, format, filter, repeat, pma).
    // Region blocks follow; an empty line separates pages in standard Spine atlas exports.
    public static AtlasCatalog Read(string text)
    {
        var pages = new List<AtlasPage>();
        var lines = text.Replace("\r", "").Split('\n');
        var i = 0;
        while (i < lines.Length)
        {
            while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i])) i++;
            if (i >= lines.Length) break;
            var name = lines[i++].Trim();
            if (!LooksLikeImage(name)) throw new FormatException($"Página atlas inválida: {name}");
            var regions = new List<string>();
            while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
            {
                var line = lines[i++].Trim();
                if (line.Contains(':')) continue;
                regions.Add(line);
            }
            pages.Add(new AtlasPage(name, regions));
        }
        if (pages.Count == 0) throw new FormatException("Atlas vazio.");
        return new AtlasCatalog(pages);
    }
    private static bool LooksLikeImage(string value) => value.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
        || value.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
        || value.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
        || value.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);
}
