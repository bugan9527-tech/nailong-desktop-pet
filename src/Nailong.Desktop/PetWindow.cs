using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Nailong.Core;
using Forms = System.Windows.Forms;

namespace Nailong.Desktop;
public sealed class PetWindow : Window
{
    readonly AnimationLibrary library;
    readonly Settings settings;
    readonly bool persist;
    readonly AnimationController controller;
    readonly DragGesture gesture = new();
    readonly Image image = new() { Stretch = Stretch.Fill, SnapsToDevicePixels = false };
    readonly Image previousImage = new() { Stretch = Stretch.Fill, IsHitTestVisible = false, Opacity = 0 };
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly DispatcherTimer timer;
    readonly Forms.NotifyIcon tray;
    readonly System.Drawing.Icon trayIcon;
    AnimationLibrary.Frame? frame;
    string lastAction = "";
    int lastFrame = -1;
    Native.Rect dragOrigin;
    bool quitting;
    string pressedAction = "wave";
    double transitionStarted;

    public PetWindow(Settings? testSettings = null, AnimationLibrary? sharedLibrary = null)
    {
        library = sharedLibrary ?? new AnimationLibrary();
        settings = testSettings ?? Settings.Load(); persist = testSettings is null;
        controller = new AnimationController(library.Clips);
        controller.SetAutomatic(settings.Automatic, clock.Elapsed.TotalMilliseconds);
        Title = "奶龙桌宠";
        WindowStyle = WindowStyle.None; AllowsTransparency = true;
        Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; ShowActivated = false; Topmost = settings.OnTop;
        Width = Height = settings.PetHeight * 512.0 / 420;
        var canvas = new Grid(); canvas.Children.Add(image); canvas.Children.Add(previousImage);
        Content = canvas; Cursor = Cursors.Hand;
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        RenderOptions.SetBitmapScalingMode(previousImage, BitmapScalingMode.HighQuality);
        SourceInitialized += (_, _) => HwndSource.FromHwnd(Native.Handle(this))?.AddHook(WindowHook);
        Loaded += (_, _) =>
        {
            var work = (Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0]).WorkingArea;
            var bounds = Native.Bounds(this);
            Native.Move(this, (int)(settings.Left ?? work.Right - bounds.Width - 24), (int)(settings.Top ?? work.Bottom - bounds.Height - 12));
            Native.Clamp(this); SavePosition();
        };
        MouseLeftButtonDown += OnPointerDown;
        MouseMove += OnPointerMove;
        MouseLeftButtonUp += OnPointerUp;
        LostMouseCapture += (_, _) => { gesture.Cancel(); controller.Suspended = false; };
        MouseRightButtonUp += (_, e) =>
        {
            if (!OnCharacter(e.GetPosition(image))) return;
            var menu = BuildMenu(); controller.Suspended = true;
            menu.Closed += (_, _) => controller.Suspended = false;
            menu.PlacementTarget = image; menu.IsOpen = true; e.Handled = true;
        };

