using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace CornerBreak.MacDesktop;

public sealed class App : Application
{
    private DesktopHost? host;
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Light;
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            lifetime.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            host = new DesktopHost(lifetime, Program.Options);
            lifetime.Exit += (_, _) => host.Dispose();
            host.Start();
            if (Program.Options.SmokeTest || Program.Options.RenderDirectory != null)
                DispatcherTimer.RunOnce(async () =>
                {
                    try
                    {
                        host.AssertReady();
                        if (Program.Options.RenderDirectory != null)
                            await host.RenderUiAsync(Program.Options.RenderDirectory);
                        Console.WriteLine("PASS: CornerBreak Avalonia startup, three embedded mascots and reminder surfaces.");
                        host.Exit(0);
                    }
                    catch (Exception error) { Console.Error.WriteLine(error); host.Exit(1); }
                }, TimeSpan.FromMilliseconds(700));
        }
        base.OnFrameworkInitializationCompleted();
    }
}
