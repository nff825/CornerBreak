using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace CornerBreak.MacDesktop;

public sealed class WidgetWindow : Window
{
    private readonly DesktopHost host;
    private readonly Image mascot = new() { Width = 82, Height = 78, Stretch = Stretch.Uniform };
    private readonly TextBlock stateText = Ui.Text("下一次休息", 13);
    private readonly TextBlock countdown = Ui.Text("还有 00:00", 20, true);
    private readonly ColumnDefinition imageColumn = new() { Width = new GridLength(90) };
    private bool pressed, moved;
    private PixelPoint pressScreen, pressPosition;
    public bool IsPointerInteraction => pressed;

    public WidgetWindow(DesktopHost desktopHost)
    {
        host = desktopHost;
        Title = "CornerBreak · 常驻小挂件";
        Width = 280; Height = 108; CanResize = false;
        SystemDecorations = SystemDecorations.None;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        Background = Ui.Background;
        var text = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(stateText); text.Children.Add(countdown);
        var hint = Ui.Text("点击查看 · 拖动移动", 10); hint.Foreground = Ui.Muted; text.Children.Add(hint);
        var grid = new Grid { Margin = new Thickness(12, 9), ColumnDefinitions = new ColumnDefinitions() };
        grid.ColumnDefinitions.Add(imageColumn); grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        grid.Children.Add(mascot); Grid.SetColumn(text, 1); grid.Children.Add(text);
        var border = new Border { Width = 280, Height = 108, Child = grid, Background = Ui.Background,
            BorderBrush = Brush.Parse("#CBD6E5"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9) };
        Content = new Viewbox { Child = border, Stretch = Stretch.Uniform };
        PointerPressed += Press;
        PointerMoved += Move;
        PointerReleased += Release;
        PointerCaptureLost += (_, _) => { pressed = false; moved = false; };
        Closing += (_, e) => { if (!host.Exiting) { e.Cancel = true; host.Collapse(); } };
        Opened += (_, _) => ClampPosition();
    }

    public void Update(BreakPresentation presentation, BreakSettings settings, MascotImages images)
    {
        var image = images.Get(settings.Mascot);
        mascot.Source = image; mascot.IsVisible = image != null; imageColumn.Width = new GridLength(image == null ? 0 : 90);
        var state = presentation.State;
        (stateText.Text, countdown.Text) = state.Mode switch
        {
            "Active" => ("休息时间", "剩余 " + ReminderRules.FormatCountdown(state.SecondsLeft)),
            "Paused" => ("提醒已暂停", state.PauseUntil.HasValue ? "恢复 " + ReminderRules.FormatCountdown(state.SecondsLeft) : "等待手动恢复"),
            "Skipped" => ("本轮已跳过", state.Target.HasValue ? "下次 " + state.Target.Value.AddMinutes(50).ToString("HH:mm") : "等待下次提醒"),
            "Disabled" => ("提醒已关闭", "点击打开设置"),
            _ => ("下次休息 " + state.Target?.ToString("HH:mm"), "还有 " + ReminderRules.FormatCountdown(state.SecondsLeft))
        };
        ClampPosition();
    }

    public void Place(int? x, int? y)
    {
        var area = (Screens.Primary ?? Screens.ScreenFromWindow(this))?.WorkingArea ?? Ui.WorkArea(this);
        var scale = RenderScaling;
        Position = x.HasValue && y.HasValue ? new PixelPoint(x.Value, y.Value) :
            new PixelPoint(area.Right - (int)Math.Ceiling(280 * scale) - 20, area.Bottom - (int)Math.Ceiling(108 * scale) - 20);
        ClampPosition();
    }

    public void ClampPosition()
    {
        var area = (Screens.ScreenFromPoint(Position) ?? Screens.Primary)?.WorkingArea ?? Ui.WorkArea(this);
        Width = Math.Min(280, area.Width / RenderScaling);
        Height = Math.Min(108, area.Height / RenderScaling);
        Position = Ui.Clamp(this, Position);
    }

