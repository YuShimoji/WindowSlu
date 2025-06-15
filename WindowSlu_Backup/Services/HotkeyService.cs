using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace WindowSlu.Services
{
    public class HotkeyService : IDisposable
    {
        private IntPtr _windowHandle;
        private SettingsService _settingsService;
        private WindowService _windowService;
        private Action<HotkeyAction, int> _hotkeyCallback;
        private List<RegisteredHotkey> _registeredHotkeys = new List<RegisteredHotkey>();
        private static int _currentHotkeyId = 1;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const uint MOD_NONE = 0x0000;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;

        public HotkeyService(IntPtr windowHandle, SettingsService settingsService, WindowService windowService, Action<HotkeyAction, int> hotkeyCallback)
        {
            _windowHandle = windowHandle;
            _settingsService = settingsService;
            _windowService = windowService;
            _hotkeyCallback = hotkeyCallback;
        }

        public bool RegisterHotkey(string keys, HotkeyAction action, int parameter)
        {
            if (_windowHandle == IntPtr.Zero) return false;

            try
            {
                KeyGestureConverter gestureConverter = new KeyGestureConverter();
                KeyGesture? gesture = gestureConverter.ConvertFromString(keys) as KeyGesture;

                if (gesture != null)
                {
                    uint modifiers = MOD_NONE;
                    if ((gesture.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) modifiers |= MOD_CONTROL;
                    if ((gesture.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) modifiers |= MOD_ALT;
                    if ((gesture.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) modifiers |= MOD_SHIFT;
                    if ((gesture.Modifiers & ModifierKeys.Windows) == ModifierKeys.Windows) modifiers |= MOD_WIN;

                    int virtualKey = KeyInterop.VirtualKeyFromKey(gesture.Key);
                    if (virtualKey == 0) 
                    {
                        LoggingService.LogError($"Could not convert key {gesture.Key} to virtual key for hotkey '{keys}'.");
                        return false;
                    }

                    int hotkeyId = _currentHotkeyId++;
                    LoggingService.LogInfo($"Attempting to register OS hotkey ID {hotkeyId} for '{keys}' (Mods: {modifiers}, VK: {virtualKey}) on HWND {_windowHandle}.");
                    if (RegisterHotKey(_windowHandle, hotkeyId, modifiers, (uint)virtualKey))
                    {
                        _registeredHotkeys.Add(new RegisteredHotkey { Id = hotkeyId, Keys = keys, Action = action, Parameter = parameter, Gesture = gesture });
                        LoggingService.LogInfo($"SUCCESSFULLY registered OS hotkey ID {hotkeyId}: '{keys}' -> {action} (Param: {parameter}).");
                        return true;
                    }
                    else
                    {
                        int errorCode = Marshal.GetLastWin32Error();
                        LoggingService.LogError($"FAILED to register OS hotkey ID {hotkeyId} for '{keys}'. Win32Error: {errorCode}.");
                        _currentHotkeyId--;
                        return false;
                    }
                }
                else
                {
                    LoggingService.LogError($"Could not parse key gesture string: '{keys}'.");
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Exception registering hotkey '{keys}': {ex.Message}");
            }
            return false;
        }

        public void UnregisterAllHotkeys()
        {
            if (_windowHandle == IntPtr.Zero) return;

            foreach (var hotkey in _registeredHotkeys)
            {
                UnregisterHotKey(_windowHandle, hotkey.Id);
                LoggingService.LogInfo($"Unregistered hotkey ID {hotkey.Id}: '{hotkey.Keys}'.");
            }
            _registeredHotkeys.Clear();
            LoggingService.LogInfo("All hotkeys unregistered.");
        }

        public void ReloadSettings()
        {
            UnregisterAllHotkeys();
            var hotkeySettings = _settingsService.GetHotkeySettings();
            if (hotkeySettings != null)
            {
                foreach (var setting in hotkeySettings)
                {
                    if (!string.IsNullOrEmpty(setting.Keys))
                    {
                        RegisterHotkey(setting.Keys, setting.Action, setting.Parameter);
                    }
                }
            }
        }

        public bool AreHotkeysRegistered()
        {
            return _registeredHotkeys.Any();
        }

        public void HandleHotkeyMessage(int msg, IntPtr wParam, IntPtr lParam)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY)
            {
                int hotkeyId = wParam.ToInt32();
                ProcessHotkey(hotkeyId);
            }
        }

        private void ProcessHotkey(int hotkeyIdFromWndProc)
        {
            var hotkey = _registeredHotkeys.FirstOrDefault(rh => rh.Id == hotkeyIdFromWndProc);
            if (hotkey != null)
            {
                LoggingService.LogInfo($"Matched WM_HOTKEY ID {hotkeyIdFromWndProc} to action {hotkey.Action}. Invoking callback.");
                _hotkeyCallback?.Invoke(hotkey.Action, hotkey.Parameter);
            }
            else
            {
                LoggingService.LogInfo($"Received WM_HOTKEY with unknown ID {hotkeyIdFromWndProc}.");
            }
        }

        public void Dispose()
        {
            UnregisterAllHotkeys();
            GC.SuppressFinalize(this);
        }

        private class RegisteredHotkey
        {
            public int Id { get; set; }
            public string? Keys { get; set; }
            public HotkeyAction Action { get; set; }
            public int Parameter { get; set; }
            public KeyGesture? Gesture { get; set; }
        }
    }
} 