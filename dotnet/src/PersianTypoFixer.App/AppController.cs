using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;
using PersianTypoFixer.Core;
using PersianTypoFixer.Core.Platform;

namespace PersianTypoFixer.App;

public sealed class AppController
{
    private const string Sentinel = "PTF-CLIP-SENTINEL-9A3F4B2C";

    private readonly IClassicDesktopStyleApplicationLifetime _lifetime;
    private readonly IInputBackend _input;
    private AppConfig _config = new();
    private AutoFixer? _autoFixer;
    private WindowsHotkey? _hotkey;
    private LinuxHotkey? _linuxHotkey;
    private TrayIcon? _tray;
    private NativeMenuItem? _enabledItem;
    private SettingsWindow? _window;
    private bool _trayAvailable;
    private readonly Queue<SemanticNotice> _noticeQueue = new();
    private readonly Dictionary<string, DateTime> _noticeSeen = new(StringComparer.Ordinal);
    private NoticeWindow? _noticeWindow;
    private DispatcherTimer? _noticeTimer;

    public AppController(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        _lifetime = lifetime;
        if (OperatingSystem.IsWindows())
            _input = new WindowsInput();
        else if (OperatingSystem.IsLinux())
            _input = new LinuxInput();
        else
            _input = new NullInput();
    }

    public AppConfig Config => _config;

    public string Status { get; private set; } = "";

    public event Action<string>? StatusChanged;

    public void Start()
    {
        _config = AppConfig.Load();
        var firstRun = !File.Exists(AppConfig.FilePath);
        CreateTray();
        ApplyConfig(save: false);
        if (firstRun || !_trayAvailable)
            ShowWindow();
    }

