using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Input;
using WindowSlu.Models;

namespace WindowSlu.Services
{
    public class HotkeyService : IDisposable
    {
        private SettingsService _settingsService;
        private WindowService _windowService;
        private Action<HotkeyAction, int> _hotkeyCallback;
        private List<RegisteredHotkey> _registeredHotkeys = new List<RegisteredHotkey>();
        private static int _currentHotkeyId = 1;

        // Windows Hooking related fields
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private static IntPtr _hookID = IntPtr.Zero;
        private LowLevelKeyboardProc _proc;

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12; // Alt key
        private const uint LLKHF_ALTDOWN = 0x20;

        // Old RegisterHotKey fields - will be removed or refactored
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const uint MOD_NONE = 0x0000;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;

        public HotkeyService(SettingsService settingsService, WindowService windowService, Action<HotkeyAction, int> hotkeyCallback)
        {
            _proc = HookCallback;
            _hookID = SetHook(_proc);
            _settingsService = settingsService;
            _windowService = windowService;
            _hotkeyCallback = hotkeyCallback;
            ReloadSettings(); // Loads the hotkey configurations to be checked against in the hook
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                KBDLLHOOKSTRUCT kbdStruct = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                Key key = KeyInterop.KeyFromVirtualKey((int)kbdStruct.vkCode);

                ModifierKeys modifiers = ModifierKeys.None;
                if ((GetKeyState(VK_CONTROL) & 0x8000) != 0) modifiers |= ModifierKeys.Control;
                if ((GetKeyState(VK_SHIFT) & 0x8000) != 0) modifiers |= ModifierKeys.Shift;
                if ((kbdStruct.flags & LLKHF_ALTDOWN) != 0) modifiers |= ModifierKeys.Alt;

                // Check against registered hotkeys
                foreach (var hotkey in _registeredHotkeys)
                {
                    if (hotkey.IsEnabled && key == hotkey.Key && modifiers == hotkey.Modifiers)
                    {
                        LoggingService.LogInfo($"Hotkey combination {hotkey.Modifiers}+{hotkey.Key} detected for action {hotkey.Action}");
                        _hotkeyCallback?.Invoke(hotkey.Action, hotkey.Parameter);
                    }
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        public void ReloadSettings()
        {
            // This method no longer registers OS-level hotkeys, but just reloads the settings.
            LoggingService.LogInfo("Reloading hotkey settings into service.");
            var settings = _settingsService.Settings.HotkeySettings;
            _registeredHotkeys.Clear();
            if (settings != null)
            {
                foreach (var setting in settings)
                {
                    // For backward compatibility
                    if (setting.Key == Key.None && !string.IsNullOrEmpty(setting.Keys))
                    {
                        try
                        {
                            var gesture = (KeyGesture)new KeyGestureConverter().ConvertFromString(setting.Keys);
                            if(gesture != null) {
                                setting.Key = gesture.Key;
                                setting.Modifiers = gesture.Modifiers;
                            }
                        }
                        catch(Exception ex) {
                            LoggingService.LogError($"Could not parse legacy key gesture: {ex.Message}");
                            continue;
                        }
                    }
                    if(setting.IsEnabled)
                    {
                        _registeredHotkeys.Add(new RegisteredHotkey {
                            Id = _currentHotkeyId++, // Keep for internal reference if needed
                            Action = setting.Action,
                            Key = setting.Key,
                            Modifiers = setting.Modifiers,
                            Parameter = setting.Parameter,
                            IsEnabled = setting.IsEnabled
                        });
                        LoggingService.LogInfo($"Loaded hotkey setting: {setting.Modifiers}+{setting.Key} for action {setting.Action}");
                    }
                }
            }
        }

        public void Dispose()
        {
            UnhookWindowsHookEx(_hookID);
            LoggingService.LogInfo("Keyboard hook released.");
            GC.SuppressFinalize(this);
        }

        private class RegisteredHotkey
        {
            public int Id { get; set; }
            public HotkeyAction Action { get; set; }
            public Key Key { get; set; }
            public ModifierKeys Modifiers { get; set; }
            public int Parameter { get; set; }
            public bool IsEnabled { get; set; }
        }
    }
} 