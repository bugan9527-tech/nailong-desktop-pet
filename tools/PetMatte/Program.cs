using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using WpfRect = System.Windows.Rect;

internal static class Program
{
    static readonly int[] KeyFrames = [0, 4, 8, 12, 16, 20, 24, 28, 32, 36, 40, 46];
    [STAThread]
    static void Main(string[] args)
    {
        var root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
        if (args.Contains("--export-gif") || args.Contains("--export-continuous") || args.Contains("--export-belly"))
        {
            if (args.Contains("--export-belly"))
            {
                var clip = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "assets", "animations", "animations.json"))).RootElement.EnumerateArray().Single(c => c.GetProperty("id").GetString() == "belly");
                var frames = clip.GetProperty("frames").EnumerateArray().Select(f =>
                {
                    var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
                    image.UriSource = new Uri(Path.Combine(root, "assets", "animations", f.GetString()!)); image.EndInit(); image.Freeze();
                    return (BitmapSource)image;
                }).ToList();
                SaveGif(frames, Path.Combine(root, "assets", "reference", "belly-click", "收手护肚.gif"), clip.GetProperty("fps").GetInt32());
                return;
            }
            var continuous = args.Contains("--export-continuous");
            var sourceFolder = continuous ? Path.Combine(root, "assets", "animations", "pet") : Path.Combine(root, "assets", "reference", "pet", "original-cutout-frames");
            var frameCount = continuous ? JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "assets", "animations", "animations.json"))).RootElement.EnumerateArray().Single(clip => clip.GetProperty("id").GetString() == "pet").GetProperty("frames").GetArrayLength() : 47;
            var images = Enumerable.Range(0, frameCount).Select(i =>
            {
                var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(Path.Combine(sourceFolder, $"{i:D3}.png")); image.EndInit(); image.Freeze();
                return (BitmapSource)image;
            }).ToList();
            SaveGif(images, Path.Combine(root, "assets", "reference", "pet", continuous ? "摸头-连贯全身.gif" : "摸头-透明背景.gif"), continuous ? 30 : 10);
            return;
        }
        var input = Path.Combine(root, "assets", "reference", "pet");
        var output = Path.Combine(input, "original-cutout-frames"); Directory.CreateDirectory(output);
        var preview = Path.Combine(root, "artifacts", "pet"); Directory.CreateDirectory(preview);
        using var guideSheet = Cv2.ImRead(Path.Combine(input, "keyframes-cutout.png"), ImreadModes.Unchanged);
        using var sheet = new Mat(); Cv2.Resize(guideSheet, sheet, new Size(1608, 1014));
        var guides = new List<Mat>();
        foreach (var index in Enumerable.Range(0, 12))
        {
            using var cell = new Mat(sheet, new Rect(index % 4 * 402, index / 4 * 338, 402, 338));
            using var alpha = new Mat(); Cv2.ExtractChannel(cell, alpha, 3);
            using var binary = new Mat(); Cv2.Threshold(alpha, binary, 64, 255, ThresholdTypes.Binary);
            // Interpolate geometry as a signed distance field, rather than dissolving silhouettes.
            using var inside = new Mat(); using var outside = new Mat(); using var inverse = new Mat();
            Cv2.BitwiseNot(binary, inverse); Cv2.DistanceTransform(binary, inside, DistanceTypes.L2, DistanceTransformMasks.Mask3);
            Cv2.DistanceTransform(inverse, outside, DistanceTypes.L2, DistanceTransformMasks.Mask3);
            var distance = new Mat(); Cv2.Subtract(inside, outside, distance); guides.Add(distance);
        }
        var report = new List<object>();
        var renderedFrames = new List<BitmapSource>();
        try
        {
            for (var i = 0; i < 47; i++)
            {
                using var original = Cv2.ImRead(Path.Combine(input, "frames", $"{i:D3}.png"));
                var upper = Array.FindIndex(KeyFrames, k => k >= i); var lower = Math.Max(0, upper - 1);
                if (KeyFrames[upper] == i) lower = upper;
                var t = lower == upper ? 0 : (i - KeyFrames[lower]) / (double)(KeyFrames[upper] - KeyFrames[lower]);
                using var distance = new Mat(); Cv2.AddWeighted(guides[lower], 1 - t, guides[upper], t, 0, distance);
                using var mask = new Mat(338, 402, MatType.CV_8UC1);
                for (var y = 0; y < 338; y++) for (var x = 0; x < 402; x++)
                {
                    var d = distance.At<float>(y, x);
                    // Confident original foreground and background, with a wide refinable contour band.
                    mask.Set(y, x, (byte)(d > 12 ? 1 : d < -22 ? 0 : d > 0 ? 3 : 2));
                }
                using var bgModel = new Mat(); using var fgModel = new Mat();
                Cv2.GrabCut(original, mask, default, bgModel, fgModel, 4, GrabCutModes.InitWithMask);
                using var solid = new Mat(338, 402, MatType.CV_8UC1);
                for (var y = 0; y < 338; y++) for (var x = 0; x < 402; x++)
                    solid.Set(y, x, (byte)((mask.At<byte>(y, x) & 1) == 1 ? 255 : 0));
                using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(3, 3));
                Cv2.MorphologyEx(solid, solid, MorphTypes.Close, kernel); Cv2.MedianBlur(solid, solid, 3);
                using var edge = new Mat(); Cv2.GaussianBlur(solid, edge, new Size(3, 3), 0.55);
                using var bgra = new Mat(); Cv2.CvtColor(original, bgra, ColorConversionCodes.BGR2BGRA);
                for (var y = 0; y < 338; y++) for (var x = 0; x < 402; x++)
                {
                    var pixel = bgra.At<Vec4b>(y, x);
                    // Keep original movie RGB; imagegen provides alpha guidance only.
                    pixel.Item3 = solid.At<byte>(y, x) == 255 ? edge.At<byte>(y, x) : (byte)0;
                    bgra.Set(y, x, pixel);
                }
                var pixels = new byte[402 * 338 * 4]; Marshal.Copy(bgra.Data, pixels, 0, pixels.Length);
                var bitmap = BitmapSource.Create(402, 338, 96, 96, PixelFormats.Bgra32, null, pixels, 402 * 4);
                var visual = new DrawingVisual(); RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
                // One fixed mapping across the entire GIF, retaining its original framing and motion.
                using (var dc = visual.RenderOpen()) dc.DrawImage(bitmap, new WpfRect(26, 83.23, 460, 386.77));
                var frame = new RenderTargetBitmap(512, 512, 96, 96, PixelFormats.Pbgra32); frame.Render(visual);
                frame.Freeze(); renderedFrames.Add(frame);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(frame));
                using (var stream = File.Create(Path.Combine(output, $"{i:D3}.png"))) encoder.Save(stream);
                if (KeyFrames.Contains(i))
                {
                    // Alternating light/dark backgrounds reveal brown halos and missing fingers.
                    using var backdrop = new Mat(338, 402, MatType.CV_8UC3, (i % 8 == 0) ? new Scalar(236, 242, 248) : new Scalar(55, 43, 35));
                    for (var y = 0; y < 338; y++) for (var x = 0; x < 402; x++)
                    {
                        var f = bgra.At<Vec4b>(y, x); var b = backdrop.At<Vec3b>(y, x); var a = f.Item3 / 255d;
                        backdrop.Set(y, x, new Vec3b((byte)(f.Item0 * a + b.Item0 * (1 - a)), (byte)(f.Item1 * a + b.Item1 * (1 - a)), (byte)(f.Item2 * a + b.Item2 * (1 - a))));
                    }
                    Cv2.ImWrite(Path.Combine(preview, $"preview-{i:D3}.png"), backdrop);
                }
                if (i <= 12 && solid.At<byte>(35, 40) < 128) throw new InvalidDataException($"Human hand missing in frame {i}");
                if (solid.At<byte>(20, 385) != 0) throw new InvalidDataException($"Background remains in frame {i}");
                report.Add(new { frame = i, durationMs = 100, opaquePixels = Cv2.CountNonZero(solid), handRetained = i > 12 || solid.At<byte>(35, 40) > 128 });
                Console.WriteLine($"Matted original frame {i + 1}/47");
            }
            var contact = new Mat(338 * 3, 402 * 4, MatType.CV_8UC3);
            for (var n = 0; n < KeyFrames.Length; n++)
            {
                using var source = Cv2.ImRead(Path.Combine(preview, $"preview-{KeyFrames[n]:D3}.png"));
                using var cell = new Mat(contact, new Rect(n % 4 * 402, n / 4 * 338, 402, 338)); source.CopyTo(cell);
            }
            Cv2.ImWrite(Path.Combine(preview, "cutout-contact-sheet.png"), contact); contact.Dispose();
            File.WriteAllText(Path.Combine(preview, "validation.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            SaveGif(renderedFrames, Path.Combine(root, "assets", "reference", "pet", "摸头-透明背景.gif"));
        }
        finally { foreach (var guide in guides) guide.Dispose(); }
    }
    static void SaveGif(List<BitmapSource> frames, string path, int fps = 10)
    {
        var colors = new List<Color> { Colors.Transparent };
        var paletteVisual = new DrawingVisual();
        using (var dc = paletteVisual.RenderOpen())
        {
            dc.DrawImage(frames[0], new WpfRect(0, 0, 512, 512));
            dc.DrawImage(frames[frames.Count / 3], new WpfRect(512, 0, 512, 512));
        }
        var paletteImage = new RenderTargetBitmap(1024, 512, 96, 96, PixelFormats.Pbgra32); paletteImage.Render(paletteVisual);
        colors.AddRange(new BitmapPalette(paletteImage, 255).Colors.Where(c => c.A > 0).Take(255));
        var palette = new BitmapPalette(colors);
        var lookup = new byte[32768];
        for (var key = 0; key < lookup.Length; key++)
        {
            var r = ((key >> 10) & 31) * 8 + 4; var g = ((key >> 5) & 31) * 8 + 4; var b = (key & 31) * 8 + 4;
            var best = int.MaxValue; var index = 1;
            for (var k = 1; k < colors.Count; k++)
            {
                var c = colors[k]; var error = (c.R - r) * (c.R - r) + (c.G - g) * (c.G - g) + (c.B - b) * (c.B - b);
                if (error < best) { best = error; index = k; }
            }
            lookup[key] = (byte)index;
        }
        var gif = new GifBitmapEncoder();
        foreach (var frame in frames)
        {
            var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            var rgba = new byte[512 * 512 * 4]; converted.CopyPixels(rgba, 2048, 0); var indexed = new byte[512 * 512];
            for (var i = 0; i < indexed.Length; i++)
            {
                var k = i * 4;
                indexed[i] = rgba[k + 3] < 128 ? (byte)0 : lookup[((rgba[k + 2] >> 3) << 10) | ((rgba[k + 1] >> 3) << 5) | (rgba[k] >> 3)];
            }
            var source = BitmapSource.Create(512, 512, 96, 96, PixelFormats.Indexed8, palette, indexed, 512);
            var metadata = new BitmapMetadata("gif");
            metadata.SetQuery("/grctlext/Delay", (ushort)10);
            metadata.SetQuery("/grctlext/Disposal", (byte)2);
            metadata.SetQuery("/grctlext/TransparencyFlag", true);
            metadata.SetQuery("/grctlext/TransparentColorIndex", (byte)0);
            gif.Frames.Add(BitmapFrame.Create(source, null, metadata, null));
        }
        using (var stream = File.Create(path)) gif.Save(stream);
        // WIC drops frame metadata on GIF encode. Write timing/disposal into the
        // actual GIF control blocks so independent viewers play at the source speed.
        var bytes = File.ReadAllBytes(path);
        var start = 13 + ((bytes[10] & 0x80) != 0 ? 3 * (1 << ((bytes[10] & 7) + 1)) : 0);
        var position = start; var controls = 0;
        while (position < bytes.Length)
        {
            var tag = bytes[position++];
            if (tag == 0x3b) break;
            if (tag == 0x21)
            {
                var label = bytes[position++];
                if (label == 0xf9)
                {
                    if (bytes[position] != 4) throw new InvalidDataException("Invalid GIF graphics control block.");
                    bytes[position + 1] = (byte)((bytes[position + 1] & 0xe0) | 9); // restore-to-background, transparent
                    var delay = (int)Math.Round((controls + 1) * 100d / fps) - (int)Math.Round(controls * 100d / fps);
                    bytes[position + 2] = (byte)delay; bytes[position + 3] = 0; bytes[position + 4] = 0;
                    controls++;
                }
            }
            else if (tag == 0x2c)
            {
                var packed = bytes[position + 8]; position += 9;
                if ((packed & 0x80) != 0) position += 3 * (1 << ((packed & 7) + 1));
                position++; // LZW minimum code size
            }
            else throw new InvalidDataException($"Unexpected GIF block: {tag:X2}");
            while (true)
            {
                var length = bytes[position++];
                if (length == 0) break;
                position += length;
            }
        }
        if (controls != frames.Count) throw new InvalidDataException("GIF frame controls are incomplete.");
        byte[] loop = [0x21, 0xff, 11, 78, 69, 84, 83, 67, 65, 80, 69, 50, 46, 48, 3, 1, 0, 0, 0];
        File.WriteAllBytes(path, bytes.Take(start).Concat(loop).Concat(bytes.Skip(start)).ToArray());
    }
}
