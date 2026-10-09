using Android.Views;

namespace SpineViewer.Android;

// Shared camera controls for the two native rendering surfaces.
internal static class SpineCamera
{
    private static readonly object Gate = new();
    private static float zoom = 1f, panX, panY;
    // A moving mesh must not drive camera bounds every frame. Shared by both backends.
    private static bool hasFrame;
    private static float frameX, frameY, frameWidth, frameHeight;
    public static (float X, float Y, float Width, float Height) StableFrame(float minX, float minY, float maxX, float maxY)
    {
        lock (Gate)
        {
            if (!hasFrame && float.IsFinite(minX) && float.IsFinite(minY) && float.IsFinite(maxX) && float.IsFinite(maxY) && maxX > minX && maxY > minY)
            {
                frameX = (minX + maxX) * 0.5f;
                frameY = (minY + maxY) * 0.5f;
                frameWidth = Math.Max(1f, maxX - minX);
                frameHeight = Math.Max(1f, maxY - minY);
                hasFrame = true;
            }
            return (frameX, frameY, Math.Max(1f, frameWidth), Math.Max(1f, frameHeight));
        }
    }
    public static void ResetCharacterFrame()
    {
        lock (Gate) { hasFrame = false; zoom = 1f; panX = 0; panY = 0; }
    }
    public static (float Zoom, float PanX, float PanY) Snapshot()
    {
        lock (Gate) return (zoom, panX, panY);
    }
    public static void Reset()
    {
        lock (Gate) { zoom = 1f; panX = 0; panY = 0; }
    }
    public static void Move(float dx, float dy)
    {
        lock (Gate) { panX += dx; panY += dy; }
    }
    public static void Scale(float factor, float focusX, float focusY, float width, float height)
    {
        lock (Gate)
        {
            float next = Math.Clamp(zoom * factor, 0.25f, 5f);
            float ratio = next / zoom;
            // Keep the point beneath the gesture centroid fixed.
            panX = focusX - width * 0.5f - (focusX - width * 0.5f - panX) * ratio;
            panY = focusY - height * 0.5f - (focusY - height * 0.5f - panY) * ratio;
            zoom = next;
        }
    }
}

internal sealed class SpineTouch
{
    readonly Action redraw;
    float previousX, previousY, previousDistance;
    int previousPointers;
    long lastTap;
    public SpineTouch(Action redraw) => this.redraw = redraw;
    public bool Handle(global::Android.Views.View view, MotionEvent? e)
    {
        if (e == null) return false;
        var action = e.ActionMasked;
        if (action == MotionEventActions.Down || action == MotionEventActions.PointerDown)
        {
            view.Parent?.RequestDisallowInterceptTouchEvent(true);
            previousPointers = e.PointerCount;
            previousX = e.GetX(0); previousY = e.GetY(0);
            if (e.PointerCount >= 2) previousDistance = Distance(e);
            if (action == MotionEventActions.Down)
            {
                long now = Java.Lang.JavaSystem.CurrentTimeMillis();
                if (now - lastTap < 320) { SpineCamera.Reset(); redraw(); lastTap = 0; }
                else lastTap = now;
            }
            return true;
        }
        if (action == MotionEventActions.Move)
        {
            if (e.PointerCount >= 2)
            {
                float d = Distance(e);
                if (previousPointers >= 2 && previousDistance > 1 && d > 1)
                    SpineCamera.Scale(d / previousDistance, (e.GetX(0) + e.GetX(1)) * 0.5f,
                        (e.GetY(0) + e.GetY(1)) * 0.5f, view.Width, view.Height);
                previousDistance = d;
            }
            else if (e.PointerCount == 1 && previousPointers == 1)
                SpineCamera.Move(e.GetX(0) - previousX, e.GetY(0) - previousY);
            previousX = e.GetX(0); previousY = e.GetY(0);
            previousPointers = e.PointerCount;
            redraw();
            return true;
        }
        if (action == MotionEventActions.PointerUp || action == MotionEventActions.Up || action == MotionEventActions.Cancel)
        {
            previousPointers = 0;
            view.Parent?.RequestDisallowInterceptTouchEvent(false);
            return true;
        }
        return true;
    }
    static float Distance(MotionEvent e)
    {
        float dx = e.GetX(0) - e.GetX(1), dy = e.GetY(0) - e.GetY(1);
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
