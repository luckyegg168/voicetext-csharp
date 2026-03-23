// src/VoiceText.App/Helpers/ClipboardHelper.cs
namespace VoiceText.App.Helpers;

public static class ClipboardHelper
{
    public static void SetText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        System.Windows.Clipboard.SetText(text);
    }
}
