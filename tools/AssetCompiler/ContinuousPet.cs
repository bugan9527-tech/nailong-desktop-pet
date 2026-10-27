using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Nailong.Core;
using Point2d = OpenCvSharp.Point2d;

internal static class ContinuousPet
{
    internal static void Compile(string root, BitmapSource neutral)
    {
        var reference = Path.Combine(root, "assets", "reference", "calibrated");
        var turn = SpriteAssets.Split(Path.Combine(reference, "turn-sheet.png"), 3, 2);
        var cold = SpriteAssets.Patch(neutral, turn[0], (x, y) => 1 - PetMotion.Ease(.78, 1, Math.Sqrt(Math.Pow((x - 256) / 87d, 2) + Math.Pow((y - 112) / 62d, 2))));
        var down = SpriteAssets.Load(Path.Combine(reference, "side-down.png"));
        var hold = SpriteAssets.Load(Path.Combine(reference, "side-hold.png"));
        var poses = new[] { neutral, cold, turn[1], turn[2], down, turn[4], hold };
        Point2d[][] features = [
            [new(216, 101), new(299, 101), new(256, 136), new(256, 50), new(256, 172), new(126, 353), new(119, 324), new(400, 353), new(405, 326), new(256, 235), new(256, 420)],
            [new(216, 101), new(299, 101), new(256, 136), new(256, 50), new(256, 172), new(126, 353), new(119, 324), new(400, 353), new(405, 326), new(256, 235), new(256, 420)],
            [new(269, 108), new(333, 99), new(312, 136), new(278, 50), new(274, 172), new(177, 363), new(158, 331), new(399, 344), new(405, 321), new(302, 221), new(291, 416)],
            [new(284, 109), new(339, 100), new(324, 134), new(282, 50), new(274, 172), new(188, 365), new(164, 331), new(397, 344), new(401, 321), new(309, 217), new(300, 414)],
            [new(282, 108), new(332, 99), new(319, 134), new(274, 50), new(270, 172), new(184, 365), new(161, 329), new(397, 344), new(401, 321), new(305, 217), new(300, 414)],
            [new(294, 110), new(346, 101), new(332, 136), new(286, 50), new(276, 172), new(302, 331), new(253, 313), new(383, 321), new(399, 294), new(309, 220), new(300, 414)],
            [new(282, 108), new(332, 99), new(319, 134), new(274, 50), new(270, 172), new(292, 339), new(248, 320), new(381, 327), new(400, 300), new(305, 217), new(300, 414)]
        ];
        Point2d[][] details = [
            [new(225,132), new(286,132), new(188,454), new(324,454), new(256,443)],
            [new(225,132), new(286,132), new(188,454), new(324,454), new(256,443)],
            [new(288,137), new(331,125), new(195,454), new(315,454), new(249,443)],
            [new(308,136), new(338,128), new(197,455), new(314,451), new(249,441)],
            [new(302,136), new(333,128), new(196,455), new(313,451), new(247,441)],
            [new(314,137), new(346,128), new(199,455), new(314,451), new(252,441)],
            [new(302,136), new(333,128), new(196,455), new(313,451), new(247,441)]
        ];
        for (var i = 0; i < features.Length; i++) features[i] = features[i].Concat(details[i]).ToArray();
        for (var i = 0; i < poses.Length; i++) SpriteAssets.Save(poses[i], Path.Combine(reference, $"reaction-{i:D2}.png"));
        (int Frame, int Pose)[] timeline = [(0, 0), (9, 0), (36, 0), (45, 1), (51, 2), (57, 3), (63, 4), (72, 5), (81, 6), (150, 6), (158, 4), (162, 3), (166, 2), (170, 1), (173, 0)];
        var morphs = new Dictionary<(int, int), FlowMorph>();
        var hand = CropAlpha(SpriteAssets.Load(Path.Combine(root, "assets", "reference", "pet", "hand-overlay.png")));
        var output = Path.Combine(root, "assets", "animations", "pet"); Directory.CreateDirectory(output);
        var artifacts = Path.Combine(root, "artifacts", "pet"); Directory.CreateDirectory(artifacts);
        var frames = new List<BitmapSource>(); var report = new List<object>(); byte[]? previous = null;
        try
        {
            for (var i = 0; i < PetMotion.FrameCount; i++)
            {
                var upper = Array.FindIndex(timeline, key => key.Frame >= i); var lower = Math.Max(0, upper - 1);
                if (timeline[upper].Frame == i) lower = upper;
                var a = timeline[lower]; var b = timeline[upper];
                BitmapSource body;
                if (a.Pose == b.Pose) body = poses[a.Pose];
                else
                {
                    var pair = (a.Pose, b.Pose);
                    if (!morphs.TryGetValue(pair, out var morph)) { morph = new FlowMorph(poses[a.Pose], poses[b.Pose], features[a.Pose], features[b.Pose]); morphs.Add(pair, morph); }
                    body = morph.At(PetMotion.Ease(a.Frame, b.Frame, i));
                }
                var pose = PetMotion.Sample(i / (double)PetMotion.Fps);
                if (i > 81 && i < 150) body = SpriteAssets.Breathe(body, (i - 81) / 69d);
                if (pose.Press > 0) body = SpriteAssets.HeadWarp(body, 0, pose.Press * 2);
                var visual = new DrawingVisual(); RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
                using (var dc = visual.RenderOpen())
                {
                    var rect = new Rect(-38 - pose.Travel * 230 + pose.Stroke, -4 - pose.Travel * 150 + pose.Press, 340, 340d * hand.PixelHeight / hand.PixelWidth);
                    dc.PushOpacity(pose.Presence); dc.DrawImage(hand, rect); dc.Pop();
                    dc.DrawImage(body, new Rect(0, 0, 512, 512));
                    dc.PushClip(new RectangleGeometry(new Rect(160, 0, 190, 59 + pose.Press * 2)));
                    dc.PushOpacity(pose.Presence); dc.DrawImage(hand, rect); dc.Pop(); dc.Pop();
                }
                var render = new RenderTargetBitmap(512, 512, 96, 96, PixelFormats.Pbgra32); render.Render(visual); render.Freeze();
                BitmapSource frame = i == 0 || i == PetMotion.FrameCount - 1 ? neutral : render;
                SpriteAssets.Save(frame, Path.Combine(output, $"{i:D3}.png")); frames.Add(frame);
                var px = SpriteAssets.Pixels(frame); var bodyPx = SpriteAssets.Pixels(body); var bottom = 0; var left = 512; var right = 0;
                for (var y = 430; y < 512; y++) for (var x = 0; x < 512; x++) if (bodyPx[(y * 512 + x) * 4 + 3] >= 64)
                { bottom = Math.Max(bottom, y); left = Math.Min(left, x); right = Math.Max(right, x); }
                double delta = 0;
                if (previous != null) { for (var k = 0; k < px.Length; k++) delta += Math.Abs(px[k] - previous[k]); delta /= px.Length; }
                if (bottom < 465 || bottom > 474) throw new InvalidDataException($"Foot baseline drift at {i}: {bottom}");
                if (delta > 10) throw new InvalidDataException($"Abrupt frame change at {i}: {delta:F3}");
                previous = px;
                report.Add(new { frame = i, timeMs = Math.Round(i * 1000d / 30, 3), sourceFrame = i >= 9 && i < 150 ? (int?)((i - 9) / 3) : null, footBaseline = bottom, footCenter = (left + right) / 2d, adjacentMeanPixelChange = Math.Round(delta, 4), pose.Cold, pose.Turn, pose.Hands });
            }
        }
        finally { foreach (var morph in morphs.Values) morph.Dispose(); }
        File.WriteAllText(Path.Combine(artifacts, "continuity-validation.json"), JsonSerializer.Serialize(new { frameCount = PetMotion.FrameCount, fps = PetMotion.Fps, durationMs = PetMotion.Duration, leadInMs = 300, originalReactionMs = 4700, returnMs = 800, frames = report }, new JsonSerializerOptions { WriteIndented = true }));
        Contact(frames, [0, 9, 36, 45, 51, 57, 63, 72, 81, 120, 149, 150, 158, 164, 169, 173], Path.Combine(artifacts, "continuous-contact-sheet.png"));
        Compare(root, frames, Path.Combine(artifacts, "original-comparison.png"));
        Console.WriteLine("Built 174 full-body reaction frames: approach, pet, cold eyes, right turn, hands to belly, side hold, release and return.");
    }
    static void Contact(List<BitmapSource> frames, int[] selected, string path)
    {
        var visual = new DrawingVisual(); using (var dc = visual.RenderOpen())
            for (var n = 0; n < selected.Length; n++)
            {
                var x = n % 8 * 200; var y = n / 8 * 225;
                dc.DrawRectangle(n % 2 == 0 ? new SolidColorBrush(Color.FromRgb(255, 245, 222)) : new SolidColorBrush(Color.FromRgb(40, 45, 55)), null, new Rect(x, y, 200, 225));
                dc.DrawImage(frames[selected[n]], new Rect(x, y + 25, 200, 200));
                dc.DrawText(new FormattedText($"{selected[n]:D3} / {selected[n] / 30d:F2} s", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, n % 2 == 0 ? Brushes.Black : Brushes.White, 1), new Point(x + 8, y + 5));
            }
        var bitmap = new RenderTargetBitmap(1600, 450, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); SpriteAssets.Save(bitmap, path);
    }
    static void Compare(string root, List<BitmapSource> frames, string path)
    {
        var visual = new DrawingVisual(); using (var dc = visual.RenderOpen())
        {
            int[] source = [0, 14, 24, 46]; int[] actual = [9, 51, 81, 147];
            string[] labels = ["FRONT / pet", "BEGIN RIGHT TURN", "HANDS TO BELLY", "SIDE HOLD"];
            for (var n = 0; n < 4; n++)
            {
                var x = n * 402;
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(244, 235, 215)), null, new Rect(x, 0, 402, 800));
                dc.DrawText(new FormattedText($"{labels[n]} | original {source[n]:D2} -> new {actual[n]:D3}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 15, Brushes.Black, 1), new Point(x + 10, 12));
                dc.DrawImage(SpriteAssets.Load(Path.Combine(root, "assets", "reference", "pet", "frames", $"{source[n]:D3}.png")), new Rect(x, 45, 402, 338));
                dc.DrawImage(frames[actual[n]], new Rect(x, 390, 402, 402));
            }
        }
        var bitmap = new RenderTargetBitmap(1608, 800, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); SpriteAssets.Save(bitmap, path);
    }
    static BitmapSource CropAlpha(BitmapSource source)
    {
        var pixels = SpriteAssets.Pixels(source); var w = source.PixelWidth; var h = source.PixelHeight;
        var left = w; var top = h; var right = 0; var bottom = 0;
        for (var y = 0; y < h; y++) for (var x = 0; x < w; x++) if (pixels[(y * w + x) * 4 + 3] > 16)
        { left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y); }
        return new CroppedBitmap(source, new Int32Rect(left, top, right - left + 1, bottom - top + 1));
    }
}
