using Avalonia;
using Avalonia.Controls;

namespace PersianTypoFixer.App;

public partial class NoticeWindow : Window
{
    public NoticeWindow() : this("")
    {
    }

    public NoticeWindow(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
        CloseButton.Click += (_, _) => Close();
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
        if (screen == null)
            return;
        var area = screen.WorkingArea;
        var scale = screen.Scaling;
        var height = (int)Math.Ceiling(Bounds.Height * scale);
        Position = new PixelPoint(area.X + 24, area.Bottom - height - 24);
    }
}
