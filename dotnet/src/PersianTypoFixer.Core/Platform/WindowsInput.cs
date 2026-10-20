using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace PersianTypoFixer.Core.Platform;

public sealed class WindowsInput : IInputBackend
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);

    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    private const byte VK_CONTROL = 0x11;
    private const byte VK_SHIFT = 0x10;
    private const byte VK_BACK = 0x08;
    private const byte VK_C = 0x43;
    private const byte VK_V = 0x56;

    private const uint KEYEVENTF_KEYUP = 0x0002;

    public string? GetSelection() => null;

    public string GetClipboard()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (!OpenClipboard(IntPtr.Zero))
            {
                Thread.Sleep(15);
                continue;
            }
            try
            {
                var handle = GetClipboardData(CF_UNICODETEXT);
                if (handle == IntPtr.Zero)
                    return "";
                var pointer = GlobalLock(handle);
                if (pointer == IntPtr.Zero)
                    return "";
                try
                {
                    return Marshal.PtrToStringUni(pointer) ?? "";
                }
                finally
                {
                    GlobalUnlock(handle);
                }
            }
            finally
            {
                CloseClipboard();
            }
        }
        return "";
    }

    public void SetClipboard(string text)
    {
        text ??= "";
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (!OpenClipboard(IntPtr.Zero))
            {
                Thread.Sleep(15);
                continue;
            }
            try
            {
                EmptyClipboard();
                var bytes = (text.Length + 1) * 2;
                var mem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes);
                if (mem == IntPtr.Zero)
                    return;
                var target = GlobalLock(mem);
                if (target == IntPtr.Zero)
                {
                    GlobalFree(mem);
                    return;
                }
                try
                {
                    var buffer = Encoding.Unicode.GetBytes(text + "\0");
                    Marshal.Copy(buffer, 0, target, buffer.Length);
                }
                finally
                {
                    GlobalUnlock(mem);
                }
                if (SetClipboardData(CF_UNICODETEXT, mem) == IntPtr.Zero)
                    GlobalFree(mem);
                return;
            }
            finally
            {
                CloseClipboard();
            }
        }
    }

    public void PressBackspace(int count)
    {
        for (var i = 0; i < count; i++)
        {
            keybd_event(VK_BACK, 0, 0, UIntPtr.Zero);
            keybd_event(VK_BACK, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            Thread.Sleep(5);
        }
    }

    public void Paste()
    {
        keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
        keybd_event(VK_V, 0, 0, UIntPtr.Zero);
        keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    public void Copy()
    {
        keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
        keybd_event(VK_C, 0, 0, UIntPtr.Zero);
        keybd_event(VK_C, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }
}
