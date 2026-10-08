using global::Android.Graphics;

namespace SpineViewer.Android;

// Both backends receive the same straight-alpha RGBA pixels. Extending RGB into
// fully transparent texels avoids dark/bright seams under bilinear sampling.
internal static class SpineAtlasPixels
{
    internal static byte[] Decode(byte[] png, out int width, out int height)
    {
        using var bitmap = BitmapFactory.DecodeByteArray(png, 0, png.Length)
            ?? throw new InvalidOperationException("Não foi possível decodificar a textura PNG.");
        width = bitmap.Width;
        height = bitmap.Height;
        var pixels = new int[checked(width * height)];
        bitmap.GetPixels(pixels, 0, width, 0, 0, width, height);
        var rgba = new byte[checked(pixels.Length * 4)];
        for (int i = 0; i < pixels.Length; i++)
        {
            uint c = unchecked((uint)pixels[i]);
            int p = i * 4;
            rgba[p] = (byte)(c >> 16);
            rgba[p + 1] = (byte)(c >> 8);
            rgba[p + 2] = (byte)c;
            rgba[p + 3] = (byte)(c >> 24);
        }
        // Dilate colors into completely transparent pixels only. Visible texels
        // and alpha remain untouched. Four passes cover typical atlas padding.
        var copy = new byte[rgba.Length];
        for (int pass = 0; pass < 4; pass++)
        {
            Buffer.BlockCopy(rgba, 0, copy, 0, rgba.Length);
            bool changed = false;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int p = (y * width + x) * 4;
                if (rgba[p + 3] != 0) continue;
                int[] nx = { x - 1, x + 1, x, x };
                int[] ny = { y, y, y - 1, y + 1 };
                for (int k = 0; k < 4; k++)
                {
                    if (nx[k] < 0 || nx[k] >= width || ny[k] < 0 || ny[k] >= height) continue;
                    int q = (ny[k] * width + nx[k]) * 4;
                    if (rgba[q + 3] == 0 && rgba[q] == 0 && rgba[q + 1] == 0 && rgba[q + 2] == 0) continue;
                    copy[p] = rgba[q]; copy[p + 1] = rgba[q + 1]; copy[p + 2] = rgba[q + 2];
                    changed = true;
                    break;
                }
            }
            if (!changed) break;
            (rgba, copy) = (copy, rgba);
        }
        return rgba;
    }
}
