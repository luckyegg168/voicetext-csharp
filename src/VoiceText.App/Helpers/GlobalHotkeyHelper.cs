// src/VoiceText.App/Helpers/GlobalHotkeyHelper.cs
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace VoiceText.App.Helpers;

public class GlobalHotkeyHelper : IDisposable
{
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int HotkeyId = 9001;
    private IntPtr _hwnd;
    private HwndSource? _source;

    public event EventHandler? HotkeyPressed;

    public void Register(Window window, uint modifiers, uint vk)
    {
        _hwnd = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        _source.AddHook(WndProc);
        RegisterHotKey(_hwnd, HotkeyId, modifiers, vk);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x0312 && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterHotKey(_hwnd, HotkeyId);
        _source?.RemoveHook(WndProc);
    }
}

public static class HotkeyModifiers
{
    public const uint Alt = 0x0001;
    public const uint Ctrl = 0x0002;
    public const uint Shift = 0x0004;
    public const uint Win = 0x0008;
}
