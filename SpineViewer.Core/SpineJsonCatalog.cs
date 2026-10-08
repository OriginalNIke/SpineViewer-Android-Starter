using System.Text.Json;
namespace SpineViewer.Core;
public sealed record SpineCatalog(string Name, IReadOnlyList<string> Skins, IReadOnlyList<string> Animations);
public static class SpineJsonCatalog
{
    public static SpineCatalog Read(string json, string name)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var skins = new List<string>();
        if (root.TryGetProperty("skins", out var s))
        {
            if (s.ValueKind == JsonValueKind.Array)
                foreach (var entry in s.EnumerateArray())
                    if (entry.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String) skins.Add(n.GetString()!);
            else if (s.ValueKind == JsonValueKind.Object)
                skins.AddRange(s.EnumerateObject().Select(x => x.Name));
        }
        var animations = new List<string>();
        if (root.TryGetProperty("animations", out var a) && a.ValueKind == JsonValueKind.Object)
            animations.AddRange(a.EnumerateObject().Select(x => x.Name));
        return new SpineCatalog(name, skins, animations);
    }
}
