using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace PersianTypoFixer.Core.Platform;

public sealed class LinuxInput : IInputBackend
{
    private const string LibX11 = "libX11.so.6";
    private const string LibXtst = "libXtst.so.6";

    [DllImport(LibX11)]
    private static extern IntPtr XOpenDisplay(string? displayName);

    [DllImport(LibX11)]
    private static extern int XCloseDisplay(IntPtr display);

    [DllImport(LibX11)]
    private static extern IntPtr XStringToKeysym(string name);

    [DllImport(LibX11)]
    private static extern byte XKeysymToKeycode(IntPtr display, IntPtr keysym);

    [DllImport(LibX11)]
    private static extern int XSync(IntPtr display, int discard);

    [DllImport(LibXtst)]
    private static extern int XTestFakeKeyEvent(IntPtr display, uint keycode, int isPress, ulong delay);

    private readonly string? _clipboardGet;
    private readonly string? _clipboardSet;
    private readonly string? _selectionGet;
    private readonly string? _injector;
    private volatile bool _xtest = true;

    public LinuxInput()
    {
        var wayland = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
        IsWayland = wayland;
        if (wayland && Which("wl-paste") != null && Which("wl-copy") != null)
        {
            _clipboardGet = "wl-paste --no-newline";
            _clipboardSet = "wl-copy";
            _selectionGet = "wl-paste --primary --no-newline";
        }
        else if (Which("xclip") != null)
        {
            _clipboardGet = "xclip -selection clipboard -o";
            _clipboardSet = "xclip -selection clipboard -i";
            _selectionGet = "xclip -selection primary -o";
        }
        else if (Which("xsel") != null)
        {
            _clipboardGet = "xsel --clipboard --output";
            _clipboardSet = "xsel --clipboard --input";
            _selectionGet = "xsel --primary --output";
        }
        else
        {
            _clipboardGet = null;
            _clipboardSet = null;
            _selectionGet = null;
        }
        _injector = Which("xdotool") != null ? "xdotool" : Which("ydotool") != null ? "ydotool" : null;
    }

    public bool IsWayland { get; }

    public bool CanAutoPaste => !IsWayland;

    public bool IsAvailable => _clipboardGet != null && _clipboardSet != null;

    public string? GetSelection()
    {
        if (_selectionGet == null)
            return null;
        var (code, output) = RunGet(_selectionGet);
        return code == 0 ? output : null;
    }

    public string GetClipboard()
    {
        if (_clipboardGet == null)
            return "";
        var (code, output) = RunGet(_clipboardGet);
        return code == 0 ? output : "";
    }

    public void SetClipboard(string text)
    {
        if (_clipboardSet == null)
            return;
        RunSet(_clipboardSet, text ?? "");
    }

    public void PressBackspace(int count)
    {
        if (!TrySend(dpy =>
            {
                for (var i = 0; i < count; i++)
                    Tap(dpy, "BackSpace");
            }))
        {
            InjectFallback("BackSpace", count);
        }
    }

    public void Paste()
    {
        if (!TrySend(dpy => Chord(dpy, "Control_L", "v")))
            InjectFallback("ctrl+v", 1);
    }

    public void Copy()
    {
        if (!TrySend(dpy => Chord(dpy, "Control_L", "c")))
            InjectFallback("ctrl+c", 1);
    }

    private bool TrySend(Action<IntPtr> action)
    {
        if (!_xtest)
            return false;
        var display = IntPtr.Zero;
        try
        {
            display = XOpenDisplay(null);
            if (display == IntPtr.Zero)
            {
                _xtest = false;
                return false;
            }
            action(display);
            XSync(display, 0);
            return true;
        }
        catch (DllNotFoundException)
        {
            _xtest = false;
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            _xtest = false;
            return false;
        }
        finally
        {
            if (display != IntPtr.Zero)
                XCloseDisplay(display);
        }
    }

    private static void Tap(IntPtr display, string keysym, int repeat = 1)
    {
        var keycode = Keycode(display, keysym);
        if (keycode == 0)
            return;
        for (var i = 0; i < repeat; i++)
        {
            XTestFakeKeyEvent(display, keycode, 1, 0);
            XTestFakeKeyEvent(display, keycode, 0, 0);
        }
    }

    private static void Chord(IntPtr display, string modifier, string key)
    {
        var modifierCode = Keycode(display, modifier);
        var keyCode = Keycode(display, key);
        if (modifierCode == 0 || keyCode == 0)
            return;
        XTestFakeKeyEvent(display, modifierCode, 1, 0);
        XTestFakeKeyEvent(display, keyCode, 1, 0);
        XTestFakeKeyEvent(display, keyCode, 0, 0);
        XTestFakeKeyEvent(display, modifierCode, 0, 0);
    }

    private static uint Keycode(IntPtr display, string keysym)
    {
        var symbol = XStringToKeysym(keysym);
        return symbol == IntPtr.Zero ? 0u : XKeysymToKeycode(display, symbol);
    }

    private void InjectFallback(string keysym, int count)
    {
        if (_injector == null)
            return;
        if (_injector == "xdotool")
        {
            var key = keysym switch
            {
                "BackSpace" => "BackSpace",
                "ctrl+v" => "ctrl+v",
                "ctrl+c" => "ctrl+c",
                _ => keysym,
            };
            RunInject($"xdotool key --repeat {count} {key}");
        }
    }

    private static string? Which(string name)
    {
        try
        {
            var (code, output) = RunGet("command -v " + name);
            return code == 0 ? output.Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    private static (int Code, string Output) RunGet(string command)
    {
        var info = ShellInfo(command);
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        using var process = Process.Start(info);
        if (process == null)
            return (1, "");
        var output = process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit(3000);
        return (process.HasExited ? process.ExitCode : 0, output);
    }

    private static void RunSet(string command, string text)
    {
        var info = ShellInfo(command);
        info.RedirectStandardInput = true;
        using var process = Process.Start(info);
        if (process == null)
            return;
        process.StandardInput.Write(text);
        process.StandardInput.Close();
        Thread.Sleep(30);
    }

    private static void RunInject(string command)
    {
        var info = ShellInfo(command);
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        using var process = Process.Start(info);
        if (process == null)
            return;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit(2000);
    }

    private static ProcessStartInfo ShellInfo(string command)
    {
        return new ProcessStartInfo
        {
            FileName = "/bin/sh",
            Arguments = "-c \"" + command.Replace("\"", "\\\"") + "\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
    }
}
