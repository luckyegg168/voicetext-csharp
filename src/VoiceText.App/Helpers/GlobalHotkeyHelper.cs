// src/VoiceText.App/Helpers/GlobalHotkeyHelper.cs
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VoiceText.App.Helpers;

/// <summary>
/// Low-level keyboard hook that fires HotkeyPressed on key-down and
/// HotkeyReleased on key-up for a configurable modifier+key combo.
/// Supports push-to-talk scenarios.
/// </summary>
public class GlobalHotkeyHelper : IDisposable
{
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc fn, IntPtr hMod, uint threadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? lpModuleName);
    [DllImport("user32.dll")] private static extern short GetKeyState(int vk);

    private const int WH_KEYBOARD_LL  = 13;
    private const int WM_KEYDOWN      = 0x0100;
    private const int WM_KEYUP        = 0x0101;
    private const int WM_SYSKEYDOWN   = 0x0104;
    private const int WM_SYSKEYUP     = 0x0105;
    private const int VK_CONTROL      = 0x11;
    private const int VK_MENU         = 0x12;  // Alt
    private const int VK_SHIFT        = 0x10;

    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookId;
    private readonly int _triggerVk;
    private readonly bool _needCtrl, _needAlt, _needShift;
    private bool _hotkeyDown;

    /// <summary>Fires once on key-down (toggle mode and push-to-talk).</summary>
    public event EventHandler? HotkeyPressed;
    /// <summary>Fires on key-up; only meaningful for push-to-talk mode.</summary>
    public event EventHandler? HotkeyReleased;

    /// <param name="vk">Virtual key code. Default 0x77 = F8.</param>
    /// <param name="needCtrl">Require Ctrl.</param>
    /// <param name="needAlt">Require Alt.</param>
    /// <param name="needShift">Require Shift.</param>
    public GlobalHotkeyHelper(int vk = 0x77, bool needCtrl = true, bool needAlt = true, bool needShift = false)
    {
        _triggerVk  = vk;
        _needCtrl   = needCtrl;
        _needAlt    = needAlt;
        _needShift  = needShift;
        _proc = HookCallback;
        using var proc   = Process.GetCurrentProcess();
        using var module = proc.MainModule!;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(module.ModuleName), 0);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int vk  = Marshal.ReadInt32(lParam);
            int msg = wParam.ToInt32();

            if (vk == _triggerVk)
            {
                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                {
                    if (!_hotkeyDown && ModifiersDown())
                    {
                        _hotkeyDown = true;
                        HotkeyPressed?.Invoke(this, EventArgs.Empty);
                    }
                }
                else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                {
                    if (_hotkeyDown)
                    {
                        _hotkeyDown = false;
                        HotkeyReleased?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private bool ModifiersDown()
    {
        if (_needCtrl  && (GetKeyState(VK_CONTROL) & 0x8000) == 0) return false;
        if (_needAlt   && (GetKeyState(VK_MENU)    & 0x8000) == 0) return false;
        if (_needShift && (GetKeyState(VK_SHIFT)   & 0x8000) == 0) return false;
        return true;
    }

    public void Dispose()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }
}

public static class HotkeyModifiers
{
    public const uint Alt   = 0x0001;
    public const uint Ctrl  = 0x0002;
    public const uint Shift = 0x0004;
    public const uint Win   = 0x0008;
}
