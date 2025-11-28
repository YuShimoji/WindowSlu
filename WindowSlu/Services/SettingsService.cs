using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Input;
using WindowSlu.Models;
using System.Linq;

namespace WindowSlu.Services
{
    public class SettingsService
    {
        private readonly string _settingsFilePath;
        public ApplicationSettings Settings { get; private set; } = null!;

        public SettingsService()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolderPath = Path.Combine(appDataPath, "WindowSlu");
            Directory.CreateDirectory(appFolderPath);
            _settingsFilePath = Path.Combine(appFolderPath, "settings.json");

            LoadSettings();
        }

        private void LoadSettings()
        {
            if (File.Exists(_settingsFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<ApplicationSettings>(json);
                    Settings = settings ?? CreateAndSaveDefaultSettings();

                    // Merge hotkeys after loading
                    if (MergeMissingHotkeys(Settings))
                    {
                        SaveSettings(Settings);
                    }
                }
                catch (Exception) // Catch broader exceptions for file corruption or access issues
                {
                    Settings = CreateAndSaveDefaultSettings();
                }
            }
            else
            {
                Settings = CreateAndSaveDefaultSettings();
            }
        }
        
        private ApplicationSettings CreateAndSaveDefaultSettings()
        {
            var defaultSettings = new ApplicationSettings();
            SaveSettings(defaultSettings);
            return defaultSettings;
        }

        public void SaveSettings()
        {
            SaveSettings(Settings);
        }

        private void SaveSettings(ApplicationSettings settings)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (IOException)
            {
                // Handle exceptions during file save (e.g., access denied)
            }
        }

        private bool MergeMissingHotkeys(ApplicationSettings loadedSettings)
        {
            bool settingsUpdated = false;
            var defaultSettings = new ApplicationSettings();

            foreach (var defaultHotkey in defaultSettings.HotkeySettings)
            {
                if (!loadedSettings.HotkeySettings.Any(h => h.Action == defaultHotkey.Action))
                {
                    loadedSettings.HotkeySettings.Add(defaultHotkey);
                    settingsUpdated = true;
                }
            }

            return settingsUpdated;
        }

        // This method is redundant as default settings are now defined in ApplicationSettings.cs
        /*
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
        */
    }
} 