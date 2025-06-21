using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using WindowSlu.Models;

namespace WindowSlu.Services
{
    public class WindowService
    {
        public Dictionary<IntPtr, bool> WindowTopMostStates { get; set; } = new Dictionary<IntPtr, bool>();
        public Dictionary<IntPtr, int> WindowTransparencySettings { get; set; } = new Dictionary<IntPtr, int>();

        // P/Invoke declarations
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetLayeredWindowAttributes(IntPtr hwnd, out uint crKey, out byte bAlpha, out uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out bool pvAttribute, int cbAttribute);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int DWMWA_CLOAKED = 14;
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_EX_NOTOPMOST = 0x00000002;
        private const int SWP_NOMOVE = 0x0002;
        private const int SWP_NOSIZE = 0x0001;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const int WS_EX_LAYERED = 0x00080000;
        private const uint LWA_ALPHA = 0x00000002;

        private readonly HashSet<string> _processNameBlocklist = new HashSet<string>
        {
            "ApplicationFrameHost",
            "ShellExperienceHost",
            "StartMenuExperienceHost",
            "SearchUI",
            "SearchApp"
        };

        public ObservableCollection<WindowInfo> GetAllWindows()
        {
            var windows = new ObservableCollection<WindowInfo>();
            var selfProcessId = Process.GetCurrentProcess().Id;

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd) || GetWindowTextLength(hWnd) == 0)
                    return true;
                
                GetWindowThreadProcessId(hWnd, out uint processId);
                if (processId == selfProcessId)
                    return true;

                // Filter out tool windows
                int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
                if ((exStyle & WS_EX_TOOLWINDOW) == WS_EX_TOOLWINDOW)
                    return true;
                
                // Filter out cloaked UWP windows
                if (DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out bool isCloaked, Marshal.SizeOf<bool>()) == 0 && isCloaked)
                    return true;

                Process p = Process.GetProcessById((int)processId);
                
                if (_processNameBlocklist.Contains(p.ProcessName))
                    return true;

                var sb = new StringBuilder(256);
                GetWindowText(hWnd, sb, sb.Capacity);
                
                string fileDescription;
                try
                {
                    fileDescription = p.MainModule?.FileVersionInfo.FileDescription ?? p.ProcessName;
                }
                catch
                {
                    fileDescription = p.ProcessName;
                }

                windows.Add(new WindowInfo
                {
                    Handle = hWnd,
                    Title = sb.ToString(),
                    ProcessName = fileDescription,
                    ProcessId = (int)processId,
                    Opacity = 100, 
                    IsTopMost = (GetWindowLong(hWnd, -20) & 0x8) != 0, // GWL_EXSTYLE & WS_EX_TOPMOST
                    IsClickThrough = (GetWindowLong(hWnd, -20) & 0x20) != 0 // GWL_EXSTYLE & WS_EX_TRANSPARENT
                });
                return true;
            }, IntPtr.Zero);

            return windows;
        }
        
        public int GetTransparency(IntPtr hWnd)
        {
            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            if ((exStyle & WS_EX_LAYERED) == 0)
            {
                return 100;
            }

            if (!GetLayeredWindowAttributes(hWnd, out _, out byte alpha, out uint dwFlags) || (dwFlags & LWA_ALPHA) == 0)
            {
                // エラーが発生した場合、またはアルファが設定されていない場合は、100%を返す
                return 100;
            }
            
            return (int)Math.Round(alpha / 2.55);
        }

        public void SetTransparency(IntPtr hWnd, int percent)
        {
            if (hWnd == IntPtr.Zero) return;
            LoggingService.LogInfo($"Attempting to set transparency for window {hWnd} to {percent}%.");

            int currentStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            if ((currentStyle & WS_EX_LAYERED) == 0)
            {
                LoggingService.LogInfo($"Window {hWnd} is not layered. Applying WS_EX_LAYERED style.");
                SetWindowLong(hWnd, GWL_EXSTYLE, currentStyle | WS_EX_LAYERED);
            }

            byte alpha = (byte)(percent * 2.55);
            if (!SetLayeredWindowAttributes(hWnd, 0, alpha, LWA_ALPHA))
            {
                 LoggingService.LogError($"Failed to set layered window attributes for window {hWnd}. Win32Error: {Marshal.GetLastWin32Error()}");
            }
            else
            {
                LoggingService.LogInfo($"Successfully set transparency for window {hWnd} to {percent}%.");
            }
        }
        
        public void ToggleTopMost(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            LoggingService.LogInfo($"ToggleTopMost called for window handle: {hWnd}");

            var currentStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            bool isTopMost = (currentStyle & WS_EX_TOPMOST) != 0;

            IntPtr newHwnd = isTopMost ? HWND_NOTOPMOST : HWND_TOPMOST;
            string newStatus = isTopMost ? "NOT TOPMOST" : "TOPMOST";
            LoggingService.LogInfo($"Setting window {hWnd} to {newStatus}.");

            if (!SetWindowPos(hWnd, newHwnd, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE))
            {
                LoggingService.LogError($"SetWindowPos failed for window {hWnd}. Win32Error: {Marshal.GetLastWin32Error()}");
            }
        }

        public bool IsTopMost(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return false;
            return (GetWindowLong(hWnd, GWL_EXSTYLE) & WS_EX_TOPMOST) != 0;
        }

        public void ToggleClickThrough(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            bool isClickThrough = (exStyle & 0x20) != 0; // WS_EX_TRANSPARENT
            int newExStyle;

            LoggingService.LogInfo($"Toggling click-through for window {hWnd}. Current state: {isClickThrough}");

            if (isClickThrough)
            {
                newExStyle = exStyle & ~0x20;
                LoggingService.LogInfo("Disabling click-through.");
            }
            else
            {
                newExStyle = exStyle | 0x20;
                LoggingService.LogInfo("Enabling click-through.");
            }
            
            if (SetWindowLong(hWnd, GWL_EXSTYLE, newExStyle) == 0)
            {
                LoggingService.LogError($"SetWindowLong failed for window {hWnd} while toggling click-through. Win32Error: {Marshal.GetLastWin32Error()}");
            }
        }

        public bool IsClickThrough(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return false;
            return (GetWindowLong(hWnd, GWL_EXSTYLE) & 0x20) != 0; // WS_EX_TRANSPARENT
        }
    }
} 