using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Input;
using WindowSlu.Models;

namespace WindowSlu.Services
{
    public class HotkeyService : IDisposable
    {
        private readonly SettingsService _settingsService;
        private readonly Action<HotkeySetting>? _hotkeyCallback;
        private readonly IntPtr _windowHandle;
        private readonly IHotkeyPlatform _platform;
        private readonly Dictionary<int, HotkeySetting> _registeredHotkeys = new();
        private readonly Dictionary<string, HotkeySetting> _presetHotkeys = new(StringComparer.Ordinal);
        private readonly List<string> _lastRegistrationErrors = new();
        private int _currentHotkeyId = 1;
        private bool _disposed;

        public IReadOnlyList<string> LastRegistrationErrors => _lastRegistrationErrors.AsReadOnly();

        public HotkeyService(IntPtr windowHandle, SettingsService settingsService, Action<HotkeySetting>? hotkeyCallback)
            : this(windowHandle, settingsService, hotkeyCallback, new WindowsHotkeyPlatform()) { }

        public HotkeyService(IntPtr windowHandle, SettingsService settingsService, Action<HotkeySetting>? hotkeyCallback, IHotkeyPlatform platform)
        {
            _windowHandle = windowHandle;
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _hotkeyCallback = hotkeyCallback;
            _platform = platform ?? throw new ArgumentNullException(nameof(platform));
            ReloadSettings();
        }

        public void HandleHotkeyMessage(int hotkeyId)
        {
            if (!_disposed && _registeredHotkeys.TryGetValue(hotkeyId, out var setting))
            {
                LoggingService.LogInfo($"Hotkey ID {hotkeyId} detected for action {setting.Action}");
                _hotkeyCallback?.Invoke(setting);
            }
        }

        public bool RegisterPresetHotkey(string presetId, string hotKeyString)
        {
            EnsureNotDisposed();
            _lastRegistrationErrors.Clear();
            if (string.IsNullOrWhiteSpace(presetId))
                return RecordFailure("Preset hotkey registration failed: preset ID is empty.");
            if (string.IsNullOrWhiteSpace(hotKeyString))
            {
                UnregisterPresetHotkeyCore(presetId);
                return true;
            }

            var setting = ParseHotKeyString(hotKeyString, presetId);
            if (setting == null || setting.Key == Key.None)
                return RecordFailure($"Preset '{presetId}' hotkey '{hotKeyString}' is invalid or has no key.");
            setting.IsEnabled = true;

            if (_presetHotkeys.TryGetValue(presetId, out var current) &&
                current.Key == setting.Key && current.Modifiers == setting.Modifiers &&
                _registeredHotkeys.Values.Any(registered => ReferenceEquals(registered, current)))
                return true;

            // Keep the current assignment registered until its replacement succeeds.
            if (!TryRegister(setting, out var newId)) return false;
            UnregisterPresetHotkeyCore(presetId);
            _presetHotkeys[presetId] = setting;
            _registeredHotkeys.Add(newId, setting);
            LogRegistered(setting, newId);
            return true;
        }

        public void UnregisterPresetHotkey(string presetId)
        {
            if (_disposed) return;
            _lastRegistrationErrors.Clear();
            UnregisterPresetHotkeyCore(presetId);
        }

        private void UnregisterPresetHotkeyCore(string presetId)
        {
            if (!_presetHotkeys.Remove(presetId, out var existing)) return;
            var id = _registeredHotkeys.FirstOrDefault(pair => ReferenceEquals(pair.Value, existing)).Key;
            if (id != 0) UnregisterRegistered(id, existing);
        }

        private static HotkeySetting? ParseHotKeyString(string hotKeyString, string presetId)
        {
            var setting = new HotkeySetting { Action = HotkeyAction.ApplyPreset, PresetId = presetId, Modifiers = ModifierKeys.None, Key = Key.None };
            foreach (var part in hotKeyString.Split('+').Select(part => part.Trim()))
            {
                var upper = part.ToUpperInvariant();
                if (upper is "CTRL" or "CONTROL") setting.Modifiers |= ModifierKeys.Control;
                else if (upper == "ALT") setting.Modifiers |= ModifierKeys.Alt;
                else if (upper == "SHIFT") setting.Modifiers |= ModifierKeys.Shift;
                else if (upper is "WIN" or "WINDOWS") setting.Modifiers |= ModifierKeys.Windows;
                else if (Enum.TryParse(part, true, out Key key)) setting.Key = key;
            }
            return setting;
        }

