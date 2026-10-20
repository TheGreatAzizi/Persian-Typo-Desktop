using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using PersianTypoFixer.Core;

namespace PersianTypoFixer.App;

public partial class SettingsWindow : Window
{
    private static readonly (string Section, string Property, string Label)[] Descriptors =
    {
        ("عمومی", nameof(FixOptions.Enabled), "برنامه فعال باشد"),
        ("عمومی", nameof(FixOptions.AutoFix), "اصلاح خودکار هنگام تایپ"),
        ("قواعد اصلاح", nameof(FixOptions.FixArabicYK), "ی و ک عربی ← فارسی"),
        ("قواعد اصلاح", nameof(FixOptions.FixArabicAlef), "أ إ آ ← ا"),
        ("قواعد اصلاح", nameof(FixOptions.FixTashkeel), "حذف اعراب"),
        ("قواعد اصلاح", nameof(FixOptions.FixKashida), "حذف کشیده"),
        ("قواعد اصلاح", nameof(FixOptions.FixZWNJ), "اصلاح نیم‌فاصله"),
        ("قواعد اصلاح", nameof(FixOptions.FixAttachedHa), "اصلاح «ها»ی چسبیده"),
        ("قواعد اصلاح", nameof(FixOptions.FixCompounds), "کلمات مرکب"),
        ("قواعد اصلاح", nameof(FixOptions.FixCommonTypos), "غلط‌های رایج"),
        ("نشانه‌گذاری", nameof(FixOptions.FixPunctuation), "اصلاح فاصله‌های نشانه‌گذاری"),
        ("نشانه‌گذاری", nameof(FixOptions.FixPersianPunct), "نشانه‌گذاری فارسی"),
        ("نشانه‌گذاری", nameof(FixOptions.FixDoubleSpace), "حذف فاصله‌های تکراری"),
        ("نشانه‌گذاری", nameof(FixOptions.FixQuotes), "اصلاح گیومه‌ها"),
        ("اعداد", nameof(FixOptions.FixArabicDigits), "اعداد عربی ← فارسی"),
        ("اعداد", nameof(FixOptions.FixLatinDigits), "اعداد لاتین ← فارسی"),
        ("اعداد", nameof(FixOptions.FixSeparators), "اصلاح جداکننده‌های اعداد"),
        ("اعداد", nameof(FixOptions.FixRepeat), "اصلاح تکرار حروف"),
    };

    private readonly AppController _controller;
    private readonly List<(CheckBox Box, PropertyInfo Property)> _checks = new();
    private string? _pendingHotkey;

    public SettingsWindow(AppController controller)
    {
        _controller = controller;
        InitializeComponent();
        BuildOptions();
        HotkeyBox.Text = _controller.Config.Hotkey;
        HotkeyBox.KeyDown += OnHotkeyKeyDown;
        SaveButton.Click += (_, _) => Save();
        CloseButton.Click += (_, _) => Hide();
        ExitButton.Click += (_, _) => _controller.Exit();
        _controller.StatusChanged += OnStatusChanged;
        Closing += (_, e) =>
        {
            e.Cancel = true;
            Hide();
        };
    }

    private void BuildOptions()
    {
        string? lastSection = null;
        foreach (var (section, propertyName, label) in Descriptors)
        {
            if (section != lastSection)
            {
                OptionsPanel.Children.Add(new TextBlock
                {
                    Text = section,
                    FontWeight = Avalonia.Media.FontWeight.SemiBold,
                    Margin = new Avalonia.Thickness(0, 14, 0, 4),
                });
                lastSection = section;
            }
            var property = typeof(FixOptions).GetProperty(propertyName);
            if (property == null)
                continue;
            var box = new CheckBox { Content = label };
            if (propertyName == nameof(FixOptions.AutoFix) && !OperatingSystem.IsWindows())
            {
                box.IsEnabled = false;
                box.Content = label + " (فعلاً فقط در ویندوز)";
            }
            _checks.Add((box, property));
            OptionsPanel.Children.Add(box);
        }
    }

    public void RefreshFromConfig()
    {
        var config = _controller.Config;
        foreach (var (box, property) in _checks)
        {
            box.IsChecked = property.GetValue(config) is true;
        }
        HotkeyBox.Text = config.Hotkey;
        _pendingHotkey = null;
        CustomWordsBox.Text = string.Join(Environment.NewLine,
            config.CustomWords.Where(w => w.On && !string.IsNullOrWhiteSpace(w.From))
                .Select(w => $"{w.From} = {w.To}"));
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
        Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusText.Text = status);
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
        foreach (var (box, property) in _checks)
        {
            property.SetValue(config, box.IsChecked == true);
        }
        if (!string.IsNullOrEmpty(_pendingHotkey))
            config.Hotkey = _pendingHotkey;
        config.CustomWords = ParseCustomWords(CustomWordsBox.Text ?? "");
        _controller.ApplyConfig();
        _pendingHotkey = null;
        _controller.SetStatus("تنظیمات ذخیره شد.");
        UpdateStatus();
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
