using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Nailong.Core;
using OpenCvSharp;

internal static class BellyAnimation
{
    internal static object ManifestEntry() => new
    {
        id = "belly", frames = Enumerable.Range(0, BellyMotion.FrameCount).Select(i => $"belly/{i:D3}.png"),
        fps = BellyMotion.Fps, loop = false
    };

    internal static void Compile(string root, BitmapSource neutral)
    {
        var reference = Path.Combine(root, "assets", "reference", "belly-click");
        var generated = SpriteAssets.Split(Path.Combine(reference, "production-sheet-v1.png"), 2, 2);
        // Keep the calibrated face and planted feet. Only arms and the occluded belly change.
        static double Region(int x, int y) => PetMotion.Ease(175, 215, y) * (1 - PetMotion.Ease(380, 415, y));
        var poses = new[] { neutral }.Concat(generated.Select(p => SpriteAssets.Patch(neutral, p, Region))).ToArray();
        for (var i = 0; i < poses.Length; i++) SpriteAssets.Save(poses[i], Path.Combine(reference, $"pose-{i:D2}.png"));
        Point2d[][] hands = [
            [new(106,319), new(136,324), new(142,341), new(139,358), new(129,371), new(108,365), new(96,346), new(120,347)],
            [new(128,284), new(157,285), new(168,302), new(165,320), new(154,337), new(134,332), new(124,310), new(147,310)],
            [new(129,285), new(166,287), new(181,306), new(177,326), new(159,347), new(139,339), new(126,316), new(153,316)],
            [new(184,289), new(226,285), new(248,304), new(248,323), new(230,347), new(199,339), new(180,315), new(215,315)],
            [new(188,294), new(231,289), new(251,310), new(249,333), new(230,359), new(202,347), new(185,322), new(218,324)]
        ];
        // Corresponding contour points around both hands, mirrored for the right palm.
        for (var p = 0; p < hands.Length; p++) hands[p] = hands[p].Concat(hands[p].Select(h => new Point2d(512 - h.X, h.Y))).ToArray();
        Point2d[] fixedPoints = [new(0,0), new(511,0), new(0,511), new(511,511), new(0,256), new(511,256), new(256,50), new(216,101), new(299,101), new(256,136), new(256,172), new(256,235), new(256,375), new(256,420), new(188,454), new(324,454)];
        (int Frame, int Pose)[] keys = [(0,0), (3,0), (10,1), (17,2), (24,3), (30,4), (51,4), (58,3), (65,2), (71,1), (77,0)];
        var morphs = new Dictionary<(int, int), FlowMorph>();
        Point2d[] Points(int pose)
        {
            var px = SpriteAssets.Pixels(poses[pose]); var silhouette = new List<Point2d>();
            foreach (var y in new[] { 200, 240, 275, 380, 415 })
            {
                var left = 511; var right = 0;
                for (var x = 0; x < 512; x++) if (px[(y * 512 + x) * 4 + 3] > 96) { left = Math.Min(left, x); right = Math.Max(right, x); }
                silhouette.Add(new(left, y)); silhouette.Add(new(right, y));
            }
            return fixedPoints.Concat(silhouette).Concat(hands[pose]).ToArray();
        }
        var output = Path.Combine(root, "assets", "animations", "belly"); Directory.CreateDirectory(output);
        var artifacts = Path.Combine(root, "artifacts", "belly"); Directory.CreateDirectory(artifacts);
        var frames = new List<BitmapSource>(); var report = new List<object>(); byte[]? previous = null;
        try
        {
            for (var i = 0; i < BellyMotion.FrameCount; i++)
            {
                var upper = Array.FindIndex(keys, k => k.Frame >= i); var lower = Math.Max(0, upper - 1);
                if (keys[upper].Frame == i) lower = upper;
                var a = keys[lower]; var b = keys[upper]; BitmapSource frame;
                if (a.Pose == b.Pose) frame = poses[a.Pose];
                else
                {
                    var pair = (Math.Min(a.Pose, b.Pose), Math.Max(a.Pose, b.Pose));
                    if (!morphs.TryGetValue(pair, out var morph))
                    {
                        morph = new FlowMorph(poses[pair.Item1], poses[pair.Item2], Points(pair.Item1), Points(pair.Item2), singleSurface: true);
                        morphs.Add(pair, morph);
                    }
                    var t = PetMotion.Ease(a.Frame, b.Frame, i);
                    var interpolated = morph.At(a.Pose < b.Pose ? t : 1 - t);
                    frame = SpriteAssets.Patch(neutral, interpolated, (x, y) => PetMotion.Ease(160, 175, y) * (1 - PetMotion.Ease(415, 430, y)));
                }
                if (i == 0 || i == BellyMotion.FrameCount - 1) frame = neutral;
                frame = LockIdentity(frame, neutral);
                SpriteAssets.Save(frame, Path.Combine(output, $"{i:D3}.png")); frames.Add(frame);
                var px = SpriteAssets.Pixels(frame); double delta = 0;
                if (previous != null) { for (var k = 0; k < px.Length; k++) delta += Math.Abs(px[k] - previous[k]); delta /= px.Length; }
                var foot = 0;
                for (var y = 430; y < 512; y++) for (var x = 0; x < 512; x++) if (px[(y * 512 + x) * 4 + 3] >= 64) foot = Math.Max(foot, y);
                if (foot is < 469 or > 471 || delta > 8) throw new InvalidDataException($"Belly animation continuity failed at {i}: foot={foot}, delta={delta:F3}");
                previous = px;
                report.Add(new { frame = i, timeMs = Math.Round(i * 1000d / BellyMotion.Fps, 3), footBaseline = foot, adjacentMeanPixelChange = Math.Round(delta, 4) });
                Console.WriteLine($"Belly frame {i + 1}/{BellyMotion.FrameCount}");
            }
        }
        finally { foreach (var morph in morphs.Values) morph.Dispose(); }
        File.WriteAllText(Path.Combine(artifacts, "continuity-validation.json"), JsonSerializer.Serialize(new
        {
            frameCount = BellyMotion.FrameCount, fps = BellyMotion.Fps, durationMs = BellyMotion.Duration,
            settleMs = 1000, holdUntilMs = 1700, keys, frames = report
        }, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
        Contact(frames, [0, 5, 10, 17, 24, 30, 42, 51, 58, 65, 71, 77], Path.Combine(artifacts, "contact-sheet.png"));
    }

    static BitmapSource LockIdentity(BitmapSource frame, BitmapSource neutral)
    {
        var a = new byte[512 * 512 * 4]; var b = new byte[a.Length];
        new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0).CopyPixels(a, 2048, 0);
        new FormatConvertedBitmap(neutral, PixelFormats.Bgra32, null, 0).CopyPixels(b, 2048, 0);
        if (ReferenceEquals(frame, neutral)) return neutral;
        Buffer.BlockCopy(b, 0, a, 0, 160 * 2048);
        Buffer.BlockCopy(b, 430 * 2048, a, 430 * 2048, (512 - 430) * 2048);
        var result = BitmapSource.Create(512, 512, 96, 96, PixelFormats.Bgra32, null, a, 2048); result.Freeze(); return result;
    }

    static void Contact(List<BitmapSource> frames, int[] selected, string path)
    {
        var visual = new DrawingVisual(); using (var dc = visual.RenderOpen())
            for (var n = 0; n < selected.Length; n++)
            {
                var x = n % 6 * 220; var y = n / 6 * 245;
                var light = n % 2 == 0;
                dc.DrawRectangle(new SolidColorBrush(light ? Color.FromRgb(255,245,222) : Color.FromRgb(40,45,55)), null, new System.Windows.Rect(x, y, 220, 245));
                dc.DrawImage(frames[selected[n]], new System.Windows.Rect(x, y + 25, 220, 220));
                dc.DrawText(new FormattedText($"{selected[n]:D3} / {selected[n] / 30d:F2} s", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 13, light ? Brushes.Black : Brushes.White, 1), new System.Windows.Point(x + 8, y + 5));
            }
        var bitmap = new RenderTargetBitmap(1320, 490, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); SpriteAssets.Save(bitmap, path);
    }
}
