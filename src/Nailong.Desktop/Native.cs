using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace Nailong.Desktop;
internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; public int Width => Right - Left; public int Height => Bottom - Top; }
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr handle, out Rect rect);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr handle);
    public static IntPtr Handle(Window window) => new WindowInteropHelper(window).Handle;
    public static void Move(Window window, int x, int y) => SetWindowPos(Handle(window), IntPtr.Zero, x, y, 0, 0, 0x0015);
    public static Rect Bounds(Window window) { GetWindowRect(Handle(window), out var r); return r; }
    public static void Clamp(Window window)
    {
        var r = Bounds(window);
        var screen = Forms.Screen.FromRectangle(new System.Drawing.Rectangle(r.Left, r.Top, r.Width, r.Height));
        var work = screen.WorkingArea;
        Move(window, Math.Clamp(r.Left, work.Left, Math.Max(work.Left, work.Right - r.Width)), Math.Clamp(r.Top, work.Top, Math.Max(work.Top, work.Bottom - r.Height)));
    }
}
