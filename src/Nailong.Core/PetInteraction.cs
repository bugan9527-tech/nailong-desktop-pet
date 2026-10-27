namespace Nailong.Core;

public static class PetInteraction
{
    // The head remains inside this region while the full body turns to the right.
    public static string ActionAt(double x, double y, string currentClip)
    {
        var head = x is >= 0.27 and <= 0.73 && y is >= 0.08 and <= 0.38;
        if (head) return "pet";
        // Follow the round belly when the existing head reaction turns sideways.
        var center = currentClip == "pet" ? 0.57 : 0.5;
        var belly = Math.Pow((x - center) / 0.215, 2) + Math.Pow((y - 0.64) / 0.19, 2) <= 1;
        return belly ? "belly" : "wave";
    }
}
