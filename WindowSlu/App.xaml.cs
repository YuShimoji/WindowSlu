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
            // Get the current merged dictionaries
            var dictionaries = Resources.MergedDictionaries;
            
            // Clear all theme-related dictionaries to avoid conflicts
            var oldThemes = dictionaries.Where(d =>
                d.Source != null && (d.Source.OriginalString.Contains("LightTheme.xaml") || d.Source.OriginalString.Contains("DarkTheme.xaml"))
            ).ToList();
            
            foreach (var oldTheme in oldThemes)
            {
                dictionaries.Remove(oldTheme);
            }

            // Add the new theme dictionary
            var themeName = theme == Theme.Light ? "LightTheme" : "DarkTheme";
            var newThemeDict = new ResourceDictionary
            {
                Source = new Uri($"Themes/{themeName}.xaml", UriKind.Relative)
            };
            dictionaries.Add(newThemeDict);
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
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Apply the theme at startup
            ApplySystemTheme();

            // Create and show the main window
            var mainWindow = new MainWindow();
            mainWindow.Show();
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
            // Show a message box with the exception details
            var errorMessage = $"An unexpected error occurred: {e.Exception.Message}\n\n{e.Exception.StackTrace}";
            MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            
            // Log the exception (optional)
            Console.WriteLine($"DispatcherUnhandledException: {e.Exception.Message}");
            Console.WriteLine($"StackTrace: {e.Exception.StackTrace}");

            // Mark the exception as handled to prevent the application from crashing
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Show a message box with the exception details
            var ex = e.ExceptionObject as Exception;
            var errorMessage = $"A critical unhandled error occurred: {(ex != null ? ex.Message : e.ExceptionObject.ToString())}\n\n{(ex != null ? ex.StackTrace : string.Empty)}";
            MessageBox.Show(errorMessage, "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);

            // Log the exception (optional)
            if (ex != null)
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