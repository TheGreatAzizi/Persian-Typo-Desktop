using System.Runtime.InteropServices;

namespace PersianTypoFixer.Core.Platform;

public sealed class X11RecordKeyboardHook : IKeyboardHook
{
    private const string LibX11 = "libX11.so.6";
    private const string LibXtst = "libXtst.so.6";
    private const string LibXkbCommon = "libxkbcommon.so.0";

    private const int KeyPress = 2;
    private const int KeyRelease = 3;
    private const int ButtonPress = 4;
    private const int MotionNotify = 6;
    private const int XRecordFromServer = 0;
    private const ulong AllClients = 3;

    private const int KeyStateOffset = 28;
    private const int KeyCodeOffset = 1;
    private const ulong MinimumDataUnits = 8;

    [DllImport(LibX11)]
    private static extern IntPtr XOpenDisplay(string? displayName);

    [DllImport(LibX11)]
    private static extern int XCloseDisplay(IntPtr display);

    [DllImport(LibX11)]
    private static extern IntPtr XStringToKeysym(string name);

    [DllImport(LibX11)]
    private static extern byte XKeysymToKeycode(IntPtr display, IntPtr keysym);

    [DllImport(LibX11)]
    private static extern ulong XkbKeycodeToKeysym(IntPtr display, byte keycode, int group, int level);

    [DllImport(LibX11)]
    private static extern int XFree(IntPtr data);

    [DllImport(LibXtst)]
    private static extern IntPtr XRecordAllocRange();

    [DllImport(LibXtst)]
    private static extern IntPtr XRecordCreateContext(IntPtr display, int datumFlags, ref ulong clients,
        int clientCount, ref IntPtr ranges, int rangeCount);

    [DllImport(LibXtst)]
    private static extern int XRecordEnableContext(IntPtr display, IntPtr context,
        XRecordInterceptProc callback, IntPtr closure);

    [DllImport(LibXtst)]
    private static extern int XRecordDisableContext(IntPtr display, IntPtr context);

    [DllImport(LibXtst)]
    private static extern void XRecordFreeContext(IntPtr display, IntPtr context);

    [DllImport(LibXtst)]
    private static extern void XRecordFreeData(IntPtr data);

