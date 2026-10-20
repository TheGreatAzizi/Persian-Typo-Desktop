using System.Runtime.InteropServices;
using System.Text;

namespace PersianTypoFixer.Core.Platform;

public sealed class WindowsKeyboardHook : IKeyboardHook
{
    private const int WH_KEYBOARD_LL = 13;
    private const int HC_ACTION = 0;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const uint WM_QUIT = 0x0012;
    private const uint PM_NOREMOVE = 0x0000;

    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;
    private const int VK_CAPITAL = 0x14;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int VK_PACKET = 0xE7;
    private const int VK_PROCESSKEY = 0xE5;

    private static readonly Dictionary<int, string> Special = new()
    {
        [0x08] = "backspace",
        [0x09] = "tab",
        [0x0D] = "enter",
        [0x1B] = "esc",
        [0x25] = "left",
        [0x26] = "up",
        [0x27] = "right",
        [0x28] = "down",
        [0x24] = "home",
        [0x23] = "end",
        [0x21] = "page_up",
        [0x22] = "page_down",
        [0x2E] = "delete",
        [0x10] = "shift",
        [0xA0] = "shift",
        [0xA1] = "shift",
        [0x11] = "ctrl",
        [0xA2] = "ctrl",
        [0xA3] = "ctrl",
        [0x12] = "alt",
        [0xA4] = "alt",
        [0xA5] = "alt",
        [0x5B] = "cmd",
        [0x5C] = "cmd",
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookExW(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool PeekMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostThreadMessageW(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint idThread);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ToUnicodeEx(uint wVirtKey, uint wScanCode, byte[] lpKeyState,
        [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff, int cchBuff, uint wFlags, IntPtr dwhkl);

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

    private readonly Action<KeyEvent> _onPress;
    private readonly Action<KeyEvent> _onRelease;
    private readonly Func<bool> _shouldIgnore;
    private readonly HookProc _proc;
    private Thread? _thread;
    private uint _threadId;
    private IntPtr _hook;
    private readonly ManualResetEventSlim _ready = new(false);

    public WindowsKeyboardHook(Action<KeyEvent> onPress, Action<KeyEvent> onRelease, Func<bool>? shouldIgnore = null)
    {
        _onPress = onPress;
        _onRelease = onRelease;
        _shouldIgnore = shouldIgnore ?? (() => false);
        _proc = Callback;
    }

    public void Start()
    {
        if (_thread != null)
            return;
        _ready.Reset();
        _thread = new Thread(Run) { IsBackground = true, Name = "ptf-keyboard-hook" };
        _thread.Start();
        _ready.Wait(2000);
    }

    public void Stop()
    {
        if (_thread == null)
            return;
        if (_threadId != 0)
            PostThreadMessageW(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(1000);
        _thread = null;
        _threadId = 0;
    }

    private void Run()
    {
        _threadId = GetCurrentThreadId();
        PeekMessageW(out _, IntPtr.Zero, 0, 0, PM_NOREMOVE);
        _hook = SetWindowsHookExW(WH_KEYBOARD_LL, _proc, IntPtr.Zero, 0);
        _ready.Set();
        if (_hook == IntPtr.Zero)
            return;
        try
        {
            while (GetMessageW(out _, IntPtr.Zero, 0, 0) > 0)
            {
            }
        }
        finally
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == HC_ACTION)
        {
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            if (!_shouldIgnore() && (data.flags & 0x00000002) == 0)
            {
                try
                {
                    Dispatch((int)wParam, data);
                }
                catch
                {
                    // never let an exception escape a global hook
                }
            }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private void Dispatch(int msg, KBDLLHOOKSTRUCT data)
    {
        var isPress = msg is WM_KEYDOWN or WM_SYSKEYDOWN;
        var vk = (int)data.vkCode;
        if (vk == VK_PROCESSKEY)
            return;
        KeyEvent ev;
        if (Special.TryGetValue(vk, out var name))
        {
            ev = KeyEvent.FromName(name);
        }
        else if (vk == VK_PACKET)
        {
            if (!isPress)
                return;
            ev = KeyEvent.FromChar((char)data.scanCode);
        }
        else
        {
            if (!isPress)
                return;
            if (IsDown(VK_CONTROL) || IsDown(VK_MENU) || IsDown(VK_LWIN) || IsDown(VK_RWIN))
                ev = new KeyEvent();
            else
                ev = Translate(vk, (int)data.scanCode) is char translated ? KeyEvent.FromChar(translated) : new KeyEvent();
        }
        if (isPress)
            _onPress(ev);
        else
            _onRelease(ev);
    }

    private static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    private static char? Translate(int vk, int scan)
    {
        var state = new byte[256];
        if (IsDown(VK_SHIFT))
            state[VK_SHIFT] = 0x80;
        if ((GetAsyncKeyState(VK_CAPITAL) & 0x0001) != 0)
            state[VK_CAPITAL] = 0x01;
        var hwnd = GetForegroundWindow();
        var threadId = hwnd != IntPtr.Zero ? GetWindowThreadProcessId(hwnd, IntPtr.Zero) : 0;
        var layout = GetKeyboardLayout(threadId);
        var buffer = new StringBuilder(8);
        var count = ToUnicodeEx((uint)vk, (uint)scan, state, buffer, buffer.Capacity, 0, layout);
        if (count > 0 && buffer.Length > 0)
            return buffer[0];
        return null;
    }
}
