using System;
using System.Drawing;
using System.Windows.Forms;

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
                // アプリケーションの実行ファイルと同じディレクトリにあると仮定
                // string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "WindowSlu_Icon.ico");
                // より確実な方法として、アセンブリの場所を取得する
                string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string assemblyDirectory = System.IO.Path.GetDirectoryName(assemblyLocation) ?? "";
                string iconPath = System.IO.Path.Combine(assemblyDirectory, "Resources", "WindowSlu_Icon.ico");

                if (System.IO.File.Exists(iconPath))
                {
                    return new Icon(iconPath);
                }
                else
                {
                    Console.WriteLine($"タスクトレイアイコンファイルが見つかりません: {iconPath}");
                    return SystemIcons.Application; // フォールバック
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"タスクトレイアイコン読み込みエラー: {ex.Message}");
                return SystemIcons.Application; // フォールバック
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
