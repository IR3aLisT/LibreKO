using Godot;

namespace LibreKO.Domain;

public static class TooltipPlacement
{
    public static Vector2 AroundCursor(Vector2 cursor, Vector2 size, Vector2 screen, float gap, float margin)
    {
        float x = cursor.X + gap;
        if (x + size.X > screen.X - margin) x = cursor.X - gap - size.X;
        float y = cursor.Y + gap;
        if (y + size.Y > screen.Y - margin) y = cursor.Y - gap - size.Y;
        return new Vector2(Fit(x, size.X, screen.X, margin), Fit(y, size.Y, screen.Y, margin));
    }

    public static Vector2 BesidePanel(Vector2 panel, Vector2 panelSize, Vector2 size, Vector2 screen,
        float gap, float margin)
    {
        float x = panel.X + panelSize.X + gap;
        if (x + size.X > screen.X - margin) x = panel.X - gap - size.X;
        if (x < margin) x = screen.X - margin - size.X;
        return new Vector2(Fit(x, size.X, screen.X, margin), Fit(panel.Y, size.Y, screen.Y, margin));
    }

    private static float Fit(float at, float extent, float screen, float margin) =>
        Mathf.Clamp(at, margin, Mathf.Max(margin, screen - margin - extent));
}