        public void ReloadSettings()
        {
            EnsureNotDisposed();
            _lastRegistrationErrors.Clear();
            UnregisterAllHotkeys();
            foreach (var setting in _settingsService.Settings.HotkeySettings ?? new List<HotkeySetting>())
            {
                if (!setting.IsEnabled) continue;
                if (setting.Key == Key.None || setting.Modifiers == ModifierKeys.None)
                {
                    if (setting.Modifiers == ModifierKeys.None)
                        LoggingService.LogInfo($"Skipping hotkey without modifiers: {setting.Key}");
                    continue;
                }
                RegisterHotkey(setting);
            }

            // Re-register all persisted in-memory preset assignments after ordinary keys.
            foreach (var pair in _presetHotkeys.ToArray())
            {
                var setting = pair.Value;
                if (!TryRegister(setting, out var id)) continue;
                _registeredHotkeys.Add(id, setting);
                LogRegistered(setting, id);
            }
        }

        private bool RegisterHotkey(HotkeySetting setting)
        {
            if (!TryRegister(setting, out var id)) return false;
            _registeredHotkeys.Add(id, setting);
            LogRegistered(setting, id);
            return true;
        }

        private bool TryRegister(HotkeySetting setting, out int id)
        {
            id = _currentHotkeyId++;
            if (id <= 0) { _currentHotkeyId = 2; id = 1; }
            var modifiers = (uint)setting.Modifiers;
            var vk = (uint)KeyInterop.VirtualKeyFromKey(setting.Key);
            if (_platform.RegisterHotKey(_windowHandle, id, modifiers, vk)) return true;
            var error = _platform.GetLastError();
            var message = $"Failed to register hotkey {setting.Modifiers}+{setting.Key}" +
                (setting.PresetId == null ? string.Empty : $" for preset '{setting.PresetId}'") + $". Win32Error: {error}.";
            _lastRegistrationErrors.Add(message);
            LoggingService.LogError(message);
            id = 0;
            return false;
        }

        private void UnregisterRegistered(int id, HotkeySetting setting)
        {
            if (_registeredHotkeys.Remove(id) && !_platform.UnregisterHotKey(_windowHandle, id))
            {
                var message = $"Failed to unregister hotkey {setting.Modifiers}+{setting.Key}. Win32Error: {_platform.GetLastError()}.";
                _lastRegistrationErrors.Add(message);
                LoggingService.LogError(message);
            }
        }

        private void UnregisterAllHotkeys()
        {
            foreach (var pair in _registeredHotkeys.ToArray()) UnregisterRegistered(pair.Key, pair.Value);
            LoggingService.LogInfo("All hotkeys have been unregistered.");
        }

        private void LogRegistered(HotkeySetting setting, int id) =>
            LoggingService.LogInfo($"Successfully registered hotkey: {setting.Modifiers}+{setting.Key} with ID {id}");

        private bool RecordFailure(string message)
        {
            _lastRegistrationErrors.Add(message);
            LoggingService.LogError(message);
            return false;
        }

        private void EnsureNotDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(HotkeyService));
        }

        public void Dispose()
        {
            if (_disposed) return;
            UnregisterAllHotkeys();
            _presetHotkeys.Clear();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        private sealed class WindowsHotkeyPlatform : IHotkeyPlatform
        {
            [DllImport("user32.dll", EntryPoint = "RegisterHotKey", SetLastError = true)] private static extern bool RegisterHotKeyNative(IntPtr hWnd, int id, uint modifiers, uint vk);
            [DllImport("user32.dll", EntryPoint = "UnregisterHotKey", SetLastError = true)] private static extern bool UnregisterHotKeyNative(IntPtr hWnd, int id);
            public bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey) => RegisterHotKeyNative(windowHandle, id, modifiers, virtualKey);
            public bool UnregisterHotKey(IntPtr windowHandle, int id) => UnregisterHotKeyNative(windowHandle, id);
            public int GetLastError() => Marshal.GetLastWin32Error();
        }
    }
}
