using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;

namespace CornerBreak.MacDesktop;

public sealed class DesktopHost : IDisposable
{
    private readonly IClassicDesktopStyleApplicationLifetime lifetime;
    private readonly DesktopOptions options;
    private readonly MascotImages images = new();
    private readonly DispatcherTimer timer;
    private readonly Stopwatch previewWatch = new();
    private readonly DateTime previewStart;
    private readonly TrayIcon tray;
    private readonly string settingsPath = AppPaths.GetSettingsPath();
    private string warning;
    private SettingsWindow? settingsWindow;
    private ContextMenu? pauseMenu;
    private bool settingsOpen, disposed;
    private double animatedWidth, animatedHeight;

    public DisplayController Controller { get; }
    public WidgetWindow Widget { get; }
    public DetailsWindow Details { get; }
    public bool Exiting { get; private set; }
    public DateTime Now => options.Preview ? previewStart.Add(previewWatch.Elapsed) : DateTime.Now;

    public DesktopHost(IClassicDesktopStyleApplicationLifetime desktopLifetime, DesktopOptions desktopOptions)
    {
        lifetime = desktopLifetime; options = desktopOptions;
        warning = string.Empty;
        var settings = options.Preview ? new BreakSettings() : SettingsStore.LoadWithRecovery(settingsPath, out warning);
        previewStart = options.PreviewAt ?? DateTime.Today.AddHours(12).AddMinutes(options.PreviewScenario == "Waiting" ? 40 : 50);
        if (options.Preview)
        {
            settings.Mascot = options.PreviewMascot;
            switch (options.PreviewScenario)
            {
                case "Paused": settings.Paused = true; settings.PauseUntil = previewStart.AddHours(1).ToString("o"); break;
                case "Skipped": settings.SkippedHour = previewStart.ToString("yyyy-MM-dd-HH"); break;
                case "Disabled": settings.Eyes = settings.Water = settings.Move = false; break;
            }
            if (options.RenderDirectory == null && !options.SmokeTest) previewWatch.Start();
        }
        Controller = new DisplayController(settings, Now);
        Widget = new WidgetWindow(this);
        Details = new DetailsWindow(this);
        Details.Opened += (_, _) => UpdateGrowth(true);
        Widget.Place(settings.FloatingX, settings.FloatingY);
        tray = new TrayIcon { Icon = MascotImages.CreateIcon(), ToolTipText = "CornerBreak · 角落小憩", IsVisible = !options.Preview };
        tray.Clicked += (_, _) => OpenDetails();
        TrayIcon.SetIcons(Application.Current!, new TrayIcons { tray });
        RefreshMenus();
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        timer.Tick += (_, _) => Tick();
    }

    public void Start()
    {
        Tick();
        if (warning.Length != 0) OpenDetails();
        timer.Start();
    }

    private bool Interaction => settingsOpen || Widget.IsPointerInteraction || Widget.ContextMenu?.IsOpen == true ||
        Details.ContextMenu?.IsOpen == true || pauseMenu?.IsOpen == true;

    private void Tick()
    {
        if (Exiting) return;
        Controller.Tick(Now, Interaction);
        ApplyPresentation();
    }

    private void ApplyPresentation()
    {
        if (Exiting) return;
        var presentation = Controller.Current;
        var settings = Controller.Settings;
        Widget.Update(presentation, settings, images);
        Details.Update(presentation, settings, images, warning, options.Preview);
        if (presentation.View == BreakView.Details || settingsOpen)
        {
            bool newlyShown = !Details.IsVisible;
            if (newlyShown) { animatedWidth = animatedHeight = 0; UpdateGrowth(true); }
            Widget.Hide();
            if (newlyShown) Details.Show();
            UpdateGrowth(false);
        }
        else
        {
            Details.Hide();
            if (presentation.View == BreakView.Widget)
            {
                if (!Widget.IsVisible) Widget.Show();
                Widget.ClampPosition();
            }
            else Widget.Hide();
        }
        tray.ToolTipText = presentation.State.Mode switch
        {
            "Active" => "CornerBreak · 本轮剩余 " + ReminderRules.FormatCountdown(presentation.State.SecondsLeft),
            "Paused" => "CornerBreak · 已暂停",
            "Skipped" => "CornerBreak · 本轮已跳过",
            _ => "CornerBreak · 下次休息 " + presentation.State.Target?.ToString("HH:mm")
        };
    }

