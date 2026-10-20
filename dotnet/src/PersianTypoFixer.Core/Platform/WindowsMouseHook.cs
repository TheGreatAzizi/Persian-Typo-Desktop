using System.Runtime.InteropServices;

namespace PersianTypoFixer.Core.Platform;

public sealed class WindowsMouseHook : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int HC_ACTION = 0;
    private const uint WM_QUIT = 0x0012;
    private const uint PM_NOREMOVE = 0x0000;

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public int ptX;
        public int ptY;
        public uint mouseData;
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

    private readonly Action _onClick;
    private readonly HookProc _proc;
    private Thread? _thread;
    private uint _threadId;
    private IntPtr _hook;
    private readonly ManualResetEventSlim _ready = new(false);

    public WindowsMouseHook(Action onClick)
    {
        _onClick = onClick;
        _proc = Callback;
    }

    public void Start()
    {
        if (_thread != null)
            return;
        _ready.Reset();
        _thread = new Thread(Run) { IsBackground = true, Name = "ptf-mouse-hook" };
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

    private void Run()
    {
        _threadId = GetCurrentThreadId();
        PeekMessageW(out _, IntPtr.Zero, 0, 0, PM_NOREMOVE);
        _hook = SetWindowsHookExW(WH_MOUSE_LL, _proc, IntPtr.Zero, 0);
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
            var msg = (int)wParam;
            if (msg is 0x0201 or 0x0204 or 0x0207 or 0x020B)
            {
                try
                {
                    _onClick();
                }
                catch
                {
                }
            }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }
}
