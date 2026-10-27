namespace Nailong.Core;

public readonly record struct PetPose(double Presence, double Travel, double Stroke, double Press, double Cold, double Turn, double Hands);

public static class PetMotion
{
    public const int FrameCount = 174;
    public const int Fps = 30;
    public const double Duration = FrameCount * 1000d / Fps;
    public const double LeadIn = 0.3;
    public const double OriginalReaction = 4.7;
    public const double ReturnStart = LeadIn + OriginalReaction;
    public static double Ease(double start, double end, double time)
    {
        var x = Math.Clamp((time - start) / (end - start), 0, 1);
        return x * x * (3 - 2 * x);
    }
    public static PetPose Sample(double seconds)
    {
        // Original GIF: 47 frames at 10 fps, shifted by the 0.3 s hand approach.
        var presence = Ease(0, LeadIn, seconds) * (1 - Ease(1.48, 1.78, seconds));
        var travel = 1 - Ease(0, LeadIn, seconds) + Ease(1.48, 1.78, seconds);
        var contact = Ease(0.23, 0.36, seconds) * (1 - Ease(1.38, 1.55, seconds));
        var press = contact * (0.5 - 0.5 * Math.Cos((seconds - 0.3) * Math.PI * 4));
        var stroke = 4 * Math.Sin((seconds - 0.3) * Math.PI * 4) * contact;
        var cold = Ease(1.22, 1.5, seconds) * (1 - Ease(5.6, 173d / Fps, seconds));
        var turn = Ease(1.5, 2.1, seconds) * (1 - Ease(5.22, 5.7, seconds));
        var hands = Ease(2.08, 2.7, seconds) * (1 - Ease(5, 5.25, seconds));
        return new(presence, travel, stroke, press, cold, turn, hands);
    }
}