    private void Press(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        pressed = true; moved = false; pressScreen = this.PointToScreen(e.GetPosition(this)); pressPosition = Position;
        e.Pointer.Capture(this); e.Handled = true;
    }

    private void Move(object? sender, PointerEventArgs e)
    {
        if (!pressed) return;
        var screen = this.PointToScreen(e.GetPosition(this));
        var deltaX = screen.X - pressScreen.X; var deltaY = screen.Y - pressScreen.Y;
        if (!moved) moved = Math.Abs(deltaX) + Math.Abs(deltaY) >= Math.Max(4, 4 * RenderScaling);
        if (moved) { Position = Ui.Clamp(this, new PixelPoint(pressPosition.X + deltaX, pressPosition.Y + deltaY)); e.Handled = true; }
    }

    private void Release(object? sender, PointerReleasedEventArgs e)
    {
        if (!pressed) return;
        bool wasMoved = moved; pressed = moved = false; e.Pointer.Capture(null); e.Handled = true;
        if (wasMoved) host.SaveWidgetPosition();
        else if (host.Controller.State.Mode == "Disabled") host.OpenSettings();
        else host.OpenDetails();
    }
}

public sealed class DetailsWindow : Window
{
    private readonly DesktopHost host;
    private readonly Border surface;
    private readonly TextBlock title = Ui.Text("该休息一下了", 26, true);
    private readonly TextBlock detail = Ui.Text("", 13);
    private readonly TextBlock countdown = Ui.Text("10:00", 30, true);
    private readonly TextBlock caption = Ui.Text("本轮休息剩余", 12);
    private readonly TextBlock notice = Ui.Text("", 11);
    private readonly Image mascot = new() { Width = 116, Height = 74, Stretch = Stretch.Uniform };
    private readonly Dictionary<string, Border> cards = new();
    private readonly Button dismiss;

