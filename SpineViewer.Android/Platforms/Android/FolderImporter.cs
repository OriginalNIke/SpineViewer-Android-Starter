using global::Android.Provider;
using Microsoft.Maui.ApplicationModel;

namespace SpineViewer.Android;

// Storage Access Framework: recursive tree traversal without broad storage permissions.
public static class FolderImporter
{
    // Name is the relative path, preserving duplicate file names in different folders.
    public sealed record Entry(string Name, byte[] Content)
    {
        public string FileName => Name[(Name.LastIndexOf('/') + 1)..];
        public string Directory => Name.Contains('/') ? Name[..Name.LastIndexOf('/')] : "";
    }

    public static async Task<IReadOnlyList<Entry>?> SelectAndReadAsync()
    {
        var tree = await MainActivity.SelectFolderAsync();
        if (tree is null) return null;
        return await Task.Run(() => ReadFolder(tree));
    }

    static IReadOnlyList<Entry> ReadFolder(global::Android.Net.Uri tree)
    {
        var resolver = Platform.CurrentActivity?.ContentResolver
            ?? throw new InvalidOperationException("ContentResolver indisponível.");
        var root = DocumentsContract.GetTreeDocumentId(tree)
            ?? throw new InvalidDataException("Pasta inválida.");
        var results = new List<Entry>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        const int maxDepth = 24;
        void Visit(string folderId, string prefix, int depth)
        {
            if (depth > maxDepth) throw new InvalidDataException("Limite de profundidade de subpastas excedido.");
            if (!visited.Add(folderId)) return;
            var children = DocumentsContract.BuildChildDocumentsUriUsingTree(tree, folderId);
            string[] projection = { DocumentsContract.Document.ColumnDocumentId,
                                    DocumentsContract.Document.ColumnDisplayName,
                                    DocumentsContract.Document.ColumnMimeType };
            var entries = new List<(string id, string name, string mime)>();
            using (var cursor = resolver.Query(children, projection, null, null, null)
                ?? throw new IOException($"Não foi possível listar a pasta {prefix}."))
            {
                while (cursor.MoveToNext())
                    entries.Add((cursor.GetString(0) ?? "", cursor.GetString(1) ?? "", cursor.GetString(2) ?? ""));
            }
            foreach (var (id, name, mime) in entries.OrderBy(x => x.name, StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) continue;
                string path = prefix + name;
                if (mime == DocumentsContract.Document.MimeTypeDir)
                {
                    Visit(id, path + "/", depth + 1);
                    continue;
                }
                if (!(name.EndsWith(".atlas", StringComparison.OrdinalIgnoreCase) ||
                      name.EndsWith(".skel", StringComparison.OrdinalIgnoreCase) ||
                      name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                      name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))) continue;
                var document = DocumentsContract.BuildDocumentUriUsingTree(tree, id);
                using var stream = resolver.OpenInputStream(document)
                    ?? throw new IOException($"Não foi possível abrir {path}.");
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                results.Add(new Entry(path, buffer.ToArray()));
            }
        }
        Visit(root, "", 0);
        return results;
    }
}
