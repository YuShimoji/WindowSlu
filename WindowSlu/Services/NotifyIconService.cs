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
                // アイコンのサイズ
                int iconSize = 32;

                // ビットマップを作成
                using (Bitmap bitmap = new Bitmap(iconSize, iconSize))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        // 背景を透明に
                        g.Clear(Color.Transparent);

                        // 円を描画
                        using (SolidBrush brush = new SolidBrush(Color.FromArgb(0, 120, 215)))
                        using (Pen pen = new Pen(Color.White, 2))
                        {
                            g.FillEllipse(brush, 4, 4, iconSize - 8, iconSize - 8);
                            g.DrawEllipse(pen, 4, 4, iconSize - 8, iconSize - 8);
                        }

                        // 文字を描画
                        using (Font font = new Font("Arial", 12, FontStyle.Bold))
                        using (SolidBrush textBrush = new SolidBrush(Color.White))
                        {
                            StringFormat format = new StringFormat
                            {
                                Alignment = StringAlignment.Center,
                                LineAlignment = StringAlignment.Center
                            };

                            g.DrawString("W", font, textBrush, new RectangleF(0, 0, iconSize, iconSize), format);
                        }
                    }

                    // ビットマップからアイコンを作成
                    IntPtr hIcon = bitmap.GetHicon();
                    return Icon.FromHandle(hIcon);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"アイコン作成エラー: {ex.Message}");
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
