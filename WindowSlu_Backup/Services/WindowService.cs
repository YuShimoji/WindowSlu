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
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder strText, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        public static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetLayeredWindowAttributes(IntPtr hwnd, out uint pcrKey, out byte pbAlpha, out uint pdwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        
        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out bool pvAttribute, int cbAttribute);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int DWMWA_CLOAKED = 14;

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

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd) || GetWindowTextLength(hWnd) == 0)
                    return true;
                
                // Filter out tool windows
                int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
                if ((exStyle & WS_EX_TOOLWINDOW) == WS_EX_TOOLWINDOW)
                    return true;
                
                // Filter out cloaked UWP windows
                if (DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out bool isCloaked, Marshal.SizeOf<bool>()) == 0 && isCloaked)
                    return true;

                GetWindowThreadProcessId(hWnd, out uint processId);
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
            if (hWnd == IntPtr.Zero) return 100;

            int exStyle = GetWindowLong(hWnd, -20);
            if ((exStyle & 0x80000) == 0x80000) // WS_EX_LAYERED
            {
                if (GetLayeredWindowAttributes(hWnd, out _, out byte alpha, out _))
                {
                    return (int)Math.Round(alpha / 2.55);
                }
            }
            return 100;
        }

        public void SetTransparency(IntPtr hWnd, int percent)
        {
            if (hWnd == IntPtr.Zero) return;
            byte alpha = (byte)Math.Round(percent * 2.55);
            int exStyle = GetWindowLong(hWnd, -20);
            SetWindowLong(hWnd, -20, exStyle | 0x80000); // GWL_EXSTYLE, WS_EX_LAYERED
            SetLayeredWindowAttributes(hWnd, 0, alpha, 0x2); // LWA_ALPHA
        }
        
        public void ToggleTopMost(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            var windowInfo = new WindowInfo { Handle = hWnd, IsTopMost = (GetWindowLong(hWnd, -20) & 0x8) != 0 };
            bool newTopMost = !windowInfo.IsTopMost;
            // HWND_TOPMOST = -1, HWND_NOTOPMOST = -2
            // SWP_NOMOVE = 0x0002, SWP_NOSIZE = 0x0001
            SetWindowPos(hWnd, newTopMost ? new IntPtr(-1) : new IntPtr(-2), 0, 0, 0, 0, 0x0001 | 0x0002);
        }

        public void ToggleClickThrough(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            int exStyle = GetWindowLong(hWnd, -20); // GWL_EXSTYLE
            bool isClickThrough = (exStyle & 0x20) != 0; // WS_EX_TRANSPARENT

            if (isClickThrough)
            {
                SetWindowLong(hWnd, -20, exStyle & ~0x20);
            }
            else
            {
                SetWindowLong(hWnd, -20, exStyle | 0x20);
            }
        }
    }
} 