    private void UpdateGrowth(bool immediate)
    {
        if (!immediate && (Interaction || Details.IsPointerOver)) return;
        var settings = Controller.Settings;
        var screen = Widget.Screens.ScreenFromPoint(Widget.Position) ?? Widget.Screens.Primary;
        var area = screen?.WorkingArea ?? Ui.WorkArea(Details);
        var scale = Details.RenderScaling;
        var baseline = Details.BaselineSize;
        var target = ReminderRules.GetGrowth(Now, settings, baseline.Width, baseline.Height, area.Width / scale, area.Height / scale);
        if (immediate || animatedWidth <= 0)
        { animatedWidth = target.Width; animatedHeight = target.Height; }
        else
        { animatedWidth += (target.Width - animatedWidth) * .22; animatedHeight += (target.Height - animatedHeight) * .22; }
        Details.Width = Math.Max(1, Math.Min(animatedWidth, area.Width / scale));
        Details.Height = Math.Max(1, Math.Min(animatedHeight, area.Height / scale));
        var right = settings.FloatingMode ? Widget.Position.X + (int)Math.Ceiling(Widget.Width * Widget.RenderScaling) : area.Right - 20;
        var bottom = settings.FloatingMode ? Widget.Position.Y + (int)Math.Ceiling(Widget.Height * Widget.RenderScaling) : area.Bottom - 20;
        Details.Position = Ui.Clamp(Details, new PixelPoint(right - (int)Math.Ceiling(Details.Width * scale), bottom - (int)Math.Ceiling(Details.Height * scale)));
    }

    public void OpenDetails()
    {
        Controller.OpenDetails(Now); ApplyPresentation(); Details.Activate();
    }

    public void Collapse()
    {
        Controller.CollapseDetails(Now); ApplyPresentation();
    }

    public void Skip()
    {
        if (Controller.State.Mode != "Active") { Collapse(); return; }
        Change(() => Controller.SkipCurrent(Now));
    }

    private void Pause(int minutes) => Change(() => Controller.SetPaused(Now, minutes));
    private void Resume() => Change(() => Controller.Resume(Now));
    private void ToggleFloating() => Change(() => Controller.SetFloating(Now, !Controller.Settings.FloatingMode));
    private void SetMascot(string id)
    {
        var candidate = Controller.Settings; candidate.Mascot = id;
        Change(() => Controller.ApplySettings(candidate, Now));
    }

    public void SaveWidgetPosition()
    {
        var candidate = Controller.Settings;
        candidate.FloatingX = Widget.Position.X; candidate.FloatingY = Widget.Position.Y;
        Change(() => Controller.ApplySettings(candidate, Now));
    }

    private void Change(Action action)
    {
        if (Exiting) return;
        var previous = Controller.Settings;
        action();
        try
        {
            if (!options.Preview) SettingsStore.Save(settingsPath, Controller.Settings);
            warning = string.Empty;
        }
        catch (Exception error)
        {
            Controller.ApplySettings(previous, Now);
            warning = "设置未保存：" + error.Message;
            Controller.OpenDetails(Now);
        }
        ApplyPresentation(); RefreshMenus();
    }

    public async void OpenSettings()
    {
        if (Exiting) return;
        if (settingsOpen) { settingsWindow?.Activate(); return; }
        OpenDetails(); settingsOpen = true;
        settingsWindow = new SettingsWindow(Controller.Settings, images);
        try
        {
            var candidate = await settingsWindow.ShowDialog<BreakSettings?>(Details);
            if (candidate != null && !Exiting) Change(() => Controller.ApplySettings(candidate, Now));
        }
        finally
        {
            settingsWindow = null; settingsOpen = false;
            if (!Exiting)
            {
                // Saving or cancelling while waiting should restore the
                // compact companion rather than leave a large status card.
                // If a rest period starts during the dialog, normal Tick
                // opens it with the real remaining time after the dialog.
                if (Controller.Settings.FloatingMode && ReminderRules.GetState(Now, Controller.Settings).Mode != "Active")
                    Controller.CollapseDetails(Now);
                Controller.Tick(Now); ApplyPresentation();
            }
        }
    }

    public void ShowPauseMenu()
    {
        if (Controller.State.Mode == "Paused") { Resume(); return; }
        pauseMenu = new ContextMenu { ItemsSource = PauseMenuItems() };
        pauseMenu.Open(Details);
    }

    private MenuItem Item(string text, Action action) => new() { Header = text, Command = new ActionCommand(action) };

    private MenuItem[] PauseMenuItems() =>
    [Item("暂停 30 分钟", () => Pause(30)), Item("暂停 1 小时", () => Pause(60)),
        Item("暂停 2 小时", () => Pause(120)), Item("暂停，直到手动恢复", () => Pause(0))];

