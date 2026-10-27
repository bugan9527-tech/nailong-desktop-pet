using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Nailong.Core;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
        var reference = Path.Combine(root, "assets", "reference", "calibrated");
        if (args.Contains("--belly-only"))
        {
            BellyAnimation.Compile(root, SpriteAssets.Load(Path.Combine(reference, "front.png")));
            var path = Path.Combine(root, "assets", "animations", "animations.json");
            var existing = JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonArray>(File.ReadAllText(path))!;
            foreach (var entry in existing.Where(e => e?["id"]?.GetValue<string>() == "belly").ToArray()) existing.Remove(entry);
            existing.Add(JsonSerializer.SerializeToNode(BellyAnimation.ManifestEntry()));
            File.WriteAllText(path, existing.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            return;
        }
        var views = SpriteAssets.Split(Path.Combine(reference, "model-sheet.png"), 3, 1);
        var names = new[] { "front.png", "side-down.png", "side-hold.png" };
        for (var i = 0; i < views.Length; i++) SpriteAssets.Save(views[i], Path.Combine(reference, names[i]));
        if (args.Contains("--calibrate-only")) return;
        var neutral = views[0];
        var actions = SpriteAssets.Split(Path.Combine(reference, "actions-sheet.png"), 3, 2);
        for (var i = 0; i < actions.Length; i++) SpriteAssets.Save(actions[i], Path.Combine(reference, $"action-{i:D2}.png"));
        double Eyes(int x, int y) => 1 - PetMotion.Ease(.72, 1, Math.Sqrt(Math.Pow((x - 256) / 83d, 2) + Math.Pow((y - 106) / 37d, 2)));
        var half = SpriteAssets.Patch(neutral, actions[0], Eyes);
        var shut = SpriteAssets.Patch(neutral, actions[1], Eyes);
        double Arm(int x, int y) => PetMotion.Ease(322, 350, x) * PetMotion.Ease(110, 140, y) * (1 - PetMotion.Ease(375, 403, y));
        var waves = SpriteAssets.Split(Path.Combine(reference, "wave-sheet.png"), 2, 2);
        var low = SpriteAssets.Patch(neutral, waves[0], Arm);
        var mid = SpriteAssets.Patch(neutral, waves[1], Arm);
        var wave = SpriteAssets.Patch(neutral, waves[2], Arm);
        var outward = SpriteAssets.Patch(neutral, waves[3], Arm);
        // Generated poses contribute only their moving regions, keeping the calibrated model elsewhere.
        var all = new List<BitmapSource[]>();
        all.Add(Enumerable.Range(0, 8).Select(i => SpriteAssets.Breathe(neutral, i / 7d)).ToArray());
        all.Add(new[] { neutral, half, shut, shut, half, neutral, neutral, neutral });
        all.Add(new[] { neutral, low, mid, wave, outward, wave, mid, neutral });
        all.Add(new[] { 0d, -4d, -8d, -12d, -12d, -8d, -4d, 0d }.Select(deg => SpriteAssets.HeadWarp(neutral, deg)).ToArray());
        var specs = new[] { (Id: "idle", Fps: 4d, Loop: true), (Id: "blink", Fps: 12d, Loop: false), (Id: "wave", Fps: 7d, Loop: false), (Id: "tilt", Fps: 6d, Loop: false) };
        var output = Path.Combine(root, "assets", "animations");
        var manifest = new List<object>(); var report = new List<object>();
        for (var a = 0; a < specs.Length; a++)
        {
            var spec = specs[a]; var frames = all[a]; frames[0] = neutral; frames[^1] = neutral;
            var files = new string[frames.Length];
            for (var i = 0; i < frames.Length; i++)
            {
                files[i] = $"{spec.Id}/{i:D3}.png"; SpriteAssets.Save(frames[i], Path.Combine(output, files[i]));
                report.Add(new { action = spec.Id, frame = i, calibratedModel = true, endpointMatchesIdle = i != 0 && i != 7 || SpriteAssets.Pixels(frames[i]).SequenceEqual(SpriteAssets.Pixels(neutral)) });
            }
            manifest.Add(new { id = spec.Id, frames = files, fps = spec.Fps, loop = spec.Loop });
        }
        ContinuousPet.Compile(root, neutral);
        manifest.Add(new { id = "pet", frames = Enumerable.Range(0, PetMotion.FrameCount).Select(i => $"pet/{i:D3}.png"), fps = (double)PetMotion.Fps, loop = false });
        BellyAnimation.Compile(root, neutral);
        manifest.Add(BellyAnimation.ManifestEntry());
        File.WriteAllText(Path.Combine(output, "animations.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        Directory.CreateDirectory(Path.Combine(root, "artifacts"));
        File.WriteAllText(Path.Combine(root, "artifacts", "asset-validation.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        ContactSheet(all, Path.Combine(root, "artifacts", "animation-contact-sheet.png"));
        var icon = new DrawingVisual(); using (var dc = icon.RenderOpen()) dc.DrawImage(neutral, new Rect(0, 0, 256, 256));
        var ib = new RenderTargetBitmap(256, 256, 96, 96, PixelFormats.Pbgra32); ib.Render(icon);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(ib)); using var data = new MemoryStream(); encoder.Save(data);
        using var writer = new BinaryWriter(File.Create(Path.Combine(output, "nailong.ico")));
        writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)1);
        writer.Write((byte)0); writer.Write((byte)0); writer.Write((byte)0); writer.Write((byte)0);
        writer.Write((ushort)1); writer.Write((ushort)32); writer.Write((uint)data.Length); writer.Write((uint)22); writer.Write(data.ToArray());
        Console.WriteLine($"Compiled 32 calibrated base frames, {PetMotion.FrameCount} petting frames and {BellyMotion.FrameCount} belly reaction frames.");
    }
    static void ContactSheet(List<BitmapSource[]> actions, string path)
    {
        var visual = new DrawingVisual(); using (var dc = visual.RenderOpen())
        {
            var names = new[] { "IDLE / breathe", "BLINK", "WAVE", "HEAD TILT" };
            for (var row = 0; row < 4; row++) for (var col = 0; col < 8; col++)
            {
                var x = col * 144; var y = row * 170;
                dc.DrawRectangle(col % 2 == 0 ? new SolidColorBrush(Color.FromRgb(255, 245, 222)) : new SolidColorBrush(Color.FromRgb(41, 47, 57)), null, new Rect(x, y, 144, 170));
                dc.DrawImage(actions[row][col], new Rect(x, y + 22, 144, 144));
                dc.DrawText(new FormattedText($"{names[row]}  {col}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, col % 2 == 0 ? Brushes.Black : Brushes.White, 1), new Point(x + 6, y + 5));
            }
        }
        var target = new RenderTargetBitmap(1152, 680, 96, 96, PixelFormats.Pbgra32); target.Render(visual); SpriteAssets.Save(target, path);
    }
}
