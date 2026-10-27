using System;
using System.IO;
using System.Threading;
using System.Windows;

namespace Nailong.Desktop;
public partial class App : Application
{
    Mutex? instance;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var smoke = Array.IndexOf(e.Args, "--smoke-test");
        if (smoke >= 0)
        {
            var output = Path.GetFullPath(e.Args.Length > smoke + 1 ? e.Args[smoke + 1] : "artifacts");
            try { await SmokeTest.RunAsync(output); Shutdown(); }
            catch (Exception ex)
            {
                Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "smoke-error.txt"), ex.ToString()); Shutdown(1);
            }
            return;
        }
        instance = new Mutex(true, "Local\\Nailong.Desktop.v1", out var created);
        if (!created) { instance.Dispose(); instance = null; Shutdown(); return; }
        DispatcherUnhandledException += (_, args) =>
        {
            Report(args.Exception); args.Handled = true; Shutdown(1);
        };
        try
        {
            MainWindow = new PetWindow(); MainWindow.Show();
        }
        catch (Exception ex) { Report(ex); Shutdown(1); }
    }
    static void Report(Exception ex)
    {
        try { Directory.CreateDirectory(Settings.Folder); File.WriteAllText(Path.Combine(Settings.Folder, "last-error.txt"), ex.ToString()); } catch { }
        MessageBox.Show("桌宠未能正常运行：\n" + ex.Message + "\n\n请确认 assets 文件夹与 Nailong.exe 放在一起。", "奶龙桌宠", MessageBoxButton.OK, MessageBoxImage.Error);
    }
    protected override void OnExit(ExitEventArgs e)
    {
        if (instance is not null) { instance.ReleaseMutex(); instance.Dispose(); }
        base.OnExit(e);
    }
}
