using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace WindowSlu.Services
{
    /// <summary>
    /// テーマ（ダークモード/ライトモード）に関するサービスクラス
    /// </summary>
    public class ThemeService
    {
        // Windows APIの定義
        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        // DWM_WINDOW_ATTRIBUTE
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        // テーマの種類
        public enum ThemeType
        {
            Light,
            Dark
        }

        // 現在のテーマ
        private ThemeType currentTheme;

        // テーマ変更イベント
        public event EventHandler<ThemeType>? ThemeChanged;

        // リソースディクショナリのパス
        private const string DarkThemeResourcePath = "Themes/DarkTheme.xaml";
        private const string LightThemeResourcePath = "Themes/LightTheme.xaml";

        public ThemeService()
        {
            // システムのテーマ設定を取得
            currentTheme = IsSystemUsingDarkTheme() ? ThemeType.Dark : ThemeType.Light;

            // 初期テーマを適用
            ApplyTheme(currentTheme);
        }

        /// <summary>
        /// システムがダークテーマを使用しているかどうかを取得
        /// </summary>
        public bool IsSystemUsingDarkTheme()
        {
            try
            {
                // レジストリからシステムのテーマ設定を取得
                Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

                if (key != null)
                {
                    object? value = key.GetValue("AppsUseLightTheme");
                    if (value != null)
                    {
                        // 0ならダークテーマ、1ならライトテーマ
                        return (int)value == 0;
                    }
                }

                // デフォルトはダークテーマ
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"システムテーマ取得エラー: {ex.Message}");
                return true; // エラー時はダークテーマをデフォルトとする
            }
        }

        /// <summary>
        /// 現在のテーマを取得
        /// </summary>
        public ThemeType GetCurrentTheme()
        {
            return currentTheme;
        }

        /// <summary>
        /// テーマを切り替え
        /// </summary>
        public void ToggleTheme()
        {
            ThemeType newTheme = (currentTheme == ThemeType.Dark) ? ThemeType.Light : ThemeType.Dark;
            ApplyTheme(newTheme);
        }

        /// <summary>
        /// 指定したテーマを適用
        /// </summary>
        public void ApplyTheme(ThemeType theme)
        {
            try
            {
                // テーマを更新
                currentTheme = theme;

                // アプリケーションのリソースを更新
                UpdateApplicationResources(theme);

                // テーマ変更イベントを発火
                ThemeChanged?.Invoke(this, theme);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"テーマ適用エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// アプリケーションのリソースを更新
        /// </summary>
        private void UpdateApplicationResources(ThemeType theme)
        {
            try
            {
                // アプリケーションのリソースディクショナリを取得
                ResourceDictionary resources = Application.Current.Resources;

                // テーマに応じた色を設定
                if (theme == ThemeType.Dark)
                {
                    // ダークテーマの色
                    resources["BackgroundColor"] = new SolidColorBrush(Color.FromRgb(45, 45, 48)); // #2D2D30
                    resources["ForegroundColor"] = new SolidColorBrush(Color.FromRgb(230, 230, 230)); // #E6E6E6
                    resources["BorderColor"] = new SolidColorBrush(Color.FromRgb(63, 63, 70)); // #3F3F46
                    resources["ControlColor"] = new SolidColorBrush(Color.FromRgb(51, 51, 55)); // #333337
                    resources["ControlBorderColor"] = new SolidColorBrush(Color.FromRgb(77, 77, 80)); // #4D4D50
                    resources["HighlightColor"] = new SolidColorBrush(Color.FromRgb(0, 122, 204)); // #007ACC
                    resources["ListBackgroundColor"] = new SolidColorBrush(Color.FromRgb(37, 37, 38)); // #252526
                    resources["ListItemColor"] = new SolidColorBrush(Color.FromRgb(45, 45, 48)); // #2D2D30
                    resources["ListItemSelectedColor"] = new SolidColorBrush(Color.FromRgb(63, 63, 70)); // #3F3F46
                    resources["TabBackgroundColor"] = new SolidColorBrush(Color.FromRgb(37, 37, 38)); // #252526
                    resources["TabSelectedColor"] = new SolidColorBrush(Color.FromRgb(63, 63, 70)); // #3F3F46
                    resources["TabSelectedTextColor"] = new SolidColorBrush(Color.FromRgb(255, 255, 255)); // #FFFFFF - 白色
                    resources["TitleBarColor"] = new SolidColorBrush(Color.FromRgb(30, 30, 30)); // #1E1E1E
                }
                else
                {
                    // ライトテーマの色
                    resources["BackgroundColor"] = new SolidColorBrush(Color.FromRgb(240, 240, 240)); // #F0F0F0
                    resources["ForegroundColor"] = new SolidColorBrush(Color.FromRgb(0, 0, 0)); // #000000
                    resources["BorderColor"] = new SolidColorBrush(Color.FromRgb(204, 204, 204)); // #CCCCCC
                    resources["ControlColor"] = new SolidColorBrush(Color.FromRgb(255, 255, 255)); // #FFFFFF
                    resources["ControlBorderColor"] = new SolidColorBrush(Color.FromRgb(170, 170, 170)); // #AAAAAA
                    resources["HighlightColor"] = new SolidColorBrush(Color.FromRgb(0, 120, 215)); // #0078D7
                    resources["ListBackgroundColor"] = new SolidColorBrush(Color.FromRgb(255, 255, 255)); // #FFFFFF
                    resources["ListItemColor"] = new SolidColorBrush(Color.FromRgb(245, 245, 245)); // #F5F5F5
                    resources["ListItemSelectedColor"] = new SolidColorBrush(Color.FromRgb(229, 229, 229)); // #E5E5E5
                    resources["TabBackgroundColor"] = new SolidColorBrush(Color.FromRgb(240, 240, 240)); // #F0F0F0
                    resources["TabSelectedColor"] = new SolidColorBrush(Color.FromRgb(255, 255, 255)); // #FFFFFF
                    resources["TabSelectedTextColor"] = new SolidColorBrush(Color.FromRgb(0, 0, 0)); // #000000 - 黒色
                    resources["TitleBarColor"] = new SolidColorBrush(Color.FromRgb(240, 240, 240)); // #F0F0F0
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"リソース更新エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// ウィンドウにダークモードを適用
        /// </summary>
        public void ApplyDarkModeToWindow(IntPtr handle, bool isDarkMode)
        {
            try
            {
                int value = isDarkMode ? 1 : 0;
                DwmGetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ウィンドウダークモード適用エラー: {ex.Message}");
            }
        }
    }
}
