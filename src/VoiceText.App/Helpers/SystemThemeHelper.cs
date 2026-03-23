// src/VoiceText.App/Helpers/SystemThemeHelper.cs
using Microsoft.Win32;

namespace VoiceText.App.Helpers;

public static class SystemThemeHelper
{
    public static bool IsDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i && i == 0;
        }
        catch
        {
            return false;
        }
    }
}
