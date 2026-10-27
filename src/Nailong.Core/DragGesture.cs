namespace Nailong.Core;

public sealed class DragGesture
{
    double startX, startY;
    public bool IsDown { get; private set; }
    public bool IsDragging { get; private set; }
    public void Begin(double x, double y)
    {
        startX = x; startY = y; IsDown = true; IsDragging = false;
    }
    public (double X, double Y) Move(double x, double y, double threshold)
    {
        if (!IsDown) return (0, 0);
        var dx = x - startX; var dy = y - startY;
        if (Math.Abs(dx) >= threshold || Math.Abs(dy) >= threshold) IsDragging = true;
        return (dx, dy);
    }
    public bool End()
    {
        var click = IsDown && !IsDragging;
        IsDown = false; IsDragging = false;
        return click;
    }
    public void Cancel() { IsDown = false; IsDragging = false; }
}
