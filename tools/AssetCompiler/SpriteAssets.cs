using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class SpriteAssets
{
    internal static BitmapSource Load(string path)
    {
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(Path.GetFullPath(path)); bitmap.EndInit(); bitmap.Freeze(); return bitmap;
    }
    internal static byte[] Pixels(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
        var pixels = new byte[source.PixelWidth * source.PixelHeight * 4]; converted.CopyPixels(pixels, source.PixelWidth * 4, 0); return pixels;
    }
    internal static BitmapSource FromPixels(byte[] pixels, int width = 512, int height = 512)
    {
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, pixels, width * 4); bitmap.Freeze(); return bitmap;
    }
    internal static void Save(BitmapSource source, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source)); using var stream = File.Create(path); encoder.Save(stream);
    }
    internal static BitmapSource[] Split(string path, int columns, int rows)
    {
        var sheet = Load(path); var frames = new BitmapSource[columns * rows];
        for (var i = 0; i < frames.Length; i++)
        {
            var x = (int)Math.Round(i % columns * sheet.PixelWidth / (double)columns);
            var y = (int)Math.Round(i / columns * sheet.PixelHeight / (double)rows);
            var right = (int)Math.Round((i % columns + 1) * sheet.PixelWidth / (double)columns);
            var bottom = (int)Math.Round((i / columns + 1) * sheet.PixelHeight / (double)rows);
            frames[i] = Normalize(new CroppedBitmap(sheet, new Int32Rect(x, y, right - x, bottom - y)));
        }
        return frames;
    }
    internal static BitmapSource Normalize(BitmapSource source)
    {
        var pixels = Pixels(source); var w = source.PixelWidth; var h = source.PixelHeight;
        // Keep the main connected figure, then retain a two-pixel antialiased fringe.
        var labels = new int[w * h]; var queue = new Queue<int>(); var sizes = new List<int> { 0 };
        for (var p = 0; p < labels.Length; p++)
        {
            if (labels[p] != 0 || pixels[p * 4 + 3] < 32) continue;
            var label = sizes.Count; var size = 0; labels[p] = label; queue.Enqueue(p);
            while (queue.TryDequeue(out var q))
            {
                size++;
                foreach (var n in new[] { q - w, q + w, q % w > 0 ? q - 1 : -1, q % w < w - 1 ? q + 1 : -1 })
                    if (n >= 0 && n < labels.Length && labels[n] == 0 && pixels[n * 4 + 3] >= 32) { labels[n] = label; queue.Enqueue(n); }
            }
            sizes.Add(size);
        }
        var largest = sizes.IndexOf(sizes.Max()); var keep = new bool[w * h];
        for (var p = 0; p < labels.Length; p++) if (labels[p] == largest)
            for (var dy = -2; dy <= 2; dy++) for (var dx = -2; dx <= 2; dx++)
            { var x = p % w + dx; var y = p / w + dy; if (x >= 0 && x < w && y >= 0 && y < h) keep[y * w + x] = true; }
        for (var p = 0; p < keep.Length; p++) if (!keep[p]) Array.Clear(pixels, p * 4, 4);
        var left = w; var right = 0; var top = h; var bottom = 0;
        for (var y = 0; y < h; y++) for (var x = 0; x < w; x++)
        {
            var k = (y * w + x) * 4;
            if (pixels[k + 3] < 8) Array.Clear(pixels, k, 4);
            if (pixels[k + 3] < 32) continue;
            left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y);
        }
        if (bottom - top < h / 2) throw new InvalidDataException("Missing full-body sprite.");
        var footLeft = w; var footRight = 0;
        for (var y = bottom - (bottom - top) / 15; y <= bottom; y++) for (var x = left; x <= right; x++)
            if (pixels[(y * w + x) * 4 + 3] >= 128) { footLeft = Math.Min(footLeft, x); footRight = Math.Max(footRight, x); }
        var scale = 420d / (bottom - top + 1); var center = (footLeft + footRight + 1) / 2d;
        var clean = FromPixels(pixels, w, h); var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using (var dc = visual.RenderOpen()) dc.DrawImage(new CroppedBitmap(clean, new Int32Rect(left, top, right - left + 1, bottom - top + 1)),
            new Rect(256 + (left - center) * scale, 50, (right - left + 1) * scale, 420));
        var render = new RenderTargetBitmap(512, 512, 96, 96, PixelFormats.Pbgra32); render.Render(visual);
        var output = Pixels(render); for (var k = 0; k < output.Length; k += 4) if (output[k + 3] < 8) Array.Clear(output, k, 4);
        return FromPixels(output);
    }
    internal static BitmapSource Breathe(BitmapSource source, double phase)
    {
        var original = Pixels(source); var output = new byte[original.Length];
        var amount = 0.006 * (0.5 - 0.5 * Math.Cos(phase * Math.PI * 2));
        for (var y = 0; y < 512; y++)
        {
            var weight = Nailong.Core.PetMotion.Ease(140, 240, y) * (1 - Nailong.Core.PetMotion.Ease(380, 430, y));
            for (var x = 0; x < 512; x++)
            {
                var sx = Math.Clamp(256 + (x - 256) / (1 + amount * weight), 0, 511);
                var a = (int)sx; var b = Math.Min(511, a + 1); var t = sx - a;
                for (var c = 0; c < 4; c++) output[(y * 512 + x) * 4 + c] = (byte)Math.Round(original[(y * 512 + a) * 4 + c] * (1 - t) + original[(y * 512 + b) * 4 + c] * t);
            }
        }
        return FromPixels(output);
    }
    internal static BitmapSource Patch(BitmapSource source, BitmapSource replacement, Func<int, int, double> mask)
    {
        var a = Pixels(source); var b = Pixels(replacement);
        for (var y = 0; y < 512; y++) for (var x = 0; x < 512; x++)
        {
            var t = Math.Clamp(mask(x, y), 0, 1); var k = (y * 512 + x) * 4;
            for (var c = 0; c < 4; c++) a[k + c] = (byte)Math.Round(a[k + c] * (1 - t) + b[k + c] * t);
        }
        return FromPixels(a);
    }
    internal static BitmapSource HeadWarp(BitmapSource source, double tilt, double press = 0)
    {
        var original = Pixels(source); var output = new byte[original.Length];
        for (var y = 0; y < 512; y++) for (var x = 0; x < 512; x++)
        {
            var weight = 1 - Nailong.Core.PetMotion.Ease(160, 255, y);
            var angle = tilt * Math.PI / 180 * weight;
            var sx = 256 + (x - 256) * Math.Cos(angle) + (y - 215) * Math.Sin(angle);
            var sy = 215 - (x - 256) * Math.Sin(angle) + (y - 215) * Math.Cos(angle) - press * weight;
            var ax = (int)Math.Floor(sx); var ay = (int)Math.Floor(sy); var fx = sx - ax; var fy = sy - ay;
            if (ax < 0 || ax >= 511 || ay < 0 || ay >= 511) continue;
            for (var c = 0; c < 4; c++) output[(y * 512 + x) * 4 + c] = (byte)Math.Round(
                original[(ay * 512 + ax) * 4 + c] * (1 - fx) * (1 - fy) + original[(ay * 512 + ax + 1) * 4 + c] * fx * (1 - fy) +
                original[((ay + 1) * 512 + ax) * 4 + c] * (1 - fx) * fy + original[((ay + 1) * 512 + ax + 1) * 4 + c] * fx * fy);
        }
        return FromPixels(output);
    }
}
