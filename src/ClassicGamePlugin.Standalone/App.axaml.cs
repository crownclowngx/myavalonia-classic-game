using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace ClassicGamePlugin.Standalone;

public sealed partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var arguments = desktop.Args ?? [];
            var checkIndex = Array.IndexOf(arguments, "--rich-town-play-check");
            if (checkIndex >= 0 && checkIndex + 1 >= arguments.Length) throw new ArgumentException("小镇窗口检查需要显式报告路径。");
            desktop.MainWindow = checkIndex >= 0
                ? new Features.RichTown.RichTownPlayWindow(arguments[checkIndex + 1])
                : arguments.Contains("--rich-town-play", StringComparer.Ordinal)
                ? new Features.RichTown.RichTownPlayWindow()
                : desktop.Args?.Contains("--rich-town-probe", StringComparer.Ordinal) == true
                ? new Features.RichTown.RichTownProbeWindow()
                : new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
