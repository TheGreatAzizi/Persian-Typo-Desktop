using System.Runtime.InteropServices;
using System.Text;

namespace PersianTypoFixer.Core.Platform;

public sealed class EvdevKeyboardHook : IKeyboardHook
{
    private const int EvKey = 1;
    private const int KeyReleased = 0;
    private const int KeyPressed = 1;
    private const ushort ButtonLeft = 0x110;
    private const ushort ButtonTask = 0x117;
    private const uint Eviocgname = 0x81004506;

    [DllImport("libc", SetLastError = true)]
    private static extern int ioctl(int fd, uint request, byte[] data);

    private readonly Action<KeyEvent> _onPress;
    private readonly Action<KeyEvent> _onRelease;
    private readonly Func<bool> _isInjecting;
    private readonly Action _onMouse;
    private readonly List<DeviceReader> _devices = new();
    private LinuxKeyMapper? _mapper;
    private Thread[] _threads = Array.Empty<Thread>();
    private volatile bool _stop;

    public EvdevKeyboardHook(Action<KeyEvent> onPress, Action<KeyEvent> onRelease,
        Func<bool> isInjecting, Action onMouse)
    {
        _onPress = onPress;
        _onRelease = onRelease;
        _isInjecting = isInjecting;
        _onMouse = onMouse;
    }

    public void Start()
    {
        if (_threads.Length > 0)
            return;

        _stop = false;
        _mapper = new LinuxKeyMapper();

        var permissionDenied = false;
        var keyboards = new List<string>();
        var mice = new List<string>();

        try
        {
            AddPaths(Path.Combine("/dev/input", "by-path"), "*-event-kbd", keyboards);
            AddPaths(Path.Combine("/dev/input", "by-path"), "*-event-mouse", mice);

            if (keyboards.Count == 0)
            {
                var fallback = new List<string>();
                AddPaths("/dev/input", "event*", fallback);
                foreach (var path in fallback)
                {
                    if (!mice.Contains(path))
                        keyboards.Add(path);
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            permissionDenied = true;
        }

        var keyboardCount = 0;
        foreach (var path in keyboards)
        {
            if (TryOpen(path, false, ref permissionDenied))
                keyboardCount++;
        }
        foreach (var path in mice)
        {
            TryOpen(path, true, ref permissionDenied);
        }

        if (keyboardCount == 0)
        {
            DisposeDevices();
            _mapper.Dispose();
            _mapper = null;
            if (permissionDenied)
            {
                throw new UnauthorizedAccessException(
                    "Cannot read /dev/input. Add the user to the input group and log in again.");
            }
            throw new InvalidOperationException("No keyboard device was found in /dev/input.");
        }

        var threads = new List<Thread>();
        foreach (var device in _devices)
        {
            var reader = new Thread(() => ReadLoop(device))
            {
                IsBackground = true,
                Name = device.IsMouse ? "ptf-evdev-mouse" : "ptf-evdev-keyboard",
            };
            reader.Start();
            threads.Add(reader);
        }
        _threads = threads.ToArray();
    }

    public void Stop()
    {
        _stop = true;
        DisposeDevices();
        foreach (var thread in _threads)
            thread.Join(1000);
        _threads = Array.Empty<Thread>();
        _mapper?.Dispose();
        _mapper = null;
    }

    private static void AddPaths(string directory, string pattern, List<string> paths)
    {
        if (!Directory.Exists(directory))
            return;
        foreach (var path in Directory.EnumerateFiles(directory, pattern))
        {
            if (!paths.Contains(path, StringComparer.Ordinal))
                paths.Add(path);
        }
    }

    private bool TryOpen(string path, bool mouse, ref bool permissionDenied)
    {
        FileStream? stream = null;
        try
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 24,
                FileOptions.None);
            var name = ReadDeviceName(stream);
            if (name.Contains("ydotool", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("virtual", StringComparison.OrdinalIgnoreCase))
            {
                stream.Dispose();
                return false;
            }

            _devices.Add(new DeviceReader(path, stream, mouse));
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            permissionDenied = true;
        }
        catch (IOException)
        {
        }
        catch (Exception)
        {
        }

        stream?.Dispose();
        return false;
    }

    private static string ReadDeviceName(FileStream stream)
    {
        try
        {
            var buffer = new byte[256];
            var fd = (int)stream.SafeFileHandle.DangerousGetHandle();
            if (ioctl(fd, Eviocgname, buffer) < 0)
                return string.Empty;
            var end = Array.IndexOf(buffer, (byte)0);
            if (end < 0)
                end = buffer.Length;
            return Encoding.UTF8.GetString(buffer, 0, end);
        }
        catch
        {
            return string.Empty;
        }
    }

    private void ReadLoop(DeviceReader device)
    {
        var stream = device.Stream;
        var buffer = new byte[24];

        try
        {
            while (!_stop)
            {
                var read = 0;
                while (read < buffer.Length && !_stop)
                {
                    var count = stream.Read(buffer, read, buffer.Length - read);
                    if (count <= 0)
                        return;
                    read += count;
                }
                if (read < buffer.Length)
                    return;

                var type = BitConverter.ToUInt16(buffer, 16);
                if (type != EvKey)
                    continue;

                var code = BitConverter.ToUInt16(buffer, 18);
                var value = BitConverter.ToInt32(buffer, 20);

                if (code is >= ButtonLeft and <= ButtonTask)
                {
                    if (value == KeyPressed && !_isInjecting())
                        _onMouse();
                    continue;
                }

                if (value is not (KeyReleased or KeyPressed))
                    continue;
                if (_isInjecting())
                    continue;

                var mapped = _mapper?.Map(code, value == KeyPressed);
                if (mapped == null)
                    continue;
                if (value == KeyPressed)
                    _onPress(mapped);
                else
                    _onRelease(mapped);
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (IOException)
        {
        }
        catch (Exception)
        {
        }
    }

    private void DisposeDevices()
    {
        foreach (var device in _devices)
        {
            try
            {
                device.Stream.Dispose();
            }
            catch
            {
            }
        }
        _devices.Clear();
    }

    private sealed class DeviceReader
    {
        public DeviceReader(string path, FileStream stream, bool isMouse)
        {
            Path = path;
            Stream = stream;
            IsMouse = isMouse;
        }

        public string Path { get; }
        public FileStream Stream { get; }
        public bool IsMouse { get; }
    }
}
