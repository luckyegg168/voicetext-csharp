// src/VoiceText.App/Helpers/ClipboardHelper.cs
namespace VoiceText.App.Helpers;

public static class ClipboardHelper
{
    public static void SetText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        System.Windows.Clipboard.SetText(text);
    }

    public static async Task WithTemporaryTextAsync(string text, Func<Task> action)
    {
        if (string.IsNullOrEmpty(text))
            return;

        System.Windows.IDataObject? original = null;
        try
        {
            if (System.Windows.Clipboard.ContainsData(DataFormats.Text) ||
                System.Windows.Clipboard.ContainsData(DataFormats.UnicodeText))
            {
                original = System.Windows.Clipboard.GetDataObject();
            }
        }
        catch
        {
            original = null;
        }

        System.Windows.Clipboard.SetText(text);
        try
        {
            await action();
        }
        finally
        {
            try
            {
                if (original != null)
                    System.Windows.Clipboard.SetDataObject(original, true);
                else
                    System.Windows.Clipboard.Clear();
            }
            catch
            {
                // Ignore clipboard restore failures to avoid blocking transcription flow.
            }
        }
    }
}
