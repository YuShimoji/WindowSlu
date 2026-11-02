using System.IO;
using WindowSlu.Models;

namespace WindowSlu.Services;

public class SettingsService
{
    private readonly string _settingsFilePath;
    public ApplicationSettings Settings { get; private set; }
    public string? LastError { get; private set; }

    public SettingsService(string? settingsFilePath = null)
    {
        _settingsFilePath = settingsFilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowSlu", "settings.json");
        Settings = JsonFileStore.Load<ApplicationSettings>(_settingsFilePath, out var error) ?? new ApplicationSettings();
        LastError = error;
        Settings.HotkeySettings ??= new();
        var missing = new ApplicationSettings().HotkeySettings
            .Where(defaultKey => !Settings.HotkeySettings.Any(key => key.Action == defaultKey.Action)).ToList();
        Settings.HotkeySettings.AddRange(missing);
        if (error == null && (!File.Exists(_settingsFilePath) || missing.Count > 0)) SaveSettings();
    }

    public bool SaveSettings()
    {
        bool saved = JsonFileStore.Save(_settingsFilePath, Settings, out var error);
        LastError = error;
        return saved;
    }
}
