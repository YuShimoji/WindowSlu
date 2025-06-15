using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace WindowSlu
{
    public enum Theme { Light, Dark }

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        public void ChangeTheme(Theme theme)
        {
            var dictionaries = Resources.MergedDictionaries;

            // 既存のテーマディクショナリを削除
            var oldTheme = dictionaries.FirstOrDefault(d =>
                d.Source != null && (d.Source.OriginalString.Contains("LightTheme.xaml") || d.Source.OriginalString.Contains("DarkTheme.xaml"))
            );

            if (oldTheme != null)
            {
                dictionaries.Remove(oldTheme);
            }

            // 新しいテーマディクショナリを追加
            var themeName = theme == Theme.Light ? "LightTheme" : "DarkTheme";
            var dict = new ResourceDictionary
            {
                Source = new Uri($"Themes/{themeName}.xaml", UriKind.Relative)
            };
            dictionaries.Add(dict);
        }

        public App()
        {
            // アプリケーションの例外ハンドラを追加
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // システムのテーマ変更イベントを購読
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

            // アプリケーション終了時にイベントハンドラを解除
            this.Exit += (s, e) => { SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged; };

            // 起動時に現在のシステムテーマを適用
            ApplySystemTheme();
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General)
            {
                // UIスレッドでテーマを適用
                Dispatcher.Invoke(ApplySystemTheme);
            }
        }

        private void ApplySystemTheme()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key?.GetValue("AppsUseLightTheme") is int appsUseLightTheme && appsUseLightTheme == 0)
                    {
                        ChangeTheme(Theme.Dark);
                    }
                    else
                    {
                        ChangeTheme(Theme.Light);
                    }
                }
            }
            catch (Exception ex)
            {
                // Fallback to a default theme
                ChangeTheme(Theme.Dark);
                Console.WriteLine($"Error applying system theme: {ex.Message}");
            }
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            // 例外情報をコンソールに出力
            Console.WriteLine($"DispatcherUnhandledException: {e.Exception.Message}");
            Console.WriteLine($"StackTrace: {e.Exception.StackTrace}");

            // 例外を処理済みとしてマーク
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // 例外情報をコンソールに出力
            if (e.ExceptionObject is Exception ex)
            {
                Console.WriteLine($"UnhandledException: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
            }
            else
            {
                Console.WriteLine($"UnhandledException: {e.ExceptionObject}");
            }
        }
    }
}