namespace PersianTypoFixer.Core.Platform;

public sealed class LinuxKeyboardHook : IKeyboardHook
{
    private readonly Action<KeyEvent> _onPress;
    private readonly Action<KeyEvent> _onRelease;
    private readonly Func<bool> _isInjecting;
    private readonly Action _onMouse;
    private IKeyboardHook? _inner;

    public LinuxKeyboardHook(Action<KeyEvent> onPress, Action<KeyEvent> onRelease,
        Func<bool> isInjecting, Action onMouse)
    {
        _onPress = onPress;
        _onRelease = onRelease;
        _isInjecting = isInjecting;
        _onMouse = onMouse;
    }

    public void Start()
    {
        if (_inner != null)
            return;

        var wayland = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
        _inner = wayland
            ? new EvdevKeyboardHook(_onPress, _onRelease, _isInjecting, _onMouse)
            : new X11RecordKeyboardHook(_onPress, _onRelease, _isInjecting, _onMouse);

        try
        {
            _inner.Start();
        }
        catch
        {
            _inner = null;
            throw;
        }
    }

    public void Stop()
    {
        _inner?.Stop();
        _inner = null;
    }
}
