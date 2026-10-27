using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Nailong.Core;

namespace Nailong.Desktop;
public sealed class AnimationLibrary
{
    public sealed class Entry
    {
        public string Id { get; set; } = "";
        public string[] Frames { get; set; } = [];
        public double Fps { get; set; }
        public bool Loop { get; set; }
    }
    public sealed class Frame(BitmapSource image, byte[] pixels)
    {
        public BitmapSource Image { get; } = image;
        public bool Hit(double x, double y)
        {
            var px = (int)(x * Image.PixelWidth); var py = (int)(y * Image.PixelHeight);
            return px >= 0 && px < Image.PixelWidth && py >= 0 && py < Image.PixelHeight && pixels[py * Image.PixelWidth + px] >= 32;
        }
    }
    public Dictionary<string, Clip> Clips { get; } = new();
    public Dictionary<string, Frame[]> Frames { get; } = new();
    public AnimationLibrary()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "assets", "animations");
        var entries = JsonSerializer.Deserialize<Entry[]>(File.ReadAllText(Path.Combine(root, "animations.json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        foreach (var e in entries)
        {
            if (string.IsNullOrWhiteSpace(e.Id) || e.Frames.Length == 0 || e.Frames.Length > 240 || e.Fps <= 0 || e.Fps > 60 || !double.IsFinite(e.Fps)) throw new InvalidDataException("动作清单格式不正确。");
            var frames = e.Frames.Select(file =>
            {
                var path = Path.GetFullPath(Path.Combine(root, file));
                if (!path.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("动作帧路径超出素材目录。");
                var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.UriSource = new Uri(path); bitmap.EndInit(); bitmap.Freeze();
                if (bitmap.PixelWidth != 512 || bitmap.PixelHeight != 512) throw new InvalidDataException("动作帧必须为 512×512。");
                var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0); converted.Freeze();
                var pixels = new byte[512 * 512 * 4]; converted.CopyPixels(pixels, 512 * 4, 0);
                var display = BitmapSource.Create(512, 512, 96, 96, PixelFormats.Bgra32, null, pixels, 2048); display.Freeze();
                var alpha = new byte[512 * 512];
                for (var i = 0; i < alpha.Length; i++) alpha[i] = pixels[i * 4 + 3];
                return new Frame(display, alpha);
            }).ToArray();
            Clips.Add(e.Id, new Clip(e.Id, frames.Length, e.Fps, e.Loop)); Frames.Add(e.Id, frames);
        }
        _ = new AnimationController(Clips);
        if (!Clips["idle"].Loop || new[] { "blink", "wave", "tilt", "pet", "belly" }.Any(id => Clips[id].Loop)) throw new InvalidDataException("待机必须循环，其余基础动作须单次播放。");
    }
}
