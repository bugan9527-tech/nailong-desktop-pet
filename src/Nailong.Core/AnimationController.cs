namespace Nailong.Core;

public sealed record Clip(string Id, int FrameCount, double Fps, bool Loop)
{
    public double Duration => FrameCount * 1000.0 / Fps;
}

// Time is monotonic milliseconds, shared with the renderer. No timers or UI dependencies.
public sealed class AnimationController
{
    readonly IReadOnlyDictionary<string, Clip> clips;
    readonly Func<double> random;
    double started, nextBlink, nextTilt;
    bool manual;
    public string Current { get; private set; } = "idle";
    public bool AutoEnabled { get; private set; } = true;
    public bool Suspended { get; set; }

    public AnimationController(IReadOnlyDictionary<string, Clip> clips, Func<double>? random = null)
    {
        this.clips = clips;
        this.random = random ?? Random.Shared.NextDouble;
        foreach (var id in new[] { "idle", "blink", "wave", "tilt", "pet", "belly" })
            if (!clips.TryGetValue(id, out var c) || c.FrameCount < 1 || c.Fps <= 0 || !double.IsFinite(c.Fps))
                throw new ArgumentException($"动作配置无效：{id}");
        ResetDeadlines(0);
    }

    void ResetDeadlines(double now)
    {
        nextBlink = now + 3000 + random() * 4000;
        nextTilt = now + 15000 + random() * 15000;
    }

    public void SetAutomatic(bool enabled, double now)
    {
        AutoEnabled = enabled;
        ResetDeadlines(now);
    }

    public void Play(string id, double now)
    {
        if (!clips.ContainsKey(id)) throw new ArgumentException($"找不到动作：{id}");
        // Repeated taps keep the contact reactions moving without restarting or queuing.
        if (id is "pet" or "belly" && Current == id) return;
        Current = id;
        started = now;
        manual = true;
        // A manual interaction replaces the current action; it never appends to a queue.
        ResetDeadlines(now);
    }

    public int Tick(double now)
    {
        var clip = clips[Current];
        if (!clip.Loop && now - started >= clip.Duration)
        {
            if (manual) ResetDeadlines(now);
            manual = false;
            Current = "idle";
            started = now;
            clip = clips[Current];
        }
        if (AutoEnabled && !Suspended && Current == "idle")
        {
            if (now >= nextTilt)
            {
                Current = "tilt"; started = now;
                manual = false;
                nextTilt = now + 15000 + random() * 15000;
            }
            else if (now >= nextBlink)
            {
                Current = "blink"; started = now;
                manual = false;
                nextBlink = now + 3000 + random() * 4000;
            }
            clip = clips[Current];
        }
        var frame = (int)(Math.Max(0, now - started) * clip.Fps / 1000);
        return clip.Loop ? frame % clip.FrameCount : Math.Min(frame, clip.FrameCount - 1);
    }
}
