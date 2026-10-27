using Nailong.Core;

var clips = new Dictionary<string, Clip>
{
    ["idle"] = new("idle", 8, 4, true), ["blink"] = new("blink", 8, 12, false),
    ["wave"] = new("wave", 8, 7, false), ["tilt"] = new("tilt", 8, 6, false), ["pet"] = new("pet", PetMotion.FrameCount, PetMotion.Fps, false),
    ["belly"] = new("belly", BellyMotion.FrameCount, BellyMotion.Fps, false)
};
var passed = 0;
void Check(bool result, string message) { if (!result) throw new Exception(message); passed++; Console.WriteLine("PASS " + message); }
AnimationController New(double random = 0.5) => new(clips, () => random);
var c = New();
Check(c.Tick(250) == 1 && c.Current == "idle", "idle advances using elapsed time");
Check(c.Tick(2250) == 1, "idle loops");
c.Tick(5000); Check(c.Current == "blink", "blink occurs within 3–7 seconds");
c.Tick(5700); Check(c.Current == "idle", "one-shot returns to idle");
c.Play("wave", 6000); c.Tick(6100); Check(c.Current == "wave", "manual wave replaces automatic action");
c.Play("wave", 6200); Check(c.Tick(6200) == 0, "repeated click restarts without queue");
c.Tick(7400); Check(c.Current == "idle", "repeated clicks leave no pending actions");
c.SetAutomatic(false, 7400); c.Tick(999999); Check(c.Current == "idle", "automatic off prevents spontaneous actions");
c.Play("tilt", 1000000); c.Tick(1000100); Check(c.Current == "tilt", "manual actions work with automatic disabled");
c.Tick(1001400); Check(c.Current == "idle", "manual tilt completes");
c = New(0); c.Tick(15000); Check(c.Current == "tilt", "head tilt takes priority when both timers are due");
c = New(0); c.Suspended = true; c.Tick(20000); Check(c.Current == "idle", "drag and menus suppress automatic action triggers");
c.Suspended = false; c.Tick(20001); Check(c.Current == "tilt", "automatic behavior resumes after drag");
c = New(); c.SetAutomatic(false, 0); c.SetAutomatic(true, 100000); c.Tick(100001); Check(c.Current == "idle", "re-enable schedules fresh deadlines");
var low = New(0); low.Tick(2999); Check(low.Current == "idle", "blink never fires before three seconds"); low.Tick(3000); Check(low.Current == "blink", "minimum blink deadline");
var high = New(0.9999); high.Tick(7000); Check(high.Current == "blink", "maximum blink deadline");
var drag = new DragGesture();
drag.Begin(100, 100); drag.Move(102, 103, 6); Check(drag.End(), "small pointer jitter remains a click");
drag.Begin(-200, 100); var delta = drag.Move(-190, 120, 6); Check(drag.IsDragging && delta == (10d, 20d), "drag supports negative monitor coordinates");
drag.Move(-200, 100, 6); Check(!drag.End(), "drag returning to origin is still not a click");
drag.Begin(100, 100); drag.Move(110, 100, 12); Check(!drag.IsDragging, "DPI-scaled threshold respected"); drag.Move(112, 100, 12); Check(!drag.End(), "threshold starts drag");
drag.Begin(0, 0); drag.Cancel(); Check(!drag.End(), "capture loss cannot synthesize a click");
Check(PetInteraction.ActionAt(0.5, 0.23, "idle") == "pet", "head click selects petting");
Check(PetInteraction.ActionAt(0.5, 0.65, "idle") == "belly", "belly click selects hands-to-belly reaction");
Check(PetInteraction.ActionAt(0.5, 0.48, "pet") == "belly", "belly remains clickable during petting");
Check(PetInteraction.ActionAt(0.24, 0.55, "idle") == "wave", "arm click keeps waving available");
Check(PetInteraction.ActionAt(0.5, 0.88, "idle") == "wave", "feet do not trigger the belly action");
Check(PetInteraction.ActionAt(0.75, 0.64, "pet") == "belly", "belly target follows the turned body");
Check(PetInteraction.ActionAt(0.5, 0.23, "pet") == "pet", "head hit area stays consistent during petting");
c = New(); c.Play("pet", 0); Check(c.Tick(5790) == 173 && c.Current == "pet", "reaction uses 174 frames at 30 fps for the full 5.8 seconds");
c.Tick(5800); Check(c.Current == "idle", "petting returns to idle without an immediate automatic interruption");
c.Play("pet", 5000); c.Play("pet", 6000); Check(c.Tick(6000) == 30 && c.Current == "pet", "repeated head clicks preserve the current motion instead of restarting");
c.Tick(10800); Check(c.Current == "idle", "repeated petting leaves no queued clips");
Check(PetMotion.Sample(0).Presence == 0 && PetMotion.Sample(0).Press == 0 && PetMotion.Sample(0).Cold == 0, "petting starts with neutral pose");
Check(PetMotion.Sample(5.8).Presence == 0 && PetMotion.Sample(5.8).Turn == 0 && PetMotion.Sample(5.8).Hands == 0 && PetMotion.Sample(5.8).Cold == 0, "petting finishes front-facing with hands down");
Check(PetMotion.Sample(1).Presence == 1 && PetMotion.Sample(1).Press > 0 && PetMotion.Sample(1).Cold == 0, "initial head contact retains original open eyes");
Check(PetMotion.Sample(1.5).Cold == 1 && PetMotion.Sample(1.5).Turn == 0, "eyes turn cold before body turns");
Check(PetMotion.Sample(2.8).Turn == 1 && PetMotion.Sample(2.8).Hands == 1 && PetMotion.Sample(2.8).Presence == 0, "right turn and hands-to-belly precede side hold");
Check(PetMotion.Sample(4.9).Turn == 1 && PetMotion.Sample(4.9).Hands == 1, "side pose remains until original reaction completes");
Check(PetMotion.ReturnStart == 5 && PetMotion.Duration == 5800, "300 ms approach plus 4700 ms original reaction plus 800 ms return");
c = New(); c.SetAutomatic(false, 0); c.Play("belly", 0);
Check(c.Tick(1000) == 30 && c.Current == "belly", "belly reaction advances through the held pose");
c.Play("belly", 1100); Check(c.Tick(1200) == 36, "repeated belly taps preserve playback position");
Check(c.Tick(2599) == 77 && c.Current == "belly", "belly reaction includes the complete release");
c.Tick(2600); Check(c.Current == "idle", "belly reaction finishes at 2.6 seconds without queued repeats");
c.Play("belly", 3000); c.Play("pet", 3400); Check(c.Current == "pet" && c.Tick(3400) == 0, "head click can interrupt belly reaction");
c.Play("belly", 3500); Check(c.Current == "belly" && c.Tick(3500) == 0, "belly click can interrupt a different action");
c.SetAutomatic(true, 3500); c.Tick(6100); Check(c.Current == "idle", "automatic timers restart after manual belly reaction");
Console.WriteLine($"{passed} checks passed.");
