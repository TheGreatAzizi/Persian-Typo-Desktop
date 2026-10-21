using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PersianTypoFixer.Core;

namespace PersianTypoFixer.App;

public partial class SettingsWindow : Window
{
    private static readonly (string Section, string Property, string Label, string Hint)[] Descriptors =
    {
        ("عمومی", nameof(FixOptions.Enabled), "برنامه فعال باشد",
            "میانبر، هشدارها و اصلاح خودکار در دسترس باشند."),
        ("عمومی", nameof(FixOptions.AutoFix), "اصلاح خودکار هنگام تایپ",
            "پس از هر توقف کوتاه، همهٔ کلمه‌های تایپ‌شدهٔ اخیر اصلاح می‌شوند."),

        ("هشدار معنایی", nameof(FixOptions.Notices), "نمایش هشدار معنایی",
            "برای «بزار / بذار»، «می‌زارم / می‌گذارم»، «حیات / حیاط» و… نوتیف راهنما نشان بده."),

        ("قواعد اصلاح", nameof(FixOptions.FixArabicYK), "ی و ک عربی ← فارسی",
            "ي، ك و ى را به ی و ک تبدیل می‌کند."),
        ("قواعد اصلاح", nameof(FixOptions.FixArabicAlef), "همزه و آ ← ا",
            "أ، إ و آ را به ا تبدیل می‌کند."),
        ("قواعد اصلاح", nameof(FixOptions.FixTashkeel), "حذف اعراب",
            "فتحه، کسره، ضمه و سایر حرکت‌ها را حذف می‌کند."),
        ("قواعد اصلاح", nameof(FixOptions.FixKashida), "حذف کشیده",
            "کشیده (ـ) را از میان کلمات حذف می‌کند."),
        ("قواعد اصلاح", nameof(FixOptions.FixZWNJ), "اصلاح نیم‌فاصله",
            "«می روم» ← «می‌روم» و «کتاب ها» ← «کتاب‌ها»."),
        ("قواعد اصلاح", nameof(FixOptions.FixAttachedHa), "اصلاح «ها»ی چسبیده",
            "«کتابها» ← «کتاب‌ها»."),
        ("قواعد اصلاح", nameof(FixOptions.FixCompounds), "کلمات مرکب",
            "ترکیب‌های رایج مثل «به‌ترین» را به «بهترین» تبدیل می‌کند."),
        ("قواعد اصلاح", nameof(FixOptions.FixCommonTypos), "غلط‌های رایج",
            "غلط‌های پرتکرار را به شکل درست تبدیل می‌کند."),

        ("نشانه‌گذاری", nameof(FixOptions.FixPunctuation), "فاصله‌های نشانه‌گذاری",
            "فاصلهٔ اشتباه قبل از ، . ! ؟ را اصلاح می‌کند."),
        ("نشانه‌گذاری", nameof(FixOptions.FixPersianPunct), "نشانه‌گذاری فارسی",
            "ویرگول و علامت سؤال لاتین را فارسی می‌کند."),
        ("نشانه‌گذاری", nameof(FixOptions.FixDoubleSpace), "حذف فاصله‌های تکراری",
            "فاصله‌های پشت‌سرهم را به یک فاصله کاهش می‌دهد."),
        ("نشانه‌گذاری", nameof(FixOptions.FixQuotes), "گیومه‌های فارسی",
            "گیومه‌های لاتین را به «...» تبدیل می‌کند."),

        ("اعداد", nameof(FixOptions.FixArabicDigits), "اعداد عربی ← فارسی",
            "٠١٢٣٤٥٦٧٨٩ عربی را به ۰۱۲۳۴۵۶۷۸۹ فارسی تبدیل می‌کند."),
        ("اعداد", nameof(FixOptions.FixLatinDigits), "اعداد لاتین ← فارسی",
            "0123456789 را به اعداد فارسی تبدیل می‌کند."),
        ("اعداد", nameof(FixOptions.FixSeparators), "اصلاح جداکنندهٔ اعداد",
            "کاما و جداکنندهٔ هزارگان را مرتب می‌کند."),
        ("اعداد", nameof(FixOptions.FixRepeat), "اصلاح تکرار حروف",
            "«خیییلی» ← «خیلی»."),
    };

