using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace CornerBreak.MacDesktop;

internal sealed class ActionCommand(Action action) : ICommand
{
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => action();
    public event EventHandler? CanExecuteChanged { add { } remove { } }
}

public sealed record MascotChoice(string Id, string Label);

public sealed class MascotImages : IDisposable
{
    public static readonly MascotChoice[] Options =
    [new("cat", "猫猫探头"), new("fish", "蓝色大肥鱼"), new("companion", "二次元伙伴"), new("none", "不显示角色")];
    private readonly Dictionary<string, Bitmap> images = new();
    public MascotImages()
    {
        foreach (var option in Options.Where(item => item.Id != "none"))
        {
            using var stream = AssetLoader.Open(new Uri("avares://CornerBreak.MacDesktop/Assets/" + option.Id + ".png"));
            images[option.Id] = new Bitmap(stream);
        }
    }
    public Bitmap? Get(string id) => images.GetValueOrDefault(id);
    public int Count => images.Count;
    public static WindowIcon CreateIcon()
    {
        using var stream = AssetLoader.Open(new Uri("avares://CornerBreak.MacDesktop/Assets/cat.png"));
        return new WindowIcon(stream);
    }
    public void Dispose() { foreach (var image in images.Values) image.Dispose(); images.Clear(); }
}

internal static class Ui
{
    public static readonly IBrush Background = Brush.Parse("#F6F8FC");
    public static readonly IBrush Navy = Brush.Parse("#27354B");
    public static readonly IBrush Muted = Brush.Parse("#677589");
    public static readonly IBrush Accent = Brush.Parse("#3569B2");

    public static TextBlock Text(string text, double size = 14, bool bold = false) => new()
    {
        Text = text, FontSize = size, FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal,
        Foreground = Navy, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center
    };

    public static Button Button(string text, Action action, bool primary = false)
    {
        var button = new Button
        {
            Content = text, Padding = new Thickness(14, 9), HorizontalContentAlignment = HorizontalAlignment.Center,
            Background = primary ? Accent : Brushes.White, Foreground = primary ? Brushes.White : Navy,
            CornerRadius = new CornerRadius(5)
        };
        button.Click += (_, _) => action();
        return button;
    }

    public static PixelRect WorkArea(Window window)
    {
        return (window.Screens.ScreenFromWindow(window) ?? window.Screens.Primary)?.WorkingArea ?? new PixelRect(0, 0, 1440, 900);
    }

    public static PixelPoint Clamp(Window window, PixelPoint position)
    {
        var area = (window.Screens.ScreenFromPoint(position) ?? window.Screens.Primary)?.WorkingArea ?? WorkArea(window);
        var scale = window.RenderScaling;
        var width = Math.Min(area.Width, Math.Max(1, (int)Math.Ceiling(window.Width * scale)));
        var height = Math.Min(area.Height, Math.Max(1, (int)Math.Ceiling(window.Height * scale)));
        return new PixelPoint(Math.Clamp(position.X, area.X, Math.Max(area.X, area.Right - width)),
            Math.Clamp(position.Y, area.Y, Math.Max(area.Y, area.Bottom - height)));
    }

    public static void SavePng(Window window, string path)
    {
        var scale = window.RenderScaling;
        using var bitmap = new RenderTargetBitmap(new PixelSize(Math.Max(1, (int)Math.Ceiling(window.Bounds.Width * scale)),
            Math.Max(1, (int)Math.Ceiling(window.Bounds.Height * scale))), new Vector(96 * scale, 96 * scale));
        bitmap.Render(window);
        bitmap.Save(path);
    }
}
