using System.Text.Json;
using System.IO;
using TaskbarMonitor.Core.Settings;

namespace TaskbarMonitor.Windows;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _path;
    private readonly string? _importPath;

    public JsonSettingsStore() : this(AppDataPaths.SettingsFile, AppDataPaths.IsPortable ? Path.Combine(AppDataPaths.LegacyDataDirectory, "settings.json") : null) { }
    public JsonSettingsStore(string path, string? importPath = null) { _path = path; _importPath = importPath; }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_path)) return Read(_path);
            var settings = _importPath is not null && File.Exists(_importPath) ? Read(_importPath) : new AppSettings();
            // A portable copy imports existing preferences once, then owns its own settings file.
            if (_importPath is not null)
            {
                try { Save(settings); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
            return settings;
        }
        catch (JsonException) { return new AppSettings(); }
        catch (IOException) { return new AppSettings(); }
        catch (UnauthorizedAccessException) { return new AppSettings(); }
    }

    private static AppSettings Read(string path) => JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings();

    public void Save(AppSettings settings)
    {
        var folder = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(folder);
        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, Options));
        File.Move(temporary, _path, true);
    }
}
