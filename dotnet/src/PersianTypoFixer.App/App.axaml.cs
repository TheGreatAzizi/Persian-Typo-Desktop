using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PersianTypoFixer.Core;

namespace PersianTypoFixer.App;

public partial class App : Application
{
    private AppController? _controller;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _controller = new AppController(desktop);
            _controller.Start();
            if (desktop.Args?.Contains("--settings") == true)
                _controller.ShowWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}

