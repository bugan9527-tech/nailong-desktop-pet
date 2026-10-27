using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;
using Nailong.Core;

namespace Nailong.Desktop;
internal static class SmokeTest
{
    internal static async Task RunAsync(string output)
    {
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        void Check(bool pass, string text) { if (!pass) throw new Exception(text); checks.Add(text); }
        var library = new AnimationLibrary();
        Check(library.Frames.Count == 6 && new[] { "idle", "blink", "wave", "tilt" }.All(id => library.Frames[id].Length == 8), "six clips load from published assets");
        Check(library.Frames["belly"].Length == BellyMotion.FrameCount && library.Clips["belly"].Duration == BellyMotion.Duration && !library.Clips["belly"].Loop, "belly reaction loads 78 frames at 30 fps for 2.6 seconds");
        Check(library.Frames["pet"].Length == 174 && library.Clips["pet"].Duration == 5800, "reaction plays 174 full-body frames over 5.8 seconds");
        Check(library.Frames["pet"][32].Hit(0.10, 0.12) && !library.Frames["pet"][32].Hit(0.94, 0.2), "human hand remains visible while background is transparent");
        static byte[] Pixels(BitmapSource image)
        {
            var pixels = new byte[512 * 512 * 4]; image.CopyPixels(pixels, 2048, 0); return pixels;
        }
        var neutral = Pixels(library.Frames["idle"][0].Image);
        Check(Pixels(library.Frames["pet"][0].Image).SequenceEqual(neutral) && Pixels(library.Frames["pet"][^1].Image).SequenceEqual(neutral), "petting begins and ends with the exact idle artwork");
        Check(Pixels(library.Frames["belly"][0].Image).SequenceEqual(neutral) && Pixels(library.Frames["belly"][^1].Image).SequenceEqual(neutral), "belly reaction begins and ends at the exact idle artwork");
        Check(Pixels(library.Frames["belly"][30].Image).AsSpan(430 * 2048).SequenceEqual(neutral.AsSpan(430 * 2048)), "belly reaction keeps both feet planted");
        Check(!Pixels(library.Frames["pet"][90].Image).AsSpan(320 * 2048).SequenceEqual(neutral.AsSpan(320 * 2048)), "body and feet change naturally during the right turn");
        Check(library.Frames["idle"][0].Hit(0.5, 0.5) && !library.Frames["idle"][0].Hit(0, 0), "character alpha mask distinguishes body and transparent margin");
        var settingsFolder = Path.Combine(output, "settings-test");
        new Settings { Left = -500, Top = 80, PetHeight = 160, Automatic = false, OnTop = false }.Save(settingsFolder);
        var restored = Settings.Load(settingsFolder);
        Check(restored.Left == -500 && restored.Top == 80 && restored.PetHeight == 160 && !restored.Automatic && !restored.OnTop, "position, size and switches persist across settings reload");
        File.WriteAllText(Path.Combine(settingsFolder, "settings.json"), "broken json");
        Check(Settings.Load(settingsFolder).PetHeight == 240, "damaged settings recover to safe defaults");
        var settings = new Settings { Automatic = false, Left = 100, Top = 100 };
        var window = new PetWindow(settings, library);
        try
        {
            window.Show(); await Task.Delay(250);
            Check(window.AllowsTransparency && window.WindowStyle == WindowStyle.None && !window.ShowInTaskbar, "native transparent borderless window without taskbar entry");
            Check(Native.Bounds(window).Width > 200, "visible native window created");
            window.ClickCharacterAt(0.01, 0.01);
            Check(window.CurrentAction == "idle", "transparent margin cannot trigger a pet action");
            window.ClickCharacterAt(0.5, 0.23);
            Check(window.CurrentAction == "pet", "clicking the standing head plays coherent full-body petting");
            Check(window.TransitionOpacity > 0.9, "action switches start with a short transition from the visible frame");
            await Task.Delay(180);
            Check(window.TransitionOpacity == 0, "transition completes without leaving a ghost image");
            await Task.Delay(820);
            var petSnap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); petSnap.Render(window);
            var petEncoder = new PngBitmapEncoder(); petEncoder.Frames.Add(BitmapFrame.Create(petSnap));
            using (var stream = File.Create(Path.Combine(output, "pet-window-preview.png"))) petEncoder.Save(stream);
            var beforeTap = window.CurrentFrame;
            window.ClickCharacterAt(0.5, 0.23);
            Check(window.CurrentFrame >= beforeTap, "repeated head click does not restart the hand approach");
            window.ClickCharacterAt(0.10, 0.12);
            Check(window.CurrentAction == "pet", "incoming human hand does not trigger a body click");
            await Task.Delay(2000);
            var sideFrame = window.CurrentFrame;
            window.ClickCharacterAt(0.60, 0.23);
            Check(window.CurrentAction == "pet" && window.CurrentFrame >= sideFrame && sideFrame >= 85, "repeated click on the turned head preserves side hold");
            await Task.Delay(2600);
            Check(window.CurrentAction == "pet" && window.CurrentFrame >= 165, "real renderer is still playing the return near 5.6 seconds");
            await Task.Delay(400);
            Check(window.CurrentAction == "idle", "petting finishes and returns to standing idle");
            window.ClickCharacterAt(0.5, 0.65);
            Check(window.CurrentAction == "belly", "clicking belly plays hands-to-belly reaction");
            await Task.Delay(1100);
            var bellyFrame = window.CurrentFrame;
            Check(window.CurrentAction == "belly" && bellyFrame >= 30, "real renderer reaches the hands-on-belly held pose");
            var bellySnap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bellySnap.Render(window);
            var bellyEncoder = new PngBitmapEncoder(); bellyEncoder.Frames.Add(BitmapFrame.Create(bellySnap));
            using (var stream = File.Create(Path.Combine(output, "belly-window-preview.png"))) bellyEncoder.Save(stream);
            window.ClickCharacterAt(0.5, 0.65);
            Check(window.CurrentFrame >= bellyFrame && window.TransitionOpacity == 0, "repeated belly clicks preserve playback without a new crossfade");
            await Task.Delay(1750);
            Check(window.CurrentAction == "idle", "belly reaction releases hands and returns to idle without queued repeats");
            window.ClickCharacterAt(0.24, 0.55);
            Check(window.CurrentAction == "wave", "clicking an arm still waves");
            window.Play("wave"); Check(window.CurrentAction == "wave", "manual wave starts");
            await Task.Delay(430);
            var snap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            snap.Render(window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(snap));
            using (var stream = File.Create(Path.Combine(output, "window-preview.png"))) encoder.Save(stream);
            await Task.Delay(950); Check(window.CurrentAction == "idle", "real dispatcher timer finishes wave and returns to idle");
            window.Play("blink"); await Task.Delay(800); Check(window.CurrentAction == "idle", "blink completes on real renderer");
            window.Play("tilt"); await Task.Delay(1450); Check(window.CurrentAction == "idle", "tilt completes on real renderer");
            var menu = window.BuildMenu();
            var actionMenu = (MenuItem)menu.Items[0];
            ((MenuItem)actionMenu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Check(window.CurrentAction == "idle", "idle action menu works");
            ((MenuItem)actionMenu.Items[1]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Check(window.CurrentAction == "blink", "blink action menu works");
            ((MenuItem)actionMenu.Items[3]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Check(window.CurrentAction == "tilt", "tilt action menu works");
            ((MenuItem)actionMenu.Items[2]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Check(window.CurrentAction == "wave", "right-click action menu dispatches wave");
            ((MenuItem)actionMenu.Items[4]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Check(window.CurrentAction == "pet", "menu can also preview the petting clip");
            ((MenuItem)actionMenu.Items[5]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Check(window.CurrentAction == "belly", "menu previews hands-to-belly reaction");
            ((MenuItem)menu.Items[1]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Check(window.Automatic, "menu toggles automatic behavior"); window.SetAutomatic(false);
            window.ResizePet(160); await Task.Delay(150); var small = Native.Bounds(window).Width;
            window.ResizePet(320); await Task.Delay(150); Check(Native.Bounds(window).Width > small * 1.8, "small and large sizes update native window");
            window.SetTopmost(false); Check(!window.Topmost, "topmost can be disabled"); window.SetTopmost(true);
            window.HidePet(); Check(!window.IsVisible, "hide removes pet window"); window.ShowPet(); Check(window.IsVisible, "restore shows pet window");
            Native.Move(window, -30000, -30000); Native.Clamp(window);
            var b = Native.Bounds(window);
            Check(Forms.Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(new System.Drawing.Rectangle(b.Left, b.Top, b.Width, b.Height))), "off-screen position recovers into connected display work area");
            File.WriteAllText(Path.Combine(output, "smoke-test.json"), JsonSerializer.Serialize(new { success = true, checks, screens = Forms.Screen.AllScreens.Select(s => new { s.DeviceName, s.WorkingArea }), dpi = Native.GetDpiForWindow(Native.Handle(window)) }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { window.CloseForTest(); }
    }
}
