using System.Runtime.InteropServices;
using System.Text;

namespace PersianTypoFixer.Core.Platform;

internal sealed class LinuxKeyMapper : IDisposable
{
    private const string LibXkbCommon = "libxkbcommon.so.0";

    [DllImport(LibXkbCommon)]
    private static extern IntPtr xkb_context_new(uint flags);

    [DllImport(LibXkbCommon)]
    private static extern void xkb_context_unref(IntPtr context);

    [DllImport(LibXkbCommon)]
    private static extern IntPtr xkb_keymap_new_from_names(IntPtr context, IntPtr names, uint flags);

    [DllImport(LibXkbCommon)]
    private static extern void xkb_keymap_unref(IntPtr keymap);

    [DllImport(LibXkbCommon)]
    private static extern IntPtr xkb_state_new(IntPtr keymap);

    [DllImport(LibXkbCommon)]
    private static extern void xkb_state_unref(IntPtr state);

    [DllImport(LibXkbCommon)]
    private static extern uint xkb_state_key_get_utf32(IntPtr state, uint keycode);

    [DllImport(LibXkbCommon)]
    private static extern int xkb_state_update_key(IntPtr state, uint keycode, int direction);

    [StructLayout(LayoutKind.Sequential)]
    private struct XkbRuleNames
    {
        public IntPtr Rules;
        public IntPtr Model;
        public IntPtr Layout;
        public IntPtr Variant;
        public IntPtr Options;
    }

    private static readonly Dictionary<ushort, string> SpecialNames = new()
    {
        [1] = "esc",
        [14] = "backspace",
        [15] = "tab",
        [28] = "enter",
        [96] = "enter",
        [29] = "ctrl",
        [97] = "ctrl",
        [42] = "shift",
        [54] = "shift",
        [56] = "alt",
        [100] = "alt",
        [125] = "cmd",
        [126] = "cmd",
        [103] = "up",
        [108] = "down",
        [105] = "left",
        [106] = "right",
        [102] = "home",
        [107] = "end",
        [104] = "page_up",
        [109] = "page_down",
        [111] = "delete",
        [58] = "capslock",
        [69] = "numlock",
        [70] = "scrolllock",
    };

    private readonly object _gate = new();
    private readonly IntPtr _context;
    private readonly IntPtr _keymap;
    private readonly IntPtr _state;
    private bool _disposed;

    public LinuxKeyMapper()
    {
        _context = xkb_context_new(0);
        if (_context == IntPtr.Zero)
            throw new InvalidOperationException("xkbcommon context could not be created.");

        var layout = ReadLayout();
        var variant = ReadVariant();
        var options = ReadOptions();
        var namesPtr = IntPtr.Zero;
        var strings = new List<IntPtr>();

        try
        {
            if (!string.IsNullOrWhiteSpace(layout))
            {
                var names = new XkbRuleNames
                {
                    Layout = AllocString(layout, strings),
                    Variant = AllocString(variant, strings),
                    Options = AllocString(options, strings),
                };
                namesPtr = Marshal.AllocHGlobal(Marshal.SizeOf<XkbRuleNames>());
                Marshal.StructureToPtr(names, namesPtr, false);
            }

            _keymap = xkb_keymap_new_from_names(_context, namesPtr, 0);
            if (_keymap == IntPtr.Zero)
                _keymap = xkb_keymap_new_from_names(_context, IntPtr.Zero, 0);
        }
        finally
        {
            foreach (var ptr in strings)
                Marshal.FreeCoTaskMem(ptr);
            if (namesPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(namesPtr);
        }

        if (_keymap == IntPtr.Zero)
        {
            xkb_context_unref(_context);
            throw new InvalidOperationException(
                "xkbcommon keymap could not be loaded. Install xkeyboard-config or set XKB_DEFAULT_LAYOUT.");
        }

        _state = xkb_state_new(_keymap);
        if (_state == IntPtr.Zero)
        {
            xkb_keymap_unref(_keymap);
            xkb_context_unref(_context);
            throw new InvalidOperationException("xkbcommon state could not be created.");
        }
    }

    public KeyEvent? Map(ushort code, bool press)
    {
        lock (_gate)
        {
            if (_disposed || code > 247)
                return null;

            var keycode = (uint)code + 8;
            if (SpecialNames.TryGetValue(code, out var name))
            {
                xkb_state_update_key(_state, keycode, press ? 1 : 0);
                return KeyEvent.FromName(name);
            }

            if (!press)
            {
                xkb_state_update_key(_state, keycode, 0);
                return null;
            }

            var codepoint = xkb_state_key_get_utf32(_state, keycode);
            xkb_state_update_key(_state, keycode, 1);
            if (codepoint is > 0 and <= 0xFFFF)
                return KeyEvent.FromChar((char)codepoint);
            return null;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_state != IntPtr.Zero)
                xkb_state_unref(_state);
            if (_keymap != IntPtr.Zero)
                xkb_keymap_unref(_keymap);
            xkb_context_unref(_context);
        }
    }

