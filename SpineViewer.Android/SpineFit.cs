namespace SpineViewer.Android;

// Identical world-to-pixel fit for the OpenGL and Vulkan backends.
internal static class SpineFit
{
    internal static float Calculate(int viewportWidth, int viewportHeight, float modelWidth, float modelHeight)
    {
        float x = Math.Max(1, viewportWidth) * 0.90f / Math.Max(1f, modelWidth);
        float y = Math.Max(1, viewportHeight) * 0.90f / Math.Max(1f, modelHeight);
        return Math.Clamp(Math.Min(x, y), 0.01f, 8f);
    }
}
