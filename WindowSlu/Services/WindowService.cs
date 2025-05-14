using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace WindowSlu.Services
{
    /// <summary>
    /// ウィンドウ操作に関するサービスクラス
    /// </summary>
    public class WindowService
    {
        // Win32 API 関数のインポート
        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        // 定数
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const uint LWA_ALPHA = 0x2;

        // 最前面化関連の定数
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;

        // デリゲート
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        // ウィンドウ一覧
        private ObservableCollection<WindowInfo> windowList;

        public WindowService()
        {
            windowList = new ObservableCollection<WindowInfo>();
        }

        /// <summary>
        /// ウィンドウ一覧を取得
        /// </summary>
        public ObservableCollection<WindowInfo> GetWindowList()
        {
            return windowList;
        }

        /// <summary>
        /// ウィンドウ一覧を更新
        /// </summary>
        public void RefreshWindowList()
        {
            try
            {
                windowList.Clear();
                EnumWindows(EnumWindowsCallback, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ウィンドウ一覧更新エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// ウィンドウの透明度を設定
        /// </summary>
        public void SetWindowTransparency(IntPtr hWnd, byte alpha)
        {
            try
            {
                if (hWnd == IntPtr.Zero)
                    return;

                // ウィンドウスタイルを取得
                int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);

                // レイヤードウィンドウスタイルを追加
                if ((exStyle & WS_EX_LAYERED) == 0)
                {
                    SetWindowLong(hWnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED);
                }

                // 透明度を設定
                SetLayeredWindowAttributes(hWnd, 0, alpha, LWA_ALPHA);

                // ウィンドウリストの透明度情報を更新
                UpdateWindowTransparencyInList(hWnd, (int)(alpha / 2.55)); // 255 -> 100 に変換
            }
            catch (Exception ex)
            {
                Console.WriteLine($"透明度設定エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// ウィンドウリストの透明度情報を更新
        /// </summary>
        private void UpdateWindowTransparencyInList(IntPtr hWnd, int transparency)
        {
            foreach (var window in windowList)
            {
                if (window.Handle == hWnd)
                {
                    window.Transparency = transparency;
                    break;
                }
            }
        }

        /// <summary>
        /// EnumWindowsのコールバック
        /// </summary>
        private bool EnumWindowsCallback(IntPtr hWnd, IntPtr lParam)
        {
            try
            {
                // 非表示ウィンドウは除外
                if (!IsWindowVisible(hWnd))
                    return true;

                // ウィンドウタイトルを取得
                StringBuilder titleBuilder = new StringBuilder(256);
                GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
                string title = titleBuilder.ToString().Trim();

                // タイトルが空のウィンドウは除外
                if (string.IsNullOrEmpty(title))
                    return true;

                // プロセスIDを取得
                uint processId;
                GetWindowThreadProcessId(hWnd, out processId);

                // プロセス名を取得
                string processName = "";
                try
                {
                    using (Process process = Process.GetProcessById((int)processId))
                    {
                        processName = process.ProcessName;
                    }
                }
                catch
                {
                    // プロセスが取得できない場合は無視
                }

                // 自分自身のウィンドウは除外
                if (processName.ToLower() == "windowslu" || title == "WindowSlu")
                    return true;

                // ウィンドウ情報をリストに追加
                windowList.Add(new WindowInfo
                {
                    Handle = hWnd,
                    Title = title,
                    ProcessName = processName,
                    ProcessId = (int)processId,
                    Transparency = 100 // デフォルトは不透明
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ウィンドウ列挙エラー: {ex.Message}");
            }

            return true;
        }

        /// <summary>
        /// アクティブウィンドウのハンドルを取得
        /// </summary>
        public IntPtr GetActiveWindow()
        {
            return GetForegroundWindow();
        }

        /// <summary>
        /// ウィンドウの最前面化状態を設定
        /// </summary>
        /// <param name="hWnd">ウィンドウハンドル</param>
        /// <param name="topMost">最前面化するかどうか</param>
        /// <returns>成功した場合はtrue</returns>
        public bool SetWindowTopMost(IntPtr hWnd, bool topMost)
        {
            try
            {
                if (hWnd == IntPtr.Zero)
                    return false;

                IntPtr flag = topMost ? HWND_TOPMOST : HWND_NOTOPMOST;

                // ウィンドウの位置とサイズを変更せずに、Z順序のみを変更
                bool result = SetWindowPos(hWnd, flag, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

                if (result)
                {
                    // ウィンドウリストの最前面化状態を更新
                    UpdateWindowTopMostState(hWnd, topMost);
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"最前面化設定エラー: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// ウィンドウの最前面化状態を更新
        /// </summary>
        private void UpdateWindowTopMostState(IntPtr hWnd, bool topMost)
        {
            foreach (var window in windowList)
            {
                if (window.Handle == hWnd)
                {
                    window.IsTopMost = topMost;
                    break;
                }
            }
        }

        /// <summary>
        /// ウィンドウの最前面化状態を取得
        /// </summary>
        public bool IsWindowTopMost(IntPtr hWnd)
        {
            foreach (var window in windowList)
            {
                if (window.Handle == hWnd)
                {
                    return window.IsTopMost;
                }
            }
            return false;
        }
    }
}
