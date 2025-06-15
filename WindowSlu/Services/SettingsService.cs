using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace WindowSlu.Services
{
    public class SettingsService
    {
        public int TransparencyStep { get; set; } = 5;
        public int DefaultTransparencyPercent { get; set; } = 70;
        public string LogFilePath { get; set; } = "application.log";
        public string LogLevel { get; set; } = "Info";

        private List<WindowSlu.HotkeySetting> _hotkeySettings;
        public Dictionary<IntPtr, bool> WindowTopMostStates { get; set; }
        public Dictionary<IntPtr, int> WindowTransparencySettings { get; set; }

        private readonly string _settingsFilePath;
        private const string DEFAULT_SETTINGS_FILE = "settings.json";

        public SettingsService()
        {
            _settingsFilePath = Path.Combine(AppContext.BaseDirectory, DEFAULT_SETTINGS_FILE);
            _hotkeySettings = new List<WindowSlu.HotkeySetting>();
            WindowTopMostStates = new Dictionary<IntPtr, bool>();
            WindowTransparencySettings = new Dictionary<IntPtr, int>();
            LoadSettings();
        }

        public void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string jsonContent = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<List<WindowSlu.HotkeySetting>>(jsonContent);
                    if (settings != null && settings.Any())
                    {
                        _hotkeySettings = settings;
                        LoggingService.LogInfo($"Loaded {settings.Count} settings from file.");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error loading settings: {ex.Message}", ex);
            }

            LoadDefaultHotkeySettings();
        }

        public void SaveSettings()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonContent = JsonSerializer.Serialize(_hotkeySettings, options);
                File.WriteAllText(_settingsFilePath, jsonContent);
                LoggingService.LogInfo($"Saved {_hotkeySettings.Count} settings to file.");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error saving settings: {ex.Message}", ex);
            }
        }

        private void LoadDefaultHotkeySettings()
        {
            _hotkeySettings.Clear();
            _hotkeySettings.Add(new WindowSlu.HotkeySetting { Keys = "Alt+Up", Action = WindowSlu.HotkeyAction.DecreaseTransparency, Parameter = 10 });
            _hotkeySettings.Add(new WindowSlu.HotkeySetting { Keys = "Alt+Down", Action = WindowSlu.HotkeyAction.IncreaseTransparency, Parameter = 10 });
            _hotkeySettings.Add(new WindowSlu.HotkeySetting { Keys = "Ctrl+Shift+Space", Action = WindowSlu.HotkeyAction.ToggleTransparency, Parameter = 0 });
            _hotkeySettings.Add(new WindowSlu.HotkeySetting { Keys = "Ctrl+Alt+P", Action = WindowSlu.HotkeyAction.ToggleTopMost, Parameter = 0 });
            _hotkeySettings.Add(new WindowSlu.HotkeySetting { Keys = "Ctrl+Alt+C", Action = WindowSlu.HotkeyAction.ToggleClickThrough, Parameter = 0 });

            SaveSettings();
            LoggingService.LogInfo($"Loaded default hotkey settings. Count: {_hotkeySettings.Count}");
            }

        public void ResetToDefaultSettings()
        {
            LoadDefaultHotkeySettings();
        }

        public List<WindowSlu.HotkeySetting> GetHotkeySettings()
        {
            if (_hotkeySettings == null || !_hotkeySettings.Any())
            {
                LoadDefaultHotkeySettings();
            }
            return _hotkeySettings;
        }

        public void UpdateHotkeySetting(string keys, WindowSlu.HotkeyAction action, int parameter)
        {
            var existing = _hotkeySettings.FirstOrDefault(h => h.Action == action);
            if (existing != null)
            {
                existing.Keys = keys;
                existing.Parameter = parameter;
            }
            else
            {
                _hotkeySettings.Add(new WindowSlu.HotkeySetting { Keys = keys, Action = action, Parameter = parameter });
            }
            SaveSettings();
        }
    }
} 