    private ContextMenu CreateContextMenu()
    {
        var companionItems = MascotImages.Options.Select(choice =>
            Item((Controller.Settings.Mascot == choice.Id ? "✓ " : "") + choice.Label, () => SetMascot(choice.Id))).ToArray();
        return new ContextMenu { ItemsSource = new object[]
        {
            Item("查看状态", OpenDetails), Item("提醒设置…", OpenSettings), Item("收起详情", Collapse),
            Item((Controller.Settings.FloatingMode ? "✓ " : "") + "常驻小挂件", ToggleFloating),
            new MenuItem { Header = "休息伙伴", ItemsSource = companionItems },
            Item("本轮不再提示", Skip), new MenuItem { Header = "暂停提醒", ItemsSource = PauseMenuItems() },
            Item("恢复提醒", Resume), new Separator(), Item("退出 CornerBreak", () => Exit())
        }};
    }

    private NativeMenu CreateNativeMenu()
    {
        var menu = new NativeMenu();
        NativeMenuItem NativeItem(string text, Action action) => new(text) { Command = new ActionCommand(action) };
        menu.Items.Add(NativeItem("查看状态", OpenDetails)); menu.Items.Add(NativeItem("提醒设置…", OpenSettings));
        menu.Items.Add(NativeItem("收起详情", Collapse));
        menu.Items.Add(NativeItem((Controller.Settings.FloatingMode ? "✓ " : "") + "常驻小挂件", ToggleFloating));
        var companions = new NativeMenu();
        foreach (var choice in MascotImages.Options)
            companions.Items.Add(NativeItem((Controller.Settings.Mascot == choice.Id ? "✓ " : "") + choice.Label, () => SetMascot(choice.Id)));
        menu.Items.Add(new NativeMenuItem("休息伙伴") { Menu = companions });
        menu.Items.Add(NativeItem("本轮不再提示", Skip));
        var pauses = new NativeMenu();
        pauses.Items.Add(NativeItem("暂停 30 分钟", () => Pause(30))); pauses.Items.Add(NativeItem("暂停 1 小时", () => Pause(60)));
        pauses.Items.Add(NativeItem("暂停 2 小时", () => Pause(120))); pauses.Items.Add(NativeItem("暂停，直到手动恢复", () => Pause(0)));
        menu.Items.Add(new NativeMenuItem("暂停提醒") { Menu = pauses }); menu.Items.Add(NativeItem("恢复提醒", Resume));
        menu.Items.Add(new NativeMenuItemSeparator());
        var quit = NativeItem("退出 CornerBreak", () => Exit());
        if (OperatingSystem.IsMacOS()) quit.Gesture = new KeyGesture(Key.Q, KeyModifiers.Meta);
        menu.Items.Add(quit); return menu;
    }

    private void RefreshMenus()
    {
        Widget.ContextMenu = CreateContextMenu(); Details.ContextMenu = CreateContextMenu();
        tray.Menu = CreateNativeMenu();
        var appMenu = new NativeMenu();
        appMenu.Items.Add(new NativeMenuItem("CornerBreak") { Menu = CreateNativeMenu() });
        NativeMenu.SetMenu(Application.Current!, appMenu);
    }

    public void AssertReady()
    {
        if (images.Count != 3) throw new InvalidOperationException("Embedded companion images did not load.");
        if (!Widget.IsVisible && !Details.IsVisible) throw new InvalidOperationException("No reminder surface is visible.");
        if (Widget.Width <= 0 || Widget.Height <= 0 || Details.BaselineSize.Height <= 0)
            throw new InvalidOperationException("Invalid reminder surface geometry.");
    }

    public async Task RenderUiAsync(string directory)
    {
        if (!options.Preview) throw new InvalidOperationException("UI rendering requires preview mode.");
        timer.Stop(); Directory.CreateDirectory(directory);
        Details.Hide(); Widget.Show(); Widget.Update(Controller.Current, Controller.Settings, images);
        await Task.Delay(180); Ui.SavePng(Widget, Path.Combine(directory, "widget.png"));
        Controller.OpenDetails(Now); ApplyPresentation();
        await Task.Delay(180); Ui.SavePng(Details, Path.Combine(directory, "details.png"));
        var editor = new SettingsWindow(Controller.Settings, images); editor.Show(Details);
        await Task.Delay(180); Ui.SavePng(editor, Path.Combine(directory, "settings.png")); editor.Close();
    }

    public void Exit(int code = 0)
    {
        if (Exiting) return;
        Exiting = true; timer.Stop(); tray.IsVisible = false;
        settingsWindow?.Close(); Details.Close(); Widget.Close(); lifetime.Shutdown(code);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = Exiting = true; timer.Stop(); tray.Dispose(); images.Dispose();
    }
}
