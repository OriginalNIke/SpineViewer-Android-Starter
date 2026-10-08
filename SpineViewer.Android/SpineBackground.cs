namespace SpineViewer.Android;

// Shared background color for OpenGL and Vulkan. Stored as packed RGB.
internal static class SpineBackground
{
    private static int rgb = 0x111827;
    public static void Set(int color) => System.Threading.Volatile.Write(ref rgb, color & 0xFFFFFF);
    public static int Current => System.Threading.Volatile.Read(ref rgb);
    public static float R => ((Current >> 16) & 255) / 255f;
    public static float G => ((Current >> 8) & 255) / 255f;
    public static float B => (Current & 255) / 255f;
}
