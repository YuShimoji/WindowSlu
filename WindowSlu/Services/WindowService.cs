using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using WindowSlu.Models;
using System.Windows.Media;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

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
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, EnumMonitorsDelegate lpfnEnum, IntPtr dwData);

        private delegate bool EnumMonitorsDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetLayeredWindowAttributes(IntPtr hwnd, out uint crKey, out byte bAlpha, out uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out bool pvAttribute, int cbAttribute);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int DWMWA_CLOAKED = 14;
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_EX_NOTOPMOST = 0x00000002;
        private const int SWP_NOMOVE = 0x0002;
        private const int SWP_NOSIZE = 0x0001;
        private const int SWP_NOACTIVATE = 0x0010;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const int WS_EX_LAYERED = 0x00080000;
        private const uint LWA_ALPHA = 0x00000002;
        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        private const uint SHGFI_SMALLICON = 0x000000001;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

        private readonly HashSet<string> _processNameBlocklist = new HashSet<string>
        {
            "ApplicationFrameHost",
            "ShellExperienceHost",
            "StartMenuExperienceHost",
            "SearchUI",
            "SearchApp"
        };

        public virtual ObservableCollection<WindowInfo> GetAllWindows()
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
                ImageSource? icon = null;
                try
                {
                    string processPath = p.MainModule?.FileName ?? "";
                    fileDescription = p.MainModule?.FileVersionInfo.FileDescription ?? p.ProcessName;
                    if (!string.IsNullOrEmpty(processPath))
                    {
                        icon = GetIconForFile(processPath);
                    }
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
                    Icon = icon,
                    Opacity = 100,
                    IsTopMost = (GetWindowLong(hWnd, -20) & 0x8) != 0, // GWL_EXSTYLE & WS_EX_TOPMOST
                    IsClickThrough = (GetWindowLong(hWnd, -20) & 0x20) != 0 // GWL_EXSTYLE & WS_EX_TRANSPARENT
                });
                return true;
            }, IntPtr.Zero);

            return windows;
        }

        public virtual int GetTransparency(IntPtr hWnd)
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

        public virtual bool SetTransparency(IntPtr hWnd, int percent)
        {
            if (hWnd == IntPtr.Zero || percent < 0 || percent > 100) return false;
            int style = GetWindowLong(hWnd, GWL_EXSTYLE);
            if ((style & WS_EX_LAYERED) == 0)
            {
                SetWindowLong(hWnd, GWL_EXSTYLE, style | WS_EX_LAYERED);
                if ((GetWindowLong(hWnd, GWL_EXSTYLE) & WS_EX_LAYERED) == 0) return false;
            }
            bool success = SetLayeredWindowAttributes(hWnd, 0, (byte)Math.Round(percent * 255.0 / 100), LWA_ALPHA);
            if (!success) LoggingService.LogError($"Transparency failed: {Marshal.GetLastWin32Error()}");
            return success;
        }

        public virtual bool SetTopMost(IntPtr hWnd, bool enabled)
        {
            if (hWnd == IntPtr.Zero) return false;
            bool success = SetWindowPos(hWnd, enabled ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            bool applied = success && IsTopMost(hWnd) == enabled;
            if (!applied) LoggingService.LogError($"TopMost was not applied: {Marshal.GetLastWin32Error()}");
            return applied;
        }

        public bool ToggleTopMost(IntPtr hWnd) => SetTopMost(hWnd, !IsTopMost(hWnd));

        public virtual bool IsTopMost(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return false;
            return (GetWindowLong(hWnd, GWL_EXSTYLE) & WS_EX_TOPMOST) != 0;
        }

        public void ToggleClickThrough(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);

            // Click-through requires the window to be layered.
            if ((exStyle & WS_EX_LAYERED) == 0)
            {
                // If not layered, apply the style first.
                SetWindowLong(hWnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED);
            }

            // Re-fetch the style to ensure we have the latest state.
            exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);

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

        public virtual bool IsClickThrough(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return false;
            return (GetWindowLong(hWnd, GWL_EXSTYLE) & 0x20) != 0; // WS_EX_TRANSPARENT
        }

        private ImageSource? GetIconForFile(string filePath)
        {
            SHFILEINFO shinfo = new SHFILEINFO();
            IntPtr hImgSmall = SHGetFileInfo(filePath, FILE_ATTRIBUTE_NORMAL, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES);

            if (shinfo.hIcon == IntPtr.Zero) return null;

            try
            {
                var icon = (ImageSource)Imaging.CreateBitmapSourceFromHIcon(
                    shinfo.hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                icon.Freeze(); // Important for use in a different thread
                return icon;
            }
            finally
            {
                DestroyIcon(shinfo.hIcon);
            }
        }

        /// <summary>
        /// ウィンドウの位置とサイズを取得
        /// </summary>
        /// <param name="hWnd">ウィンドウハンドル</param>
        /// <returns>位置とサイズを含むRECT構造体</returns>
        public virtual RECT GetWindowPosition(IntPtr hWnd)
        {
            RECT rect = new RECT();
            if (hWnd != IntPtr.Zero)
            {
                GetWindowRect(hWnd, out rect);
            }
            return rect;
        }

        /// <summary>
        /// ウィンドウの位置とサイズを設定
        /// </summary>
        /// <param name="hWnd">ウィンドウハンドル</param>
        /// <param name="x">X座標</param>
        /// <param name="y">Y座標</param>
        /// <param name="width">幅</param>
        /// <param name="height">高さ</param>
        public virtual bool SetWindowPosition(IntPtr hWnd, int x, int y, int width, int height)
        {
            if (hWnd == IntPtr.Zero || width <= 0 || height <= 0) return false;
            bool success = MoveWindow(hWnd, x, y, width, height, true);
            if (!success) LoggingService.LogError($"MoveWindow failed for {hWnd}: {Marshal.GetLastWin32Error()}");
            return success;
        }

        /// <summary>Restores a valid minimized or maximized window before arranging it.</summary>
        public virtual bool PrepareForArrangement(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd)) return false;
            if (IsIconic(hWnd) || IsZoomed(hWnd)) ShowWindow(hWnd, SW_RESTORE);
            return IsWindow(hWnd) && !IsIconic(hWnd) && !IsZoomed(hWnd);
        }

        /// <summary>
        /// ウィンドウのサイズのみを変更（位置は維持）
        /// </summary>
        /// <param name="hWnd">ウィンドウハンドル</param>
        /// <param name="width">新しい幅</param>
        /// <param name="height">新しい高さ</param>
        public virtual bool SetWindowSize(IntPtr hWnd, int width, int height)
        {
            if (hWnd == IntPtr.Zero) return false;
            var rect = GetWindowPosition(hWnd);
            return SetWindowPosition(hWnd, rect.Left, rect.Top, width, height);
        }

        /// <summary>
        /// ウィンドウが存在するモニターの識別子を取得
        /// </summary>
        /// <param name="hWnd">ウィンドウハンドル</param>
        /// <returns>モニターのデバイス名</returns>
        public virtual string GetMonitorId(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return string.Empty;

            IntPtr hMonitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
            MONITORINFOEX monitorInfo = new MONITORINFOEX();
            monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));

            if (GetMonitorInfo(hMonitor, ref monitorInfo))
            {
                return monitorInfo.szDevice;
            }
            return string.Empty;
        }

        /// <summary>
        /// 全モニター情報を取得
        /// </summary>
        /// <returns>モニター情報のリスト</returns>
        public virtual List<MonitorInfo> GetAllMonitors()
        {
            var monitors = new List<MonitorInfo>();

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
            {
                MONITORINFOEX monitorInfo = new MONITORINFOEX();
                monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));

                if (GetMonitorInfo(hMonitor, ref monitorInfo))
                {
                    monitors.Add(new MonitorInfo
                    {
                        DeviceName = monitorInfo.szDevice,
                        Bounds = monitorInfo.rcMonitor,
                        WorkArea = monitorInfo.rcWork,
                        IsPrimary = (monitorInfo.dwFlags & 1) != 0
                    });
                }
                return true;
            }, IntPtr.Zero);

            return monitors;
        }

        /// <summary>
        /// WindowInfoの位置・サイズ情報を更新
        /// </summary>
        /// <param name="windowInfo">更新対象のWindowInfo</param>
        public void UpdateWindowPositionInfo(WindowInfo windowInfo)
        {
            if (windowInfo.Handle == IntPtr.Zero) return;

            var rect = GetWindowPosition(windowInfo.Handle);
            windowInfo.Left = rect.Left;
            windowInfo.Top = rect.Top;
            windowInfo.Width = rect.Width;
            windowInfo.Height = rect.Height;
            windowInfo.MonitorId = GetMonitorId(windowInfo.Handle);
        }
    }

    /// <summary>
    /// モニター情報
    /// </summary>
    public class MonitorInfo
    {
        public string DeviceName { get; set; } = string.Empty;
        public WindowService.RECT Bounds { get; set; }
        public WindowService.RECT WorkArea { get; set; }
        public bool IsPrimary { get; set; }
    }
}