    [DllImport(LibXkbCommon)]
    private static extern uint xkb_keysym_to_utf32(uint keysym);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void XRecordInterceptProc(IntPtr closure, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct XRecordRange8
    {
        public byte First;
        public byte Last;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XRecordRange16
    {
        public ushort First;
        public ushort Last;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XRecordExtRange
    {
        public XRecordRange8 Major;
        public XRecordRange16 Minor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XRecordRange
    {
        public XRecordRange8 CoreRequests;
        public XRecordRange8 CoreReplies;
        public XRecordExtRange ExtRequests;
        public XRecordExtRange ExtReplies;
        public XRecordRange8 DeliveredEvents;
        public XRecordRange8 DeviceEvents;
        public XRecordRange8 Errors;
        public int ClientStarted;
        public int ClientDied;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct XRecordInterceptData
    {
        [FieldOffset(0)] public IntPtr IdBase;
        [FieldOffset(8)] public ulong ServerTime;
        [FieldOffset(16)] public ulong ClientSequence;
        [FieldOffset(24)] public int Category;
        [FieldOffset(28)] public int ClientSwapped;
        [FieldOffset(32)] public IntPtr Data;
        [FieldOffset(40)] public ulong DataLength;
    }

    private readonly Action<KeyEvent> _onPress;
    private readonly Action<KeyEvent> _onRelease;
    private readonly Func<bool> _isInjecting;
    private readonly Action _onMouse;
    private readonly XRecordInterceptProc _callback;
    private readonly ManualResetEventSlim _ready = new(false);
    private readonly Dictionary<uint, string> _names = new();
    private readonly char?[,,] _chars = new char?[256, 4, 2];

    private Thread? _thread;
    private Exception? _error;
    private IntPtr _display;
    private IntPtr _context;
    private int _disabled;
    private bool _xkbCommon = true;

    public X11RecordKeyboardHook(Action<KeyEvent> onPress, Action<KeyEvent> onRelease,
        Func<bool> isInjecting, Action onMouse)
    {
        _onPress = onPress;
        _onRelease = onRelease;
        _isInjecting = isInjecting;
        _onMouse = onMouse;
        _callback = OnRecord;
    }

    public void Start()
    {
        if (_thread != null)
            return;

        _ready.Reset();
        _error = null;
        _disabled = 0;
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "ptf-xrecord",
        };
        _thread.Start();

        if (!_ready.Wait(3000))
        {
            Stop();
            throw new TimeoutException("X11 keyboard hook did not start.");
        }
        if (_error != null)
        {
            var error = _error;
            Stop();
            throw new InvalidOperationException("X11 keyboard hook failed: " + error.Message, error);
        }
    }

    public void Stop()
    {
        var thread = _thread;
        var display = _display;
        var context = _context;
        _thread = null;
        if (thread != null)
            DisableContext(display, context);
        thread?.Join(1500);
    }

    private void DisableContext(IntPtr display, IntPtr context)
    {
        if (display == IntPtr.Zero || context == IntPtr.Zero)
            return;
        if (Interlocked.Exchange(ref _disabled, 1) == 0)
            XRecordDisableContext(display, context);
    }

    private void Run()
    {
        try
        {
            _display = XOpenDisplay(null);
            if (_display == IntPtr.Zero)
                throw new InvalidOperationException("X11 display is not available.");

            var range = new XRecordRange
            {
                DeviceEvents = new XRecordRange8 { First = KeyPress, Last = MotionNotify },
            };
            var rangePtr = XRecordAllocRange();
            if (rangePtr == IntPtr.Zero)
                throw new InvalidOperationException("XRecord is not available.");
            Marshal.StructureToPtr(range, rangePtr, false);

            var clients = AllClients;
            _context = XRecordCreateContext(_display, 0, ref clients, 1, ref rangePtr, 1);
            XFree(rangePtr);
            if (_context == IntPtr.Zero)
                throw new InvalidOperationException("XRecord context could not be created.");

            BuildKeyNames();
            BuildCharMap();
            _ready.Set();
            XRecordEnableContext(_display, _context, _callback, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            _error = ex;
            _ready.Set();
        }
        finally
        {
            if (_display != IntPtr.Zero)
            {
                if (_context != IntPtr.Zero)
                {
                    DisableContext(_display, _context);
                    XRecordFreeContext(_display, _context);
                    _context = IntPtr.Zero;
                }
                XCloseDisplay(_display);
                _display = IntPtr.Zero;
            }
            _ready.Set();
        }
    }

    private void BuildKeyNames()
    {
        AddName("Shift_L", "shift");
        AddName("Shift_R", "shift");
        AddName("Control_L", "ctrl");
        AddName("Control_R", "ctrl");
        AddName("Alt_L", "alt");
        AddName("Alt_R", "alt");
        AddName("Super_L", "cmd");
        AddName("Super_R", "cmd");
        AddName("BackSpace", "backspace");
        AddName("Tab", "tab");
        AddName("Return", "enter");
        AddName("KP_Enter", "enter");
        AddName("Escape", "esc");
        AddName("Delete", "delete");
        AddName("Left", "left");
        AddName("Right", "right");
        AddName("Up", "up");
        AddName("Down", "down");
        AddName("Home", "home");
        AddName("End", "end");
        AddName("Prior", "page_up");
        AddName("Next", "page_down");
    }

    private void AddName(string keysymName, string name)
    {
        var keysym = XStringToKeysym(keysymName);
        if (keysym == IntPtr.Zero)
            return;
        var keycode = XKeysymToKeycode(_display, keysym);
        if (keycode != 0)
            _names[keycode] = name;
    }

    private void BuildCharMap()
    {
        for (var keycode = 8; keycode < 256; keycode++)
        {
            for (var group = 0; group < 4; group++)
            {
                for (var level = 0; level < 2; level++)
                {
                    var keysym = XkbKeycodeToKeysym(_display, (byte)keycode, group, level);
                    if (keysym == 0 && group != 0)
                        keysym = XkbKeycodeToKeysym(_display, (byte)keycode, 0, level);
                    if (keysym != 0)
                        _chars[keycode, group, level] = KeysymToChar(keysym);
                }
            }
        }
    }

    private char? KeysymToChar(ulong keysym)
    {
        if (_xkbCommon)
        {
            try
            {
                var codepoint = xkb_keysym_to_utf32((uint)keysym);
                if (codepoint is > 0 and <= 0xFFFF)
                    return (char)codepoint;
            }
            catch (DllNotFoundException)
            {
                _xkbCommon = false;
            }
            catch (EntryPointNotFoundException)
            {
                _xkbCommon = false;
            }
        }

        if ((keysym & 0xFF000000UL) == 0x01000000UL)
            return (char)(keysym & 0xFFFF);
        if (keysym is >= 0x20 and <= 0x7E)
            return (char)keysym;
        return null;
    }

    private void OnRecord(IntPtr closure, IntPtr data)
    {
        if (data == IntPtr.Zero)
            return;

        try
        {
            var record = Marshal.PtrToStructure<XRecordInterceptData>(data);
            if (record.Category != XRecordFromServer || record.Data == IntPtr.Zero)
                return;
            if (record.DataLength < MinimumDataUnits)
                return;

            var type = Marshal.ReadByte(record.Data, 0);
            if (type == ButtonPress)
            {
                if (!_isInjecting())
                    _onMouse();
                return;
            }
            if (type != KeyPress && type != KeyRelease)
                return;
            if (_isInjecting())
                return;

            var state = (uint)(ushort)Marshal.ReadInt16(record.Data, KeyStateOffset);
            var keycode = (uint)Marshal.ReadByte(record.Data, KeyCodeOffset);

            if (_names.TryGetValue(keycode, out var name))
            {
                var named = KeyEvent.FromName(name);
                if (type == KeyPress)
                    _onPress(named);
                else
                    _onRelease(named);
                return;
            }

            if (type == KeyRelease)
                return;

            var ch = Translate(keycode, state);
            if (ch != null)
                _onPress(KeyEvent.FromChar(ch.Value));
        }
        catch
        {
            // Exceptions must never escape a native hook callback.
        }
        finally
        {
            if (data != IntPtr.Zero)
                XRecordFreeData(data);
        }
    }

    private char? Translate(uint keycode, uint state)
    {
        if (keycode >= 256)
            return null;
        var group = (int)((state >> 13) & 0x3);
        var level = (state & 0x2) != 0 || (state & 0x1) != 0 ? 1 : 0;
        return _chars[keycode, group, level];
    }
}