    public void ApplyConfig(bool save = true)
    {
        if (save)
            _config.Save();

        if (OperatingSystem.IsWindows())
        {
            _hotkey?.Dispose();
            _hotkey = null;
            try
            {
                var hotkey = new WindowsHotkey(FixSelection, _config.Hotkey);
                hotkey.Start();
                if (hotkey.IsRegistered)
                {
                    _hotkey = hotkey;
                }
                else
                {
                    hotkey.Dispose();
                    SetStatus($"میانبر {_config.Hotkey} در دسترس نیست (برنامه دیگری از آن استفاده می‌کند).");
                }
            }
            catch (Exception ex)
            {
                SetStatus("خطا در ثبت میانبر: " + ex.Message);
            }

            if (_autoFixer == null)
            {
                _autoFixer = new AutoFixer(() => _config, _input);
                _autoFixer.NoticeDetected += ShowNotice;
                try
                {
                    _autoFixer.Start();
                }
                catch (Exception ex)
                {
                    SetStatus("خطا در راه‌اندازی اصلاح خودکار: " + ex.Message);
                }
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            _linuxHotkey?.Dispose();
            _linuxHotkey = null;
            try
            {
                var hotkey = new LinuxHotkey(FixSelection, _config.Hotkey);
                hotkey.Start();
                if (hotkey.IsRegistered)
                {
                    _linuxHotkey = hotkey;
                    SetStatus($"آماده — میانبر: {_config.Hotkey} (اصلاح خودکار هنگام تایپ فقط در ویندوز)");
                }
                else
                {
                    hotkey.Dispose();
                    SetStatus("میانبر سراسری ثبت نشد (X11 لازم است)؛ از منوی سینی برای اصلاح متن استفاده کنید.");
                }
            }
            catch (Exception ex)
            {
                SetStatus("خطا در ثبت میانبر: " + ex.Message);
            }
            if (_input is LinuxInput linux && !linux.IsAvailable)
                SetStatus(Status + " | xclip یا xsel نصب نیست؛ کلیپ‌بورد در دسترس نیست.");
        }

        if (_enabledItem != null)
            _enabledItem.IsChecked = _config.Enabled;
        UpdateTrayText();

        if (!_config.Enabled || !_config.Notices)
        {
            _noticeQueue.Clear();
            _noticeWindow?.Close();
        }

        if (string.IsNullOrEmpty(Status))
            SetStatus(DefaultStatus());

        _window?.RefreshFromConfig();
        _window?.UpdateStatus();
    }

    public void FixSelection() => FixSelectionCore(copyOnly: false);

    public void FixSelectionAndCopy() => FixSelectionCore(copyOnly: true);

    private void FixSelectionCore(bool copyOnly)
    {
        if (!_config.Enabled)
        {
            SetStatus("برنامه غیرفعال است.");
            return;
        }
        Task.Run(() =>
        {
            try
            {
                var backup = _input.GetClipboard();
                _input.SetClipboard(Sentinel);
                Thread.Sleep(150);
                _input.Copy();
                var text = Sentinel;
                var deadline = DateTime.UtcNow.AddMilliseconds(1200);
                while (DateTime.UtcNow < deadline)
                {
                    Thread.Sleep(30);
                    text = _input.GetClipboard();
                    if (text != Sentinel)
                        break;
                }
                if (text == Sentinel || string.IsNullOrWhiteSpace(text))
                {
                    _input.SetClipboard(backup);
                    SetStatus("متنی انتخاب نشده بود.");
                    return;
                }
                NotifyNotices(text);
                var fixedText = Fixer.FixText(text, _config);
                if (copyOnly)
                {
                    _input.SetClipboard(fixedText);
                    SetStatus(fixedText == text
                        ? "متن در کلیپ‌بورد کپی شد."
                        : "متن اصلاح‌شده در کلیپ‌بورد کپی شد.");
                    return;
                }
                if (fixedText == text)
                {
                    _input.SetClipboard(backup);
                    SetStatus("تغییری لازم نبود.");
                    return;
                }
                _input.SetClipboard(fixedText);
                Thread.Sleep(OperatingSystem.IsLinux() ? 150 : 40);
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
                SetStatus("متن انتخاب‌شده اصلاح شد.");
            }
            catch (Exception ex)
            {
                SetStatus("خطا: " + ex.Message);
            }
        });
    }

    public void NotifyNotices(string text)
    {
        if (!_config.Enabled || !_config.Notices || string.IsNullOrWhiteSpace(text))
            return;
        List<SemanticNotice> found;
        try
        {
            found = Notices.Find(text);
        }
        catch
        {
            return;
        }
        if (found.Count == 0)
            return;
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var notice in found)
                EnqueueNotice(notice);
        });
    }

    public void ShowNotice(SemanticNotice notice)
    {
        if (notice != null)
            Dispatcher.UIThread.Post(() => EnqueueNotice(notice));
    }

    private void EnqueueNotice(SemanticNotice notice)
    {
        if (!_config.Enabled || !_config.Notices)
            return;
        var key = notice.Id + ":" + notice.Word;
        if (_noticeSeen.TryGetValue(key, out var seen) &&
            DateTime.UtcNow - seen < TimeSpan.FromMinutes(10))
        {
            return;
        }
        _noticeSeen[key] = DateTime.UtcNow;
        _noticeQueue.Enqueue(notice);
        ShowNextNotice();
    }

    private void ShowNextNotice()
    {
        if (_noticeWindow != null || _noticeQueue.Count == 0)
            return;
        var notice = _noticeQueue.Dequeue();
        var window = new NoticeWindow(notice.Message);
        _noticeWindow = window;
        window.Closed += (_, _) =>
        {
            _noticeTimer?.Stop();
            _noticeTimer = null;
            _noticeWindow = null;
            ShowNextNotice();
        };
        window.Show();
        _noticeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(9) };
        _noticeTimer.Tick += (_, _) => window.Close();
        _noticeTimer.Start();
    }

    public void ShowWindow()
    {
        if (_window == null)
        {
            _window = new SettingsWindow(this);
            _window.Closed += (_, _) => _window = null;
        }
        _window.Show();
        _window.Activate();
        _window.RefreshFromConfig();
        _window.UpdateStatus();
    }

    public void Exit()
    {
        try
        {
            _noticeTimer?.Stop();
            _noticeTimer = null;
            _noticeWindow?.Close();
            _noticeWindow = null;
            _autoFixer?.Dispose();
            _hotkey?.Dispose();
            _linuxHotkey?.Dispose();
            _tray?.Dispose();
        }
        catch
        {
        }
        _lifetime.Shutdown();
    }

    private void ToggleEnabled()
    {
        _config.Enabled = !_config.Enabled;
        if (_enabledItem != null)
            _enabledItem.IsChecked = _config.Enabled;
        _config.Save();
        UpdateTrayText();
        SetStatus(_config.Enabled ? "برنامه فعال شد." : "برنامه غیرفعال شد.");
        _window?.RefreshFromConfig();
    }

    private void UpdateTrayText()
    {
        if (_tray != null)
            _tray.ToolTipText = "اصلاح‌گر تایپ فارسی" + (_config.Enabled ? " — فعال" : " — غیرفعال");
    }

    public void SetStatus(string status)
    {
        Status = status;
        StatusChanged?.Invoke(status);
    }

    private string DefaultStatus()
    {
        if (OperatingSystem.IsWindows())
            return $"آماده — میانبر: {_config.Hotkey}";
        if (OperatingSystem.IsLinux())
            return $"آماده — میانبر: {_config.Hotkey}";
        return "این سیستم‌عامل پشتیبانی نمی‌شود.";
    }

    private void CreateTray()
    {
        try
        {
            _tray = new TrayIcon
            {
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://PersianTypoFixer/Assets/icon.png"))),
                ToolTipText = "اصلاح‌گر تایپ فارسی",
                IsVisible = true,
            };
            var menu = new NativeMenu();
            _enabledItem = new NativeMenuItem("فعال")
            {
                ToggleType = NativeMenuItemToggleType.CheckBox,
                IsChecked = _config.Enabled,
            };
            _enabledItem.Click += (_, _) => ToggleEnabled();
            var fixItem = new NativeMenuItem("اصلاح متن انتخاب‌شده");
            fixItem.Click += (_, _) => FixSelection();
            var copyItem = new NativeMenuItem("اصلاح و کپی");
            copyItem.Click += (_, _) => FixSelectionAndCopy();
            var settingsItem = new NativeMenuItem("تنظیمات…");
            settingsItem.Click += (_, _) => ShowWindow();
            var exitItem = new NativeMenuItem("خروج");
            exitItem.Click += (_, _) => Exit();
            menu.Items.Add(_enabledItem);
            menu.Items.Add(new NativeMenuItemSeparator());
            menu.Items.Add(fixItem);
            menu.Items.Add(copyItem);
            menu.Items.Add(new NativeMenuItemSeparator());
            menu.Items.Add(settingsItem);
            menu.Items.Add(new NativeMenuItemSeparator());
            menu.Items.Add(exitItem);
            _tray.Menu = menu;
            _tray.Clicked += (_, _) => ShowWindow();
            _trayAvailable = true;
        }
        catch
        {
            _trayAvailable = false;
        }
    }

    private sealed class NullInput : IInputBackend
    {
        public string GetClipboard() => "";
        public void SetClipboard(string text) { }
        public void PressBackspace(int count) { }
        public void Paste() { }
        public void Copy() { }
    }
}
