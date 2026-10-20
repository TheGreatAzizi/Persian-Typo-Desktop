using System.Runtime.InteropServices;

namespace PersianTypoFixer.Core.Platform;

public sealed class LinuxHotkey : IDisposable
{
    private const string LibX11 = "libX11.so.6";

    private const int KeyPress = 2;
    private const int GrabModeAsync = 1;
    private const uint ShiftMask = 1 << 0;
    private const uint LockMask = 1 << 1;
    private const uint ControlMask = 1 << 2;
    private const uint Mod1Mask = 1 << 3;
    private const uint Mod4Mask = 1 << 6;
    private const uint Mod2Mask = 1 << 4;

    [DllImport(LibX11)]
    private static extern IntPtr XOpenDisplay(string? displayName);

    [DllImport(LibX11)]
    private static extern int XCloseDisplay(IntPtr display);

    [DllImport(LibX11)]
    private static extern IntPtr XDefaultRootWindow(IntPtr display);

    [DllImport(LibX11)]
    private static extern int XGrabKey(IntPtr display, int keycode, uint modifiers, IntPtr grabWindow,
        int ownerEvents, int pointerMode, int keyboardMode);

    [DllImport(LibX11)]
    private static extern int XUngrabKey(IntPtr display, int keycode, uint modifiers, IntPtr grabWindow);

    [DllImport(LibX11)]
    private static extern int XSync(IntPtr display, int discard);

    [DllImport(LibX11)]
    private static extern int XPending(IntPtr display);

    [DllImport(LibX11)]
    private static extern int XNextEvent(IntPtr display, IntPtr xevent);

    [DllImport(LibX11)]
    private static extern byte XKeysymToKeycode(IntPtr display, IntPtr keysym);

    [DllImport(LibX11)]
    private static extern IntPtr XStringToKeysym(string name);

    private readonly Action _onHotkey;
    private readonly string _keysymName;
    private readonly uint _modifiers;
    private Thread? _thread;
    private IntPtr _display;
    private IntPtr _root;
    private int _keycode;
    private volatile bool _stop;
    private bool _registered;
    private readonly ManualResetEventSlim _ready = new(false);

    public LinuxHotkey(Action onHotkey, string hotkey)
    {
        _onHotkey = onHotkey;
        Hotkey = hotkey;
        if (!TryParse(hotkey, out _modifiers, out var keyName))
            throw new ArgumentException("invalid hotkey: " + hotkey);
        _keysymName = keyName;
    }

    public string Hotkey { get; }

    public bool IsRegistered => _registered;

    public void Start()
    {
        if (_thread != null)
            return;
        _ready.Reset();
        _thread = new Thread(Run) { IsBackground = true, Name = "ptf-hotkey" };
        _thread.Start();
        _ready.Wait(3000);
        if (!_registered)
        {
            _thread.Join(500);
            _thread = null;
        }
    }

    public void Dispose()
    {
        _stop = true;
        if (_thread != null)
        {
            _thread.Join(1000);
            _thread = null;
        }
    }

    private void Run()
    {
        var eventBuffer = IntPtr.Zero;
        try
        {
            _display = XOpenDisplay(null);
            if (_display == IntPtr.Zero)
                return;
            var keysym = XStringToKeysym(_keysymName);
            if (keysym == IntPtr.Zero)
                return;
            _keycode = XKeysymToKeycode(_display, keysym);
            if (_keycode == 0)
                return;
            _root = XDefaultRootWindow(_display);
            foreach (var extra in new uint[] { 0, LockMask, Mod2Mask, LockMask | Mod2Mask })
            {
                XGrabKey(_display, _keycode, _modifiers | extra, _root, 0, GrabModeAsync, GrabModeAsync);
            }
            XSync(_display, 0);
            _registered = true;
            _ready.Set();
            eventBuffer = Marshal.AllocHGlobal(256);
            while (!_stop)
            {
                while (XPending(_display) > 0)
                {
                    XNextEvent(_display, eventBuffer);
                    if (Marshal.ReadInt32(eventBuffer) == KeyPress)
                    {
                        try
                        {
                            _onHotkey();
                        }
                        catch
                        {
                        }
                    }
                }
                Thread.Sleep(15);
            }
        }
        finally
        {
            if (_display != IntPtr.Zero)
            {
                if (_keycode != 0)
                {
                    foreach (var extra in new uint[] { 0, LockMask, Mod2Mask, LockMask | Mod2Mask })
                    {
                        XUngrabKey(_display, _keycode, _modifiers | extra, _root);
                    }
                    XSync(_display, 0);
                }
                XCloseDisplay(_display);
                _display = IntPtr.Zero;
            }
            if (eventBuffer != IntPtr.Zero)
                Marshal.FreeHGlobal(eventBuffer);
            _registered = false;
            _ready.Set();
        }
    }

    public static bool TryParse(string hotkey, out uint modifiers, out string keysymName)
    {
        modifiers = 0;
        keysymName = "";
        if (string.IsNullOrWhiteSpace(hotkey))
            return false;
        var parts = hotkey.ToLowerInvariant().Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string? key = null;
        foreach (var part in parts)
        {
            switch (part)
            {
                case "ctrl":
                case "control":
                    modifiers |= ControlMask;
                    break;
                case "shift":
                    modifiers |= ShiftMask;
                    break;
                case "alt":
                    modifiers |= Mod1Mask;
                    break;
                case "win":
                case "cmd":
                case "super":
                    modifiers |= Mod4Mask;
                    break;
                default:
                    key = part;
                    break;
            }
        }
        if (key == null || modifiers == 0)
            return false;
        if (key.Length == 1 && key[0] is >= 'a' and <= 'z' or >= '0' and <= '9')
        {
            keysymName = key;
            return true;
        }
        keysymName = key switch
        {
            "space" => "space",
            "tab" => "Tab",
            "enter" => "Return",
            "esc" or "escape" => "Escape",
            "backspace" => "BackSpace",
            "insert" => "Insert",
            "delete" => "Delete",
            "home" => "Home",
            "end" => "End",
            "pageup" => "Prior",
            "pagedown" => "Next",
            "left" => "Left",
            "up" => "Up",
            "right" => "Right",
            "down" => "Down",
            _ => "",
        };
        if (keysymName.Length == 0 && key.Length is 2 or 3 && key[0] == 'f'
            && int.TryParse(key[1..], out var number) && number is >= 1 and <= 24)
        {
            keysymName = "F" + number;
        }
        return keysymName.Length > 0;
    }
}
