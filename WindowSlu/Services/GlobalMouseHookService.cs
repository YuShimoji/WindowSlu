using System;
using System.ComponentModel; // For Win32Exception
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input; // For ModifierKeys and KeyEventArgs potentially (though not directly used for wheel)
// For LoggingService and WindowService (or its POINT struct)
// Assuming WindowService is in WindowSlu.Services, otherwise adjust namespace
// No, LoggingService is likely also in WindowSlu.Services if used directly like this.

namespace WindowSlu.Services
{
    public class GlobalMouseHookService : IDisposable
    {
        private const int WH_MOUSE_LL = 14; // Low-level mouse hook
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int VK_CONTROL = 0x11;  // Ctrl key

        private IntPtr _hookID = IntPtr.Zero;
        private readonly LowLevelMouseProc _proc; // Keep a reference to prevent GC
        private DateTime _lastWheelEventTime = DateTime.MinValue;
        private const int MIN_WHEEL_EVENT_INTERVAL_MS = 50; // 最小イベント間隔（ミリ秒）

        public event EventHandler<GlobalMouseWheelEventArgs>? GlobalMouseWheelEvent;

        // WindowService is not directly used here anymore for GetWindowFromCursorPos
        // as WindowFromPoint and POINT struct are defined locally or via P/Invoke.
        // If LoggingService is a static class, it can be called directly.
        // If it's an instance, it would need to be passed in or accessed differently.
        // For simplicity, assuming LoggingService has static methods.

        public GlobalMouseHookService()
        {
            _proc = HookCallback; 
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule? curModule = curProcess.MainModule)
            {
                if (curModule != null)
                {
                    _hookID = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
                    if (_hookID == IntPtr.Zero)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                }
            }
        }

        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule? curModule = curProcess.MainModule)
            {
                if (curModule == null)
                {
                    LoggingService.LogError("Failed to get current process MainModule for setting hook.");
                    return IntPtr.Zero;
                }
                return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_MOUSEWHEEL)
            {
                bool ctrlPressed = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;

                // Ctrlキーが押されている場合のみ処理
                if (ctrlPressed)
                {
                    var now = DateTime.Now;
                    if ((now - _lastWheelEventTime).TotalMilliseconds >= MIN_WHEEL_EVENT_INTERVAL_MS)
                    {
                        _lastWheelEventTime = now;
                        
                    MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT))!;
                    int wheelDelta = (short)((hookStruct.mouseData >> 16) & 0xFFFF);
                    IntPtr windowHandle = WindowFromPoint(hookStruct.pt);
                    
                        var args = new GlobalMouseWheelEventArgs(windowHandle, wheelDelta);
                        GlobalMouseWheelEvent?.Invoke(this, args);
                        
                        if (args.Handled)
                        {
                            // To prevent the message from being passed to other applications (e.g., zooming),
                            // we return a non-zero value.
                            return (IntPtr)1;
                        }
                    }
                    else
                    {
                        // イベントの間隔が短すぎる場合は処理をスキップ
                    return (IntPtr)1; 
                    }
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }
        
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        static extern IntPtr WindowFromPoint(POINT Point);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT 
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)] // Corrected: LayoutKind
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData; 
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        public void Dispose()
        {
            if (_hookID != IntPtr.Zero)
            {
                if (UnhookWindowsHookEx(_hookID))
                {
                    LoggingService.LogInfo("Global mouse hook successfully unhooked.");
                }
                else
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    LoggingService.LogError($"Failed to unhook global mouse hook. Win32Error: {errorCode}");
                }
                _hookID = IntPtr.Zero;
            }
            GC.SuppressFinalize(this);
        }

        ~GlobalMouseHookService()
        {
            Dispose();
        }
    }

    public class GlobalMouseWheelEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; }
        public int Delta { get; }
        public bool Handled { get; set; }

        public GlobalMouseWheelEventArgs(IntPtr windowHandle, int delta)
        {
            WindowHandle = windowHandle;
            Delta = delta;
            Handled = false;
        }
    }
} 