    public DetailsWindow(DesktopHost desktopHost)
    {
        host = desktopHost;
        Title = "CornerBreak · 角落小憩（Mac 测试版）";
        Width = 420; Height = 640; CanResize = false;
        SystemDecorations = SystemDecorations.None;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true; Background = Ui.Background;
        var root = new StackPanel { Spacing = 13, Margin = new Thickness(20) };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,116") };
        var brand = new StackPanel { Spacing = 7, VerticalAlignment = VerticalAlignment.Center };
        var name = Ui.Text("CORNERBREAK", 13, true); name.Foreground = Ui.Accent;
        brand.Children.Add(name); brand.Children.Add(Ui.Text("给忙碌的工作留一点休息时间", 11));
        header.Children.Add(brand); Grid.SetColumn(mascot, 1); header.Children.Add(mascot); root.Children.Add(header);
        root.Children.Add(title); detail.Foreground = Ui.Muted; root.Children.Add(detail);
        foreach (var card in new[]
        {
            ("Eyes", "眼", "让眼睛休息", "暂时离开屏幕，看看远处。", "#E2EEFB"),
            ("Water", "水", "喝点水", "放下手头的事，喝点水。", "#E1F2EE"),
            ("Move", "动", "起身活动", "离开工位，站起来走动一下。", "#FDEEDA")
        })
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("44,*"), ColumnSpacing = 12 };
            row.Children.Add(new Border { Background = Brush.Parse(card.Item5), Child = new TextBlock {
                Text = card.Item2, FontSize = 19, Foreground = Ui.Navy, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center }, MinHeight = 44 });
            var words = new StackPanel { Spacing = 4 }; words.Children.Add(Ui.Text(card.Item3, 15, true));
            var description = Ui.Text(card.Item4, 12); description.Foreground = Ui.Muted; words.Children.Add(description);
            Grid.SetColumn(words, 1); row.Children.Add(words);
            var border = new Border { Background = Brushes.White, Padding = new Thickness(12), Child = row, CornerRadius = new CornerRadius(4) };
            root.Children.Add(border); cards[card.Item1] = border;
        }
        root.Children.Add(countdown); caption.Foreground = Ui.Muted; root.Children.Add(caption);
        var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 10 };
        dismiss = Ui.Button("本轮不再提示", host.Skip, true); actions.Children.Add(dismiss);
        var pause = Ui.Button("暂停 / 恢复", host.ShowPauseMenu); Grid.SetColumn(pause, 1); actions.Children.Add(pause); root.Children.Add(actions);
        var lower = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 8 };
        lower.Children.Add(Ui.Button("提醒设置", host.OpenSettings));
        var collapse = Ui.Button("收起详情", host.Collapse); Grid.SetColumn(collapse, 1); lower.Children.Add(collapse);
        var exit = Ui.Button("退出", () => host.Exit()); Grid.SetColumn(exit, 2); lower.Children.Add(exit); root.Children.Add(lower);
        notice.Foreground = Brush.Parse("#9A6522"); root.Children.Add(notice);
        surface = new Border { Width = 420, Background = Ui.Background, Child = root,
            BorderBrush = Brush.Parse("#CBD6E5"), BorderThickness = new Thickness(1) };
        // Scale the entire vector surface, including text, pictures and
        // buttons. Width/height growth alone would leave these at base size.
        // Uniform scaling preserves glyph and mascot proportions.
        Content = new Viewbox { Child = surface, Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.Both, HorizontalAlignment = HorizontalAlignment.Stretch };
        Closing += (_, e) => { if (!host.Exiting) { e.Cancel = true; host.Collapse(); } };
    }

    public Size BaselineSize
    {
        get
        {
            // Visibility changes invalidate measure, so disabled cards and
            // the "none" mascot recompute the baseline before growth.
            surface.Measure(new Size(420, double.PositiveInfinity));
            return new Size(420, Math.Max(360, Math.Ceiling(surface.DesiredSize.Height)));
        }
    }

    public void Update(BreakPresentation presentation, BreakSettings settings, MascotImages images, string warning, bool preview)
    {
        mascot.Source = images.Get(settings.Mascot); mascot.IsVisible = mascot.Source != null;
        cards["Eyes"].IsVisible = settings.Eyes; cards["Water"].IsVisible = settings.Water; cards["Move"].IsVisible = settings.Move;
        var state = presentation.State;
        countdown.Text = ReminderRules.FormatCountdown(state.SecondsLeft);
        dismiss.Content = state.Mode == "Active" ? "本轮不再提示" : "收起详情";
        (title.Text, detail.Text, caption.Text) = state.Mode switch
        {
            "Active" => ("该休息一下了", "给自己一点空隙。到 " + state.Target?.ToString("HH:mm") + " 自动结束本轮。", "本轮休息剩余"),
            "Paused" => ("提醒已暂停", state.PauseUntil.HasValue ? "将在 " + state.PauseUntil.Value.ToString("HH:mm") + " 恢复，届时按正常时段提醒。" : "准备好后，从菜单选择恢复提醒。", state.PauseUntil.HasValue ? "距离恢复提醒" : "等待手动恢复"),
            "Skipped" => ("本轮已跳过", "下次提醒：" + state.Target?.AddMinutes(50).ToString("HH:mm"), "本轮结束倒计时"),
            "Disabled" => ("提醒项目已关闭", "打开提醒设置，选择眼睛、喝水或起身活动。", "随时可以重新开启"),
            _ => ("下一次小憩，稍后见", "下次提醒：" + state.Target?.ToString("HH:mm") + "，每小时 50 分至整点。", "距离下次提醒")
        };
        if (state.Mode == "Disabled") countdown.Text = "未开启";
        if (state.Mode == "Paused" && !state.PauseUntil.HasValue) countdown.Text = "已暂停";
        notice.Text = preview ? "预览模式 · 不修改日常设置" : warning;
        notice.IsVisible = notice.Text.Length != 0;
    }
}