        var iconPath = Path.Combine(AppContext.BaseDirectory, "assets", "animations", "nailong.ico");
        trayIcon = new System.Drawing.Icon(iconPath);
        tray = new Forms.NotifyIcon { Icon = trayIcon, Text = "奶龙桌宠 · 右键菜单", Visible = true };
        var trayMenu = new Forms.ContextMenuStrip();
        trayMenu.Items.Add("显示奶龙", null, (_, _) => Dispatcher.BeginInvoke(() => ShowPet()));
        trayMenu.Items.Add("隐藏奶龙", null, (_, _) => Dispatcher.BeginInvoke(() => HidePet()));
        trayMenu.Items.Add(new Forms.ToolStripSeparator());
        trayMenu.Items.Add("退出", null, (_, _) => Dispatcher.BeginInvoke(() => Quit()));
        tray.ContextMenuStrip = trayMenu;
        tray.DoubleClick += (_, _) => Dispatcher.BeginInvoke(() => ShowPet());
        timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(1000.0 / 30) };
        timer.Tick += (_, _) => RenderFrame();
        IsVisibleChanged += (_, _) => { if (IsVisible) timer.Start(); else timer.Stop(); };
        RenderFrame();
    }

    void RenderFrame()
    {
        var now = clock.Elapsed.TotalMilliseconds;
        var index = controller.Tick(now);
        if (lastAction != controller.Current && image.Source is not null) BeginTransition(now);
        if (previousImage.Source is not null)
        {
            previousImage.Opacity = 1 - PetMotion.Ease(0, 140, now - transitionStarted);
            if (previousImage.Opacity <= 0) previousImage.Source = null;
        }
        if (lastAction == controller.Current && lastFrame == index) return;
        lastAction = controller.Current; lastFrame = index;
        frame = library.Frames[controller.Current][index]; image.Source = frame.Image;
    }
    void BeginTransition(double now)
    {
        previousImage.Source = image.Source; previousImage.Opacity = 1; transitionStarted = now;
    }
    bool HitCharacter(double x, double y)
    {
        // The incoming human hand is not interactive. Below it, follow the actual turning silhouette.
        if (controller.Current == "pet" && y < .18) return library.Frames["idle"][0].Hit(x, y);
        return frame?.Hit(x, y) == true;
    }
    bool OnCharacter(System.Windows.Point point) => HitCharacter(point.X / Math.Max(1, image.ActualWidth), point.Y / Math.Max(1, image.ActualHeight));
    void OnPointerDown(object sender, MouseButtonEventArgs e)
    {
        if (!OnCharacter(e.GetPosition(image))) return;
        Native.GetCursorPos(out var point); dragOrigin = Native.Bounds(this);
        var local = e.GetPosition(image);
        pressedAction = PetInteraction.ActionAt(local.X / image.ActualWidth, local.Y / image.ActualHeight, controller.Current);
        gesture.Begin(point.X, point.Y); controller.Suspended = true;
        CaptureMouse(); e.Handled = true;
    }
    void OnPointerMove(object sender, MouseEventArgs e)
    {
        if (!gesture.IsDown) return;
        Native.GetCursorPos(out var p);
        var delta = gesture.Move(p.X, p.Y, 6 * Native.GetDpiForWindow(Native.Handle(this)) / 96.0);
        if (gesture.IsDragging) Native.Move(this, dragOrigin.Left + (int)delta.X, dragOrigin.Top + (int)delta.Y);
    }
    void OnPointerUp(object sender, MouseButtonEventArgs e)
    {
        if (!gesture.IsDown) return;
        OnPointerMove(sender, e);
        var click = gesture.End();
        ReleaseMouseCapture(); controller.Suspended = false;
        Native.Clamp(this); SavePosition();
        if (click) Play(pressedAction);
        e.Handled = true;
    }
    internal void Play(string id)
    {
        if (id is "pet" or "belly" && controller.Current == id) return;
        var now = clock.Elapsed.TotalMilliseconds;
        if (image.Source is not null) BeginTransition(now);
        controller.Play(id, now); RenderFrame();
    }
    internal void ClickCharacterAt(double x, double y)
    {
        if (HitCharacter(x, y)) Play(PetInteraction.ActionAt(x, y, controller.Current));
    }
    internal string CurrentAction => controller.Current;
    internal bool Automatic => settings.Automatic;
    internal int CurrentFrame => lastFrame;
    internal double TransitionOpacity => previousImage.Opacity;
    internal void SetAutomatic(bool value)
    {
        settings.Automatic = value; controller.SetAutomatic(value, clock.Elapsed.TotalMilliseconds); Save();
    }
    internal void SetTopmost(bool value) { Topmost = value; settings.OnTop = value; Save(); }
    internal void ResizePet(int height)
    {
        var old = Native.Bounds(this);
        settings.PetHeight = height; Width = Height = height * 512.0 / 420;
        UpdateLayout();
        var updated = Native.Bounds(this);
        Native.Move(this, old.Left + (old.Width - updated.Width) / 2, old.Bottom - updated.Height);
        Native.Clamp(this); SavePosition();
    }
    internal void ShowPet()
    {
        Show(); controller.Play("idle", clock.Elapsed.TotalMilliseconds); RenderFrame(); Native.Clamp(this);
    }
    internal void HidePet() { SavePosition(); Hide(); }
    internal void Quit() { quitting = true; SavePosition(); Close(); Application.Current.Shutdown(); }
    void SavePosition()
    {
        var bounds = Native.Bounds(this); settings.Left = bounds.Left; settings.Top = bounds.Top; Save();
    }
    void Save() { if (persist) settings.Save(); }

    internal ContextMenu BuildMenu()
    {
        var menu = new ContextMenu
        {
            FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 14,
            Background = new SolidColorBrush(Color.FromRgb(255, 251, 240)),
            Foreground = new SolidColorBrush(Color.FromRgb(66, 48, 27)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(232, 209, 160)),
            Padding = new Thickness(5), MinWidth = 180
        };
        MenuItem Item(string label, Action action, bool? check = null)
        {
            var item = new MenuItem { Header = label, MinHeight = 32, Padding = new Thickness(8, 4, 12, 4) };
            if (check.HasValue) { item.IsCheckable = true; item.IsChecked = check.Value; }
            item.Click += (_, _) => action(); return item;
        }
        var actions = new MenuItem { Header = "播放动作", MinHeight = 32 };
        actions.Items.Add(Item("待机呼吸", () => Play("idle")));
        actions.Items.Add(Item("眨眨眼", () => Play("blink")));
        actions.Items.Add(Item("挥挥手", () => Play("wave")));
        actions.Items.Add(Item("歪歪头", () => Play("tilt")));
        actions.Items.Add(Item("摸摸头", () => Play("pet")));
        actions.Items.Add(Item("收手护肚", () => Play("belly")));
        menu.Items.Add(actions);
        menu.Items.Add(Item("自动小动作", () => SetAutomatic(!settings.Automatic), settings.Automatic));
        var sizes = new MenuItem { Header = "角色大小", MinHeight = 32 };
        foreach (var h in new[] { 160, 240, 320 })
        {
            var value = h; sizes.Items.Add(Item($"{h} 像素", () => ResizePet(value), settings.PetHeight == value));
        }
        menu.Items.Add(sizes);
        menu.Items.Add(Item("始终置顶", () => SetTopmost(!Topmost), Topmost));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("隐藏到托盘", HidePet)); menu.Items.Add(Item("退出", Quit));
        return menu;
    }
    IntPtr WindowHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg is 0x007E or 0x02E0) // Display topology or per-monitor DPI changed.
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => { Native.Clamp(this); SavePosition(); }));
        if (msg == 0x0084 && !gesture.IsDown && frame is not null) // WM_NCHITTEST
        {
            var x = (short)(lParam.ToInt64() & 0xffff); var y = (short)((lParam.ToInt64() >> 16) & 0xffff);
            var b = Native.Bounds(this);
            if (!HitCharacter((x - b.Left) / (double)Math.Max(1, b.Width), (y - b.Top) / (double)Math.Max(1, b.Height)))
            { handled = true; return new IntPtr(-1); }
        }
        return IntPtr.Zero;
    }
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!quitting) { e.Cancel = true; HidePet(); }
        base.OnClosing(e);
    }
    protected override void OnClosed(EventArgs e)
    {
        timer.Stop(); tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Dispose(); trayIcon.Dispose();
        base.OnClosed(e);
    }
    internal void CloseForTest() { quitting = true; Close(); }
}
