using System.Diagnostics;
using System.Globalization;
using Avalonia;

namespace CornerBreak.MacDesktop;

public sealed class DesktopOptions
{
    public bool Preview { get; set; }
    public DateTime? PreviewAt { get; set; }
    public string PreviewScenario { get; set; } = "Waiting";
    public string PreviewMascot { get; set; } = "cat";
    public bool SmokeTest { get; set; }
    public string? RenderDirectory { get; set; }

    public static DesktopOptions Parse(string[] args)
    {
        var options = new DesktopOptions();
        string Value(ref int index)
        {
            if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
                throw new ArgumentException("参数缺少值。");
            return args[index];
        }
        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i].TrimStart('-').Replace("-", "").ToLowerInvariant();
            switch (key)
            {
                case "preview": options.Preview = true; break;
                case "previewat": options.PreviewAt = DateTime.Parse(Value(ref i), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind); break;
                case "previewscenario": options.PreviewScenario = Value(ref i); break;
                case "previewmascot": options.PreviewMascot = Value(ref i); break;
                case "smoketest": options.SmokeTest = options.Preview = true; break;
                case "renderui": options.RenderDirectory = Path.GetFullPath(Value(ref i)); options.Preview = true; break;
                default: throw new ArgumentException("未知参数：" + args[i]);
            }
        }
        if (!new[] { "Waiting", "Active", "Paused", "Skipped", "Disabled" }.Contains(options.PreviewScenario))
            throw new ArgumentException("预览状态无效。");
        if (!MascotImages.Options.Any(item => item.Id == options.PreviewMascot))
            throw new ArgumentException("预览角色无效。");
        return options;
    }
}

internal static class Program
{
    internal static DesktopOptions Options { get; private set; } = new();

    [STAThread]
    public static int Main(string[] args)
    {
        FileStream? instanceLock = null;
        try
        {
            Options = DesktopOptions.Parse(args);
            if (!Options.Preview)
            {
                var directory = Path.GetDirectoryName(AppPaths.GetSettingsPath())!;
                Directory.CreateDirectory(directory);
                try { instanceLock = new FileStream(Path.Combine(directory, "instance.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
                catch (IOException)
                {
                    Console.Error.WriteLine("CornerBreak 已在运行，请从菜单栏图标或常驻挂件打开。");
                    return 0;
                }
            }
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            if (!Options.Preview)
            {
                try
                {
                    var directory = Path.GetDirectoryName(AppPaths.GetSettingsPath())!;
                    Directory.CreateDirectory(directory);
                    File.AppendAllText(Path.Combine(directory, "CornerBreak.log"), DateTime.Now.ToString("o") + " " + error + Environment.NewLine);
                }
                catch { }
            }
            return 1;
        }
        finally { instanceLock?.Dispose(); }
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect().WithInterFont().LogToTrace();
}
