using System.Text.RegularExpressions;

namespace PersianTypoFixer.Core;

public sealed class AutoFixer : IDisposable
{
    private const int MaxBuffer = 120;
    private const int DebounceMs = 450;

    private static readonly Regex BoundaryRe = new(@"[\s.,!?؟؛;:،…»)\]}\-–—]$", RegexOptions.Compiled);
    private static readonly Regex TailRe = new(@"(?:[^\s]+\s+)?[^\s]+(?:\s+)?$", RegexOptions.Compiled);

    private static readonly HashSet<string> ResetKeys = new(StringComparer.Ordinal)
    {
        "left", "right", "up", "down", "home", "end", "page_up", "page_down", "delete", "esc", "tab", "enter",
    };

    private readonly Func<FixOptions> _options;
    private readonly IInputBackend _input;
    private readonly object _gate = new();
    private string _buffer = "";
    private Timer? _timer;
    private int _generation;
    private volatile bool _injecting;
    private bool _ctrl;
    private bool _alt;
    private bool _cmd;
    private IKeyboardHook? _keyboard;
    private IDisposable? _mouse;

    public AutoFixer(Func<FixOptions> options, IInputBackend input)
    {
        _options = options;
        _input = input;
    }

    public event Action<string, string>? Replaced;

    public event Action<SemanticNotice>? NoticeDetected;

    public void Start()
    {
        if (_keyboard != null)
            return;
        if (OperatingSystem.IsWindows())
        {
            _keyboard = new Platform.WindowsKeyboardHook(OnPress, OnRelease, () => _injecting);
            _mouse = new Platform.WindowsMouseHook(() => Reset());
            ((Platform.WindowsMouseHook)_mouse).Start();
        }
        else
        {
            return;
        }
        _keyboard.Start();
    }

    public void Stop()
    {
        if (_keyboard != null)
        {
            _keyboard.Stop();
            _keyboard = null;
        }
        _mouse?.Dispose();
        _mouse = null;
        Reset();
    }

    public void Reset()
    {
        Cancel();
        lock (_gate)
        {
            _buffer = "";
            _generation++;
        }
    }

    private void OnRelease(KeyEvent ev)
    {
        switch (ev.Name)
        {
            case "ctrl": _ctrl = false; break;
            case "alt": _alt = false; break;
            case "cmd": _cmd = false; break;
        }
    }

    private void OnPress(KeyEvent ev)
    {
        if (_injecting)
            return;
        var name = ev.Name;
        if (name == "ctrl")
        {
            _ctrl = true;
            Reset();
            return;
        }
        if (name == "alt")
        {
            _alt = true;
            Reset();
            return;
        }
        if (name == "cmd")
        {
            _cmd = true;
            Reset();
            return;
        }
        if (_ctrl || _alt || _cmd)
            return;
        if (name == "shift")
            return;
        if (name == "backspace")
        {
            lock (_gate)
            {
                if (_buffer.Length > 0)
                    _buffer = _buffer[..^1];
                _generation++;
            }
            Cancel();
            return;
        }
        if (name != null && ResetKeys.Contains(name))
        {
            Reset();
            return;
        }
        var ch = ev.Char;
        if (ch == null)
            return;
        int gen;
        lock (_gate)
        {
            _buffer = ClampTail(_buffer + ch.Value);
            _generation++;
            gen = _generation;
        }
        Schedule(gen);
    }

    private void Schedule(int gen)
    {
        Cancel();
        var timer = new Timer(_ => MaybeFix(gen), null, DebounceMs, Timeout.Infinite);
        lock (_gate)
        {
            _timer = timer;
        }
    }

    private void Cancel()
    {
        Timer? timer;
        lock (_gate)
        {
            timer = _timer;
            _timer = null;
        }
        timer?.Dispose();
    }

    private void MaybeFix(int gen)
    {
        var options = _options();
        if (!options.Enabled)
            return;
        string text;
        lock (_gate)
        {
            if (gen != _generation)
                return;
            text = _buffer;
            _timer = null;
        }
        if (options.Notices)
            EmitNotices(text);
        if (!options.AutoFix)
            return;
        if (text.Trim().Length == 0 || !BoundaryRe.IsMatch(text))
            return;
        var match = TailRe.Match(text);
        var snippet = match.Success ? match.Value : text;
        string fixedText;
        try
        {
            fixedText = Fixer.FixText(snippet, options);
        }
        catch
        {
            return;
        }
        if (fixedText == snippet)
            return;
        Replace(snippet, fixedText);
        lock (_gate)
        {
            if (gen == _generation)
            {
                _buffer = ClampTail(text[..^snippet.Length] + fixedText);
                _generation++;
            }
        }
    }

    private void EmitNotices(string text)
    {
        List<SemanticNotice> found;
        try
        {
            found = Notices.Find(text);
        }
        catch
        {
            return;
        }
        foreach (var notice in found)
        {
            try
            {
                NoticeDetected?.Invoke(notice);
            }
            catch
            {
            }
        }
    }

    private void Replace(string text, string fixedText)
    {
        _injecting = true;
        try
        {
            _input.PressBackspace(text.Length);
            var backup = _input.GetClipboard();
            _input.SetClipboard(fixedText);
            Thread.Sleep(30);
            _input.Paste();
            _ = Task.Run(async () =>
            {
                await Task.Delay(600).ConfigureAwait(false);
                try
                {
                    _input.SetClipboard(backup);
                }
                catch
                {
                }
            });
            Thread.Sleep(200);
            Replaced?.Invoke(text, fixedText);
        }
        finally
        {
            _injecting = false;
        }
    }

    public void Dispose() => Stop();

    private static string ClampTail(string value)
        => value.Length > MaxBuffer ? value[^MaxBuffer..] : value;
}
