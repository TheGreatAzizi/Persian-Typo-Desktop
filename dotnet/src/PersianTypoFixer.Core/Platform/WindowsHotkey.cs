using System.Runtime.InteropServices;

namespace PersianTypoFixer.Core.Platform;

public sealed class WindowsHotkey : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint WM_QUIT = 0x0012;
    private const uint PM_NOREMOVE = 0x0000;
    private const int HotkeyId = 0x5054;

    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    private const int HWND_MESSAGE = -3;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSW
    {
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassW(ref WNDCLASSW lpWndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(uint dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int w, int h, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool PeekMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostThreadMessageW(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? lpModuleName);

    private readonly Action _onHotkey;
    private readonly WndProcDelegate _wndProc;
    private readonly string _className = "PTFHotkeyWindow_" + Guid.NewGuid().ToString("N");
    private Thread? _thread;
    private uint _threadId;
    private IntPtr _hwnd;
    private readonly ManualResetEventSlim _ready = new(false);
    private bool _registered;

    public WindowsHotkey(Action onHotkey, string hotkey)
    {
        _onHotkey = onHotkey;
        _wndProc = WindowProc;
        Hotkey = hotkey;
    }

    public string Hotkey { get; }

    public bool IsRegistered => _registered;

    public void Start()
    {
        if (_thread != null)
            return;
        if (!TryParse(Hotkey, out var modifiers, out var vk))
            throw new ArgumentException("invalid hotkey: " + Hotkey);
        _ready.Reset();
        _thread = new Thread(() => Run(modifiers, vk)) { IsBackground = true, Name = "ptf-hotkey" };
        _thread.Start();
        _ready.Wait(2000);
    }

    public void Dispose()
    {
        if (_thread == null)
            return;
        if (_threadId != 0)
            PostThreadMessageW(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(1000);
        _thread = null;
        _threadId = 0;
    }

    private void Run(uint modifiers, uint vk)
    {
        _threadId = GetCurrentThreadId();
        PeekMessageW(out _, IntPtr.Zero, 0, 0, PM_NOREMOVE);
        var wc = new WNDCLASSW
        {
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = GetModuleHandleW(null),
            lpszClassName = _className,
        };
        var atom = RegisterClassW(ref wc);
        if (atom == 0)
        {
            _ready.Set();
            return;
        }
        _hwnd = CreateWindowExW(0, _className, "Persian Typo Fixer", 0, 0, 0, 0, 0, (IntPtr)HWND_MESSAGE, IntPtr.Zero,
            GetModuleHandleW(null), IntPtr.Zero);
        if (_hwnd == IntPtr.Zero)
        {
            _ready.Set();
            return;
        }
        _registered = RegisterHotKey(_hwnd, HotkeyId, modifiers | MOD_NOREPEAT, vk);
        _ready.Set();
        try
        {
            while (GetMessageW(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                if (msg.message == WM_HOTKEY)
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
        }
        finally
        {
            if (_registered)
                UnregisterHotKey(_hwnd, HotkeyId);
            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
            _registered = false;
        }
    }

    private IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    public static bool TryParse(string hotkey, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;
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
                    modifiers |= MOD_CONTROL;
                    break;
                case "shift":
                    modifiers |= MOD_SHIFT;
                    break;
                case "alt":
                    modifiers |= MOD_ALT;
                    break;
                case "win":
                case "cmd":
                case "super":
                    modifiers |= MOD_WIN;
                    break;
                default:
                    key = part;
                    break;
            }
        }
        if (key == null || modifiers == 0)
            return false;
        if (key.Length == 1)
        {
            var c = key[0];
            if (c >= 'a' && c <= 'z')
                vk = (uint)('A' + (c - 'a'));
            else if (c >= '0' && c <= '9')
                vk = (uint)c;
            else
                return false;
        }
        else if (key.Length is 2 or 3 && key[0] == 'f' && int.TryParse(key[1..], out var number) && number is >= 1 and <= 24)
        {
            vk = (uint)(0x70 + number - 1);
        }
        else
        {
            vk = key switch
            {
                "space" => 0x20,
                "tab" => 0x09,
                "enter" => 0x0D,
                "esc" or "escape" => 0x1B,
                "backspace" => 0x08,
                "insert" => 0x2D,
                "delete" => 0x2E,
                "home" => 0x24,
                "end" => 0x23,
                "pageup" => 0x21,
                "pagedown" => 0x22,
                "left" => 0x25,
                "up" => 0x26,
                "right" => 0x27,
                "down" => 0x28,
                _ => 0,
            };
            if (vk == 0)
                return false;
        }
        return true;
    }
}
