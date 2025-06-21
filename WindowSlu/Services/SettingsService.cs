using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Input;
using WindowSlu.Models;

namespace WindowSlu.Services
{
    public class SettingsService
    {
        private readonly string _settingsFilePath;
        public ApplicationSettings Settings { get; private set; }

        public SettingsService()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolderPath = Path.Combine(appDataPath, "WindowSlu");
            Directory.CreateDirectory(appFolderPath);
            _settingsFilePath = Path.Combine(appFolderPath, "settings.json");

            Settings = LoadSettings();
        }

        private ApplicationSettings LoadSettings()
        {
            if (File.Exists(_settingsFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<ApplicationSettings>(json);
                    return settings ?? new ApplicationSettings();
                }
                catch (JsonException)
                {
                    // If file is corrupted, return default settings
                    return new ApplicationSettings();
                }
                catch (IOException)
                {
                    // If file is inaccessible, return default settings
                    return new ApplicationSettings();
                }
            }
            return new ApplicationSettings();
        }

        public void SaveSettings()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(Settings, options);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (IOException)
            {
                // Handle exceptions during file save (e.g., access denied)
            }
        }

        private HotkeySetting[] GetDefaultHotkeySettings()
        {
            return new HotkeySetting[]
            {
                new HotkeySetting { Action = HotkeyAction.IncreaseOpacity,   Key = Key.Up,    Modifiers = ModifierKeys.Control | ModifierKeys.Alt, Parameter = 10, Keys = "Ctrl+Alt+Up" },
                new HotkeySetting { Action = HotkeyAction.DecreaseOpacity,   Key = Key.Down,  Modifiers = ModifierKeys.Control | ModifierKeys.Alt, Parameter = 10, Keys = "Ctrl+Alt+Down" },
                new HotkeySetting { Action = HotkeyAction.ToggleTopMost,     Key = Key.T,     Modifiers = ModifierKeys.Control | ModifierKeys.Alt, Parameter = 0,  Keys = "Ctrl+Alt+T" },
                new HotkeySetting { Action = HotkeyAction.ToggleClickThrough,Key = Key.C,     Modifiers = ModifierKeys.Control | ModifierKeys.Alt, Parameter = 0,  Keys = "Ctrl+Alt+C" },
            };
        }
    }
} 