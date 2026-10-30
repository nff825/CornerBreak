using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace CornerBreak.MacDesktop;

public sealed class SettingsWindow : Window
{
    private readonly BreakSettings original;
    private readonly CheckBox eyes, water, move, expand, floating;
    private readonly ComboBox mascotChoice;
    public SettingsWindow(BreakSettings settings, MascotImages images)
    {
        original = settings.Clone();
        Title = "CornerBreak · 提醒设置"; Width = 420; Height = 575;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false; ShowInTaskbar = false; Background = Ui.Background;
        var root = new StackPanel { Margin = new Thickness(22), Spacing = 13 };
        root.Children.Add(Ui.Text("选择想收到的提醒", 22, true));
        var preview = new Image { Width = 108, Height = 78, Stretch = Stretch.Uniform, Source = images.Get(settings.Mascot) };
        mascotChoice = new ComboBox { ItemsSource = MascotImages.Options.Select(item => item.Label).ToArray(),
            SelectedIndex = Math.Max(0, Array.FindIndex(MascotImages.Options, item => item.Id == settings.Mascot)), MinWidth = 220 };
        mascotChoice.SelectionChanged += (_, _) => preview.Source = images.Get(MascotImages.Options[Math.Max(0, mascotChoice.SelectedIndex)].Id);
        var choiceRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,108"), ColumnSpacing = 12 };
        var choiceColumn = new StackPanel { Spacing = 8 }; choiceColumn.Children.Add(Ui.Text("休息伙伴")); choiceColumn.Children.Add(mascotChoice);
        choiceRow.Children.Add(choiceColumn); Grid.SetColumn(preview, 1); choiceRow.Children.Add(preview); root.Children.Add(choiceRow);
        CheckBox Check(string text, bool value)
        {
            var box = new CheckBox { Content = text, IsChecked = value, MinHeight = 30 }; root.Children.Add(box); return box;
        }
        eyes = Check("眼睛休息 — 离开屏幕，看看远处", settings.Eyes);
        water = Check("喝水提醒 — 工作间隙喝点水", settings.Water);
        move = Check("起身活动 — 离开工位，走动一下", settings.Move);
        expand = Check("随倒计时逐渐放大", settings.AutoExpand);
        floating = Check("常驻小挂件（平时显示下次休息）", settings.FloatingMode);
        var hint = Ui.Text("共用每小时 50 分至整点的提醒时段。\n关闭详情只收起，跳过本轮请用“本轮不再提示”。\n全部项目关闭后不自动提醒。", 12);
        hint.Foreground = Ui.Muted; root.Children.Add(hint);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10 };
        buttons.Children.Add(Ui.Button("取消", () => Close(null)));
        buttons.Children.Add(Ui.Button("保存", () => Close(BuildCandidate()), true)); root.Children.Add(buttons);
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
    }

    public BreakSettings BuildCandidate()
    {
        var candidate = original.Clone();
        candidate.Eyes = eyes.IsChecked == true; candidate.Water = water.IsChecked == true; candidate.Move = move.IsChecked == true;
        candidate.AutoExpand = expand.IsChecked == true; candidate.FloatingMode = floating.IsChecked == true;
        candidate.Mascot = MascotImages.Options[Math.Max(0, mascotChoice.SelectedIndex)].Id;
        SettingsStore.Validate(candidate);
        return candidate;
    }
}
