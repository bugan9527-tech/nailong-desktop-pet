using System;
using System.IO;
using System.Text.Json;

namespace Nailong.Desktop;
public sealed class Settings
{
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NailongDesktopPet");
    public double? Left { get; set; }
    public double? Top { get; set; }
    public int PetHeight { get; set; } = 240;
    public bool Automatic { get; set; } = true;
    public bool OnTop { get; set; } = true;
    public static Settings Load(string? folder = null)
    {
        try
        {
            var value = JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path.Combine(folder ?? Folder, "settings.json"))) ?? new Settings();
            if (value.PetHeight is not (160 or 240 or 320)) value.PetHeight = 240;
            if (value.Left.HasValue && !double.IsFinite(value.Left.Value)) value.Left = null;
            if (value.Top.HasValue && !double.IsFinite(value.Top.Value)) value.Top = null;
            return value;
        }
        catch { return new Settings(); }
    }
    public void Save(string? folder = null)
    {
        try
        {
            var destination = folder ?? Folder;
            Directory.CreateDirectory(destination);
            var path = Path.Combine(destination, "settings.json");
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(path + ".tmp", path, true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
