using System.Text;
namespace SpineViewer.Core;
public sealed record SpineBinaryInfo(string? Version, long ByteCount);
public static class SpineBinaryInspector
{
    // This only probes the modern Spine binary header. It does NOT decode skeletons.
    public static SpineBinaryInfo Inspect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 12) throw new FormatException("Arquivo .skel muito curto.");
        // Modern exports: 8-byte hash then a length-prefixed version string (varint + UTF-8).
        // Versions and formats vary; do not assume the detected version is authoritative.
        string? version = null;
        var offset = 8;
        if (offset < bytes.Length)
        {
            int length = 0, shift = 0;
            for (int j = 0; j < 5 && offset < bytes.Length; j++)
            {
                byte b = bytes[offset++]; length |= (b & 0x7f) << shift;
                if ((b & 0x80) == 0) break;
                shift += 7;
            }
            length -= 1; // Spine strings store length+1; 0 means null.
            if (length > 0 && length < 64 && offset + length <= bytes.Length)
            {
                var candidate = Encoding.UTF8.GetString(bytes.Slice(offset, length));
                if (System.Text.RegularExpressions.Regex.IsMatch(candidate, @"^\d+\.\d+(\.\d+)?")) version = candidate;
            }
        }
        return new SpineBinaryInfo(version, bytes.Length);
    }
}
