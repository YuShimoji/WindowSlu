using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using WindowSlu.Models;
using System.Linq;

namespace WindowSlu.Services
{
    public class HotkeyService : IDisposable
    {
        private readonly SettingsService _settingsService;
        private readonly Action<HotkeySetting> _hotkeyCallback;
        private readonly IntPtr _windowHandle;
        private readonly Dictionary<int, HotkeySetting> _registeredHotkeys = new Dictionary<int, HotkeySetting>();
        private int _currentHotkeyId = 1;
        private readonly List<HotkeySetting> _presetHotkeys = new List<HotkeySetting>();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public HotkeyService(IntPtr windowHandle, SettingsService settingsService, Action<HotkeySetting> hotkeyCallback)
        {
            _windowHandle = windowHandle;
            _settingsService = settingsService;
            _hotkeyCallback = hotkeyCallback;

            ReloadSettings();
        }

        public void HandleHotkeyMessage(int hotkeyId)
        {
            if (_registeredHotkeys.TryGetValue(hotkeyId, out var hotkeySetting))
            {
                LoggingService.LogInfo($"Hotkey ID {hotkeyId} detected for action {hotkeySetting.Action}");
                _hotkeyCallback?.Invoke(hotkeySetting);
            }
        }

        /// <summary>
        /// プリセット用のホットキーを登録
        /// </summary>
        public void RegisterPresetHotkey(string presetId, string hotKeyString)
        {
            if (string.IsNullOrWhiteSpace(hotKeyString)) return;

            // 既存のプリセットホットキーを解除
            UnregisterPresetHotkey(presetId);

            var setting = ParseHotKeyString(hotKeyString, presetId);
            if (setting != null && setting.Key != Key.None)
            {
                setting.IsEnabled = true;
                _presetHotkeys.Add(setting);
                RegisterHotkey(setting);
            }
        }

        /// <summary>
        /// プリセット用のホットキーを解除
        /// </summary>
        public void UnregisterPresetHotkey(string presetId)
        {
            var existing = _presetHotkeys.FirstOrDefault(h => h.PresetId == presetId);
            if (existing != null)
            {
                var registeredId = _registeredHotkeys.FirstOrDefault(x => x.Value == existing).Key;
                if (registeredId != 0)
                {
                    UnregisterHotKey(_windowHandle, registeredId);
                    _registeredHotkeys.Remove(registeredId);
                }
                _presetHotkeys.Remove(existing);
            }
        }

        /// <summary>
        /// ホットキー文字列をパースしてHotkeySettingを生成
        /// </summary>
        private HotkeySetting? ParseHotKeyString(string hotKeyString, string presetId)
        {
            var setting = new HotkeySetting
            {
                Action = HotkeyAction.ApplyPreset,
                PresetId = presetId,
                Modifiers = ModifierKeys.None,
                Key = Key.None
            };

            var parts = hotKeyString.Split('+').Select(p => p.Trim()).ToList();
            foreach (var part in parts)
            {
                var upper = part.ToUpperInvariant();
                if (upper == "CTRL" || upper == "CONTROL")
                    setting.Modifiers |= ModifierKeys.Control;
                else if (upper == "ALT")
                    setting.Modifiers |= ModifierKeys.Alt;
                else if (upper == "SHIFT")
                    setting.Modifiers |= ModifierKeys.Shift;
                else if (upper == "WIN" || upper == "WINDOWS")
                    setting.Modifiers |= ModifierKeys.Windows;
                else if (Enum.TryParse<Key>(part, true, out var key))
                    setting.Key = key;
            }

            return setting;
        }

        public void ReloadSettings()
        {
            UnregisterAllHotkeys();
            _registeredHotkeys.Clear();

            var settings = _settingsService.Settings.HotkeySettings;
            if (settings == null) return;

            foreach (var setting in settings)
            {
                if (setting.IsEnabled && setting.Key != Key.None && setting.Modifiers != ModifierKeys.None)
                {
                    RegisterHotkey(setting);
                }
                else if (setting.IsEnabled && setting.Modifiers == ModifierKeys.None)
                {
                    LoggingService.LogInfo($"Skipping hotkey without modifiers: {setting.Key}");
                }
            }
        }

        private void RegisterHotkey(HotkeySetting setting)
        {
            var modifiers = (uint)setting.Modifiers;
            var vk = (uint)KeyInterop.VirtualKeyFromKey(setting.Key);
            int hotkeyId = _currentHotkeyId++;

            if (RegisterHotKey(_windowHandle, hotkeyId, modifiers, vk))
            {
                _registeredHotkeys.Add(hotkeyId, setting);
                LoggingService.LogInfo($"Successfully registered hotkey: {setting.Modifiers}+{setting.Key} with ID {hotkeyId}");
            }
            else
            {
                var errorCode = Marshal.GetLastWin32Error();
                LoggingService.LogError($"Failed to register hotkey: {setting.Modifiers}+{setting.Key}. Win32Error: {errorCode}");
            }
        }
        
        private void UnregisterAllHotkeys()
        {
            foreach (var id in _registeredHotkeys.Keys)
            {
                UnregisterHotKey(_windowHandle, id);
            }
            LoggingService.LogInfo("All hotkeys have been unregistered.");
        }

        public void Dispose()
        {
            UnregisterAllHotkeys();
            GC.SuppressFinalize(this);
        }
    }
} 