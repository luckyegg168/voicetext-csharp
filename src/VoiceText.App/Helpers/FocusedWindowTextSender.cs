using System.Runtime.InteropServices;
using WinForms = System.Windows.Forms;

namespace VoiceText.App.Helpers;

public static class FocusedWindowTextSender
{
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    private const int SwRestore = 9;

    public static async Task SendAsync(IntPtr hwnd, string text)
    {
        if (hwnd == IntPtr.Zero || string.IsNullOrWhiteSpace(text))
            return;

        await ClipboardHelper.WithTemporaryTextAsync(text, async () =>
        {
            await BringToFrontAsync(hwnd);
            WinForms.SendKeys.SendWait("^v");
            await Task.Delay(100);

            if (GetForegroundWindow() != hwnd)
            {
                await BringToFrontAsync(hwnd);
                WinForms.SendKeys.SendWait("^v");
                await Task.Delay(100);
            }
        });
    }

    private static async Task BringToFrontAsync(IntPtr hwnd)
    {
        ShowWindowAsync(hwnd, SwRestore);
        SetForegroundWindow(hwnd);
        await Task.Delay(200);
    }
}
