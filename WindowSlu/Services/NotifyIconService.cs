using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows; // Required for Application.GetResourceStream
using System.Windows.Resources; // Required for StreamResourceInfo
using System.IO; // Required for Stream

namespace WindowSlu.Services
{
    /// <summary>
    /// タスクトレイアイコンに関するサービスクラス
    /// </summary>
    public class NotifyIconService
    {
        // タスクトレイアイコン
        private NotifyIcon? notifyIcon;

        // メインウィンドウへの参照
        private MainWindow mainWindow;

        public NotifyIconService(MainWindow window)
        {
            mainWindow = window;
            InitializeNotifyIcon();
        }

        /// <summary>
        /// タスクトレイアイコンの初期化
        /// </summary>
        private void InitializeNotifyIcon()
        {
            try
            {
                // アプリケーションアイコンの作成
                Icon appIcon = CreateApplicationIcon();

                notifyIcon = new NotifyIcon
                {
                    Icon = appIcon,
                    Visible = true,
                    Text = "WindowSlu"
                };

                // コンテキストメニューの作成
                ContextMenuStrip contextMenu = new ContextMenuStrip();

                var showItem = new ToolStripMenuItem("表示");
                showItem.Click += (s, e) =>
                {
                    try
                    {
                        mainWindow.Show();
                        mainWindow.WindowState = System.Windows.WindowState.Normal;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ウィンドウ表示エラー: {ex.Message}");
                    }
                };
                contextMenu.Items.Add(showItem);

                var exitItem = new ToolStripMenuItem("終了");
                exitItem.Click += (s, e) =>
                {
                    try
                    {
                        // タスクトレイアイコンを削除してからウィンドウを閉じる
                        Dispose();

                        // ウィンドウを閉じる
                        mainWindow.Close();

                        // 確実に終了するためのバックアップ
                        System.Windows.Application.Current.Shutdown();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"終了エラー: {ex.Message}");
                        // 強制終了を試みる
                        Environment.Exit(0);
                    }
                };
                contextMenu.Items.Add(exitItem);

                notifyIcon.ContextMenuStrip = contextMenu;

                // アイコンダブルクリックでウィンドウを表示
                notifyIcon.DoubleClick += (s, e) =>
                {
                    try
                    {
                        mainWindow.Show();
                        mainWindow.WindowState = System.Windows.WindowState.Normal;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ウィンドウ表示エラー: {ex.Message}");
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"タスクトレイアイコン初期化エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// アプリケーションアイコンの作成
        /// </summary>
        private Icon CreateApplicationIcon()
        {
            try
            {
                // Construct the pack URI for the embedded resource.
                // Assumes 'Resources/WindowSlu_Icon.ico' is relative to the project root
                // and the build action is set to 'Resource'.
                Uri iconUri = new Uri("pack://application:,,,/Resources/WindowSlu_Icon.ico", UriKind.RelativeOrAbsolute);
                StreamResourceInfo? streamInfo = System.Windows.Application.GetResourceStream(iconUri); // Fully qualify Application

                if (streamInfo != null)
                {
                    using (Stream iconStream = streamInfo.Stream)
                    {
                        return new Icon(iconStream);
                    }
                }
                else
                {
                    Console.WriteLine($"タスクトレイアイコンリソースが見つかりません: {iconUri}");
                    return SystemIcons.Application; // Fallback
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"タスクトレイアイコンリソース読み込みエラー: {ex.Message}");
                return SystemIcons.Application; // Fallback
            }
        }

        /// <summary>
        /// タスクトレイアイコンの破棄
        /// </summary>
        public void Dispose()
        {
            if (notifyIcon != null)
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
                notifyIcon = null;
            }
        }
    }
}