    private static IntPtr AllocString(string? value, List<IntPtr> strings)
    {
        if (string.IsNullOrWhiteSpace(value))
            return IntPtr.Zero;
        var ptr = Marshal.StringToCoTaskMemUTF8(value);
        strings.Add(ptr);
        return ptr;
    }

    private static string? ReadLayout()
    {
        var env = Environment.GetEnvironmentVariable("XKB_DEFAULT_LAYOUT");
        if (!string.IsNullOrWhiteSpace(env))
            return env;

        var kde = ReadKdeConfig("LayoutList");
        if (!string.IsNullOrWhiteSpace(kde))
            return kde;

        var defaultKeyboard = "/etc/default/keyboard";
        if (File.Exists(defaultKeyboard))
        {
            var value = ReadAssignment(defaultKeyboard, "XKBLAYOUT");
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static string? ReadVariant()
    {
        var env = Environment.GetEnvironmentVariable("XKB_DEFAULT_VARIANT");
        if (!string.IsNullOrWhiteSpace(env))
            return env;

        var kde = ReadKdeConfig("VariantList");
        if (!string.IsNullOrWhiteSpace(kde) && kde.Trim(',').Length > 0)
            return kde;

        var defaultKeyboard = "/etc/default/keyboard";
        if (File.Exists(defaultKeyboard))
        {
            var value = ReadAssignment(defaultKeyboard, "XKBVARIANT");
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static string? ReadOptions()
    {
        var env = Environment.GetEnvironmentVariable("XKB_DEFAULT_OPTIONS");
        if (!string.IsNullOrWhiteSpace(env))
            return env;

        var kde = ReadKdeConfig("Options");
        if (!string.IsNullOrWhiteSpace(kde))
            return kde;

        var defaultKeyboard = "/etc/default/keyboard";
        if (File.Exists(defaultKeyboard))
        {
            var value = ReadAssignment(defaultKeyboard, "XKBOPTIONS");
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }
        return null;
    }

    private static string? ReadAssignment(string path, string key)
    {
        try
        {
            foreach (var line in File.ReadLines(path))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith(key + "=", StringComparison.Ordinal))
                    continue;
                return trimmed[(key.Length + 1)..].Trim().Trim('"');
            }
        }
        catch
        {
        }
        return null;
    }

    private static string? ReadKdeConfig(string key)
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config", "kxkbrc");
            if (!File.Exists(path))
                return null;

            var inLayout = false;
            foreach (var line in File.ReadLines(path))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
                {
                    inLayout = trimmed.Equals("[Layout]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }
                if (!inLayout)
                    continue;

                var separator = trimmed.IndexOf('=');
                if (separator <= 0)
                    continue;
                if (!trimmed[..separator].Trim().Equals(key, StringComparison.Ordinal))
                    continue;

                var value = trimmed[(separator + 1)..].Trim().Trim('"');
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }
        catch
        {
        }
        return null;
    }
}
