using System;
using System.Windows;
using Microsoft.Win32;

namespace SGuardLimiterMax.Services
{
    public static class ThemeManager
    {
        private const string LightThemePath = "Resources/LightTheme.xaml";
        private const string DarkThemePath  = "Resources/DarkTheme.xaml";

        public static bool IsDarkTheme { get; private set; }

        public static void Initialize()
        {
            IsDarkTheme = DetectSystemTheme();
            ApplyTheme(IsDarkTheme);
        }

        private static bool DetectSystemTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var value = key?.GetValue("AppsUseLightTheme");
                if (value is int intVal)
                    return intVal == 0; // 0 = dark, 1 = light
            }
            catch { }
            return false; // default to light
        }

        private static void ApplyTheme(bool dark)
        {
            var dict = new ResourceDictionary
            {
                Source = new Uri(dark ? DarkThemePath : LightThemePath, UriKind.Relative)
            };

            // Remove any previously loaded theme dictionary
            var toRemove = Application.Current.Resources.MergedDictionaries;
            for (int i = toRemove.Count - 1; i >= 0; i--)
            {
                var src = toRemove[i].Source?.ToString() ?? "";
                if (src.Contains("Theme.xaml"))
                    toRemove.RemoveAt(i);
            }

            Application.Current.Resources.MergedDictionaries.Add(dict);
        }
    }
}
