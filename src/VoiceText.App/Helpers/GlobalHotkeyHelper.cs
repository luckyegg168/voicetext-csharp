using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace VoiceText.App.Helpers;

/// <summary>
/// Global keyboard hook that does not rely on a WPF window handle.
/// It supports both press and release events for push-to-talk.
/// </summary>
public sealed class GlobalHotkeyHelper : IDisposable
{
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private const int WhKeyboardLl = 13;
    private const int WmKeydown = 0x0100;
    private const int WmKeyup = 0x0101;
    private const int WmSyskeydown = 0x0104;
    private const int WmSyskeyup = 0x0105;
    private const int VkCtrl = 0x11;
    private const int VkAlt = 0x12;
    private const int VkShift = 0x10;
    private const int VkLwin = 0x5B;
    private const int VkRwin = 0x5C;

    private readonly LowLevelKeyboardProc _proc;
    private readonly int _triggerVk;
    private readonly bool _needCtrl;
    private readonly bool _needAlt;
    private readonly bool _needShift;
    private readonly bool _needWin;
    private IntPtr _hookId;
    private bool _hotkeyDown;

    public event EventHandler? HotkeyPressed;
    public event EventHandler? HotkeyReleased;

    public GlobalHotkeyHelper(string hotkey)
        : this(ParseGesture(hotkey))
    {
    }

    public GlobalHotkeyHelper(HotkeyGesture gesture)
    {
        _triggerVk = gesture.VirtualKey;
        _needCtrl = gesture.Ctrl;
        _needAlt = gesture.Alt;
        _needShift = gesture.Shift;
        _needWin = gesture.Win;

        _proc = HookCallback;
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        var moduleHandle = GetModuleHandle(module?.ModuleName);
        _hookId = SetWindowsHookEx(WhKeyboardLl, _proc, moduleHandle, 0);
        if (_hookId == IntPtr.Zero)
            throw new InvalidOperationException("無法安裝全域快捷鍵鍵盤 hook。");
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = wParam.ToInt32();
            var vkCode = Marshal.ReadInt32(lParam);

            if (vkCode == _triggerVk)
            {
                if (message is WmKeydown or WmSyskeydown)
                {
                    if (!_hotkeyDown && ModifiersMatch())
                    {
                        _hotkeyDown = true;
                        HotkeyPressed?.Invoke(this, EventArgs.Empty);
                        return (IntPtr)1;
                    }
                }
                else if (message is WmKeyup or WmSyskeyup)
                {
                    if (_hotkeyDown)
                    {
                        _hotkeyDown = false;
                        HotkeyReleased?.Invoke(this, EventArgs.Empty);
                        return (IntPtr)1;
                    }
                }
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private bool ModifiersMatch()
    {
        if (_needCtrl && !IsPressed(VkCtrl)) return false;
        if (_needAlt && !IsPressed(VkAlt)) return false;
        if (_needShift && !IsPressed(VkShift)) return false;
        if (_needWin && !(IsPressed(VkLwin) || IsPressed(VkRwin))) return false;
        return true;
    }

    private static bool IsPressed(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    public static HotkeyGesture ParseGesture(string? hotkey)
    {
        if (string.IsNullOrWhiteSpace(hotkey))
            return new HotkeyGesture(0x77, Ctrl: true, Alt: true, Shift: false, Win: false);

        bool ctrl = false, alt = false, shift = false, win = false;
        int? vk = null;

        foreach (var rawPart in hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var part = rawPart.Trim();
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                ctrl = true;
                continue;
            }

            if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                alt = true;
                continue;
            }

            if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                shift = true;
                continue;
            }

            if (part.Equals("Win", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            {
                win = true;
                continue;
            }

            vk = ParseVirtualKey(part);
        }

        if (vk is null)
            throw new FormatException($"無法解析快捷鍵: {hotkey}");

        return new HotkeyGesture(vk.Value, ctrl, alt, shift, win);
    }

    private static int ParseVirtualKey(string key)
    {
        if (key.Length == 1)
        {
            var upper = char.ToUpperInvariant(key[0]);
            if (upper is >= 'A' and <= 'Z') return upper;
            if (upper is >= '0' and <= '9') return upper;
        }

        if (key.StartsWith("F", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(key[1..], NumberStyles.None, CultureInfo.InvariantCulture, out var functionNumber) &&
            functionNumber is >= 1 and <= 24)
        {
            return 0x70 + functionNumber - 1;
        }

        return key.ToUpperInvariant() switch
        {
            "SPACE" => 0x20,
            "ENTER" => 0x0D,
            "TAB" => 0x09,
            "ESC" or "ESCAPE" => 0x1B,
            _ => throw new FormatException($"不支援的按鍵: {key}")
        };
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

public readonly record struct HotkeyGesture(int VirtualKey, bool Ctrl, bool Alt, bool Shift, bool Win);