    private readonly AppController _controller;
    private readonly List<(ToggleSwitch Toggle, PropertyInfo Property)> _checks = new();
    private string? _pendingHotkey;

    public SettingsWindow(AppController controller)
    {
        _controller = controller;
        InitializeComponent();
        BuildOptions();
        HotkeyBox.KeyDown += OnHotkeyKeyDown;
        SaveButton.Click += (_, _) => Save();
        CloseButton.Click += (_, _) => Hide();
        ExitButton.Click += (_, _) => _controller.Exit();
        TestInputBox.TextChanged += (_, _) => UpdateTest();
        _controller.StatusChanged += OnStatusChanged;
        Closing += (_, e) =>
        {
            e.Cancel = true;
            Hide();
        };
        var version = typeof(SettingsWindow).Assembly.GetName().Version;
        VersionText.Text = version == null
            ? "نسخه ۲٫۱٫۰"
            : $"نسخه {version.ToString(3)}";
        TestInputBox.Text = "من می خواستم برم کتاب ها رو بزارم";
        RefreshFromConfig();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        WindowChrome.UseDarkTitleBar(this);
    }

    private void BuildOptions()
    {
        foreach (var (section, propertyName, label, hint) in Descriptors)
        {
            var panel = section switch
            {
                "عمومی" => GeneralPanel,
                "هشدار معنایی" => NoticesPanel,
                "قواعد اصلاح" => RulesPanel,
                "نشانه‌گذاری" => PunctuationPanel,
                "اعداد" => NumbersPanel,
                _ => null,
            };
            if (panel == null)
                continue;
            var property = typeof(FixOptions).GetProperty(propertyName);
            if (property == null)
                continue;
            var toggle = new ToggleSwitch
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            var hintText = hint;
            if (propertyName == nameof(FixOptions.AutoFix) &&
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
            {
                hintText += " در Wayland به ydotool و دسترسی /dev/input نیاز دارد.";
            }
            _checks.Add((toggle, property));
            panel.Children.Add(BuildRow(label, hintText, toggle));
        }
    }

    private static Control BuildRow(string label, string hint, ToggleSwitch toggle)
    {
        var texts = new StackPanel { Spacing = 2 };
        texts.Children.Add(new TextBlock { Text = label, Classes = { "label" } });
        if (!string.IsNullOrWhiteSpace(hint))
            texts.Children.Add(new TextBlock { Text = hint, Classes = { "hint" } });

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(texts);
        Grid.SetColumn(toggle, 1);
        grid.Children.Add(toggle);

        return new Border { Classes = { "optionRow" }, Child = grid };
    }

    public void RefreshFromConfig()
    {
        var config = _controller.Config;
        foreach (var (toggle, property) in _checks)
            toggle.IsChecked = property.GetValue(config) is true;
        HotkeyBox.Text = config.Hotkey;
        _pendingHotkey = null;
        CustomWordsBox.Text = string.Join(Environment.NewLine,
            config.CustomWords.Where(w => w.On && !string.IsNullOrWhiteSpace(w.From))
                .Select(w => $"{w.From} = {w.To}"));
        UpdateTest();
        UpdateStatus();
    }

    public void UpdateStatus()
    {
        StatusText.Text = string.IsNullOrEmpty(_controller.Status)
            ? "آماده"
            : _controller.Status;
    }

    private void OnStatusChanged(string status)
    {
        Dispatcher.UIThread.Post(() => StatusText.Text = status);
    }

    private void OnHotkeyKeyDown(object? sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin or Key.None)
            return;
        var name = KeyName(key);
        if (name == null)
            return;
        var modifiers = new List<string>();
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            modifiers.Add("ctrl");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            modifiers.Add("shift");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
            modifiers.Add("alt");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta))
            modifiers.Add("win");
        if (modifiers.Count == 0)
        {
            _controller.SetStatus("میانبر باید حداقل یک کلید اصلاح‌گر (Ctrl، Shift، Alt یا Win) داشته باشد.");
            StatusText.Text = _controller.Status;
            return;
        }
        _pendingHotkey = string.Join("+", modifiers) + "+" + name;
        HotkeyBox.Text = _pendingHotkey;
    }

    private static string? KeyName(Key key)
    {
        if (key is >= Key.A and <= Key.Z)
            return key.ToString().ToLowerInvariant();
        if (key is >= Key.D0 and <= Key.D9)
            return ((int)(key - Key.D0)).ToString();
        if (key is >= Key.F1 and <= Key.F24)
            return key.ToString().ToLowerInvariant();
        return key switch
        {
            Key.Space => "space",
            Key.Tab => "tab",
            Key.Enter => "enter",
            Key.Escape => "esc",
            Key.Back => "backspace",
            Key.Insert => "insert",
            Key.Delete => "delete",
            Key.Home => "home",
            Key.End => "end",
            Key.PageUp => "pageup",
            Key.PageDown => "pagedown",
            Key.Left => "left",
            Key.Up => "up",
            Key.Right => "right",
            Key.Down => "down",
            _ => null,
        };
    }

    private void Save()
    {
        var config = _controller.Config;
        foreach (var (toggle, property) in _checks)
            property.SetValue(config, toggle.IsChecked == true);
        if (!string.IsNullOrEmpty(_pendingHotkey))
            config.Hotkey = _pendingHotkey;
        config.CustomWords = ParseCustomWords(CustomWordsBox.Text ?? "");
        _controller.ApplyConfig();
        _pendingHotkey = null;
        _controller.SetStatus("تنظیمات ذخیره شد.");
        UpdateStatus();
        UpdateTest();
    }

    private void UpdateTest()
    {
        var text = TestInputBox.Text ?? "";
        if (string.IsNullOrWhiteSpace(text))
        {
            TestResultText.Text = "نتیجه اینجا نمایش داده می‌شود…";
        }
        else
        {
            try
            {
                var fixedText = Fixer.FixText(text, _controller.Config);
                TestResultText.Text = string.IsNullOrWhiteSpace(fixedText) ? "—" : fixedText;
            }
            catch
            {
                TestResultText.Text = text;
            }
        }

        var notices = Notices.Find(text);
        if (notices.Count == 0)
        {
            TestNoticePanel.Background = Brush.Parse("#141414");
            TestNoticePanel.BorderBrush = Brush.Parse("#2A2A2A");
            TestNoticeTitle.Foreground = Brush.Parse("#9CA3AF");
            TestNoticeText.Foreground = Brush.Parse("#9CA3AF");
            TestNoticeText.Text = string.IsNullOrWhiteSpace(text)
                ? "متن نمونه را بنویسید تا هشدارها بررسی شوند."
                : "برای این متن هشدار معنایی پیدا نشد.";
            return;
        }
        TestNoticePanel.Background = Brush.Parse("#2A2110");
        TestNoticePanel.BorderBrush = Brush.Parse("#78350F");
        TestNoticeTitle.Foreground = Brush.Parse("#FBBF24");
        TestNoticeText.Foreground = Brush.Parse("#FDE68A");
        TestNoticeText.Text = string.Join("\n\n", notices.Select(n => "• " + n.Message));
    }

    private static List<CustomWord> ParseCustomWords(string text)
    {
        var words = new List<CustomWord>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            var index = line.IndexOf('=');
            if (index <= 0)
                continue;
            var from = line[..index].Trim();
            var to = line[(index + 1)..].Trim();
            if (from.Length == 0 || to.Length == 0)
                continue;
            words.Add(new CustomWord { On = true, From = from, To = to });
        }
        return words;
    }
}
