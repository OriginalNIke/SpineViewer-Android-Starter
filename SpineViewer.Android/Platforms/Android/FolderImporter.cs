using global::Android.Provider;
using Microsoft.Maui.ApplicationModel;

namespace SpineViewer.Android;

// Storage Access Framework: sem permissão de acesso irrestrito ao armazenamento.
public static class FolderImporter
{
    public sealed record Entry(string Name, byte[] Content);

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
        var parentId = DocumentsContract.GetTreeDocumentId(tree)
            ?? throw new InvalidDataException("Pasta inválida.");
        var children = DocumentsContract.BuildChildDocumentsUriUsingTree(tree, parentId);
        var results = new List<Entry>();
        string[] projection = { DocumentsContract.Document.ColumnDocumentId,
                                DocumentsContract.Document.ColumnDisplayName,
                                DocumentsContract.Document.ColumnMimeType };
        using var cursor = resolver.Query(children, projection, null, null, null)
            ?? throw new IOException("Não foi possível listar os arquivos da pasta.");
        while (cursor.MoveToNext())
        {
            string id = cursor.GetString(0) ?? "";
            string name = cursor.GetString(1) ?? "";
            string mime = cursor.GetString(2) ?? "";
            if (mime == DocumentsContract.Document.MimeTypeDir) continue;
            if (!(name.EndsWith(".atlas", StringComparison.OrdinalIgnoreCase) ||
                  name.EndsWith(".skel", StringComparison.OrdinalIgnoreCase) ||
                  name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                  name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))) continue;
            var document = DocumentsContract.BuildDocumentUriUsingTree(tree, id);
            using var stream = resolver.OpenInputStream(document)
                ?? throw new IOException($"Não foi possível abrir {name}.");
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            results.Add(new Entry(name, buffer.ToArray()));
        }
        return results;
    }
}
