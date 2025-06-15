using System;
using System.Windows;

namespace WindowSlu.Services
{
    public enum Theme { Light, Dark }

    public class ThemeService
    {
        private const string LightThemeUri = "/Themes/LightTheme.xaml";
        private const string DarkThemeUri = "/Themes/DarkTheme.xaml";

        public void ApplyTheme(Theme theme)
        {
            var app = System.Windows.Application.Current;
            if (app == null) return;

            app.Resources.MergedDictionaries.Clear();

            string themeUri = theme == Theme.Light ? LightThemeUri : DarkThemeUri;
            
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri(themeUri, UriKind.Relative)
            });
        }
    }
} 