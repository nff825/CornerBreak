using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CornerBreak.Tests
{
    public static class NativeUiTests
    {
        private static int assertions;
        private static void Check(bool condition, string message)
        { assertions++; if (!condition) throw new Exception("Native UI: " + message); }
        private static DateTime At(string time) { return DateTime.Parse("2026-10-25T" + time, System.Globalization.CultureInfo.InvariantCulture); }
        private static void Tick(MainWindow window, string time)
        {
            window.SetPreviewTime(At(time));
            window.UpdateReminder();
            window.UpdateReminderGrowth(true, true);
        }
        private static void Fits(MainWindow window, string context)
        {
            Rectangle area = Screen.FromControl(window).WorkingArea;
            Check(area.Contains(window.Bounds), context + ": window inside work area");
            Check(TextRenderer.MeasureText(window.TitleLabel.Text, window.TitleLabel.Font).Height <= window.TitleLabel.ClientSize.Height,
                context + ": title glyphs fit at current DPI");
            Check(TextRenderer.MeasureText(window.ClockLabel.Text, window.ClockLabel.Font).Height <= window.ClockLabel.ClientSize.Height,
                context + ": countdown glyphs fit at current DPI");
            foreach (Control child in window.RootLayout.Controls)
                if (child.Visible) Check(child.Left >= 0 && child.Top >= 0 && child.Right <= window.RootLayout.ClientSize.Width && child.Bottom <= window.RootLayout.ClientSize.Height,
                    context + ": root control fits " + child.Name + " " + child.Text);
            foreach (var card in window.Cards.Values)
                if (card.Visible)
                    foreach (Control child in card.Controls)
                        Check(child.Left >= 0 && child.Top >= 0 && child.Right <= card.ClientSize.Width && child.Bottom <= card.ClientSize.Height, context + ": card text fits");
        }
        private static void Selection(MainWindow window, MascotCatalog catalog, string id)
        {
            bool visible = id != "none" && catalog.Images.ContainsKey(id);
            Check(window.CurrentSettings.Mascot == id, "selected setting " + id);
            Check(window.MascotHeader.Visible == visible && window.BrandLabel.Visible != visible, "selected header " + id);
            Check(Object.ReferenceEquals(window.MascotPicture.Image, visible ? catalog.Images[id] : null), "selected bitmap " + id);
            foreach (ToolStripMenuItem item in window.MascotMenuItem.DropDownItems)
                Check(item.Checked == ((string)item.Tag == id), "selected tray item " + item.Tag);
        }
        public static int Run()
        {
            assertions = 0;
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CornerBreak-native-ui-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string settingsPath = Path.Combine(folder, "settings.json");
            const string sentinel = "{\"Version\":1,\"Mascot\":\"fish\",\"Eyes\":false}";
            File.WriteAllText(settingsPath, sentinel);
            try
            {
                using (var catalog = new MascotCatalog())
                {
                    Check(catalog.Images.Count == 3, "three embedded mascots load without external assets");
                    foreach (var image in catalog.Images.Values)
                    {
                        Check(image.Width > 100 && image.Height > 100, "embedded image dimensions");
                        Check(image.GetPixel(0, 0).A == 0, "embedded image alpha retained");
                    }
                    var options = new AppOptions { Preview = true, PreviewAt = At("12:50:00") };
                    using (var window = new MainWindow(options, settingsPath, catalog))
                    {
                        // Existing presentation assertions cover the original
                        // timed-popup mode; floating mode has its own suite.
                        window.CurrentSettings.FloatingMode = false;
                        Tick(window, "12:50:00");
                        Check(window.Visible && window.CurrentState.Mode == "Active", "active auto-show");
                        Check(window.ClockLabel.Text == "10:00", "ten minute countdown");
                        Check(window.CurrentSettings.Eyes && window.CurrentSettings.Mascot == "cat", "preview ignores real settings");
                        Check(window.Cards.Count == 3 && window.Cards.Values.All(c => c.Visible), "three enabled cards");
                        Fits(window, "start");
                        var baseline = window.Bounds;
                        float originalFont = window.TitleLabel.Font.Size;
                        Tick(window, "12:55:00");
                        var middle = window.Bounds;
                        Check(middle.Width > baseline.Width, "grows midway");
                        Check(middle.Right == baseline.Right && middle.Bottom == baseline.Bottom, "bottom right anchor");
                        Tick(window, "12:59:00");
                        var late = window.Bounds;
                        Check(late.Width > middle.Width, "grows more near end");
                        if (late.Height > baseline.Height) Check(window.TitleLabel.Font.Size > originalFont, "text grows with available height");
                        Fits(window, "late");
                        for (int i = 0; i < 8; i++) window.UpdateReminderGrowth(true, true);
                        Check(window.Bounds == late, "growth does not drift");
                        foreach (string id in new[] { "fish", "companion", "none", "cat", "none", "cat" })
                        {
                            var item = window.MascotMenuItem.DropDownItems.Cast<ToolStripMenuItem>().Single(x => (string)x.Tag == id);
                            item.PerformClick();
                            Selection(window, catalog, id);
                            Check(window.ClockLabel.Text == "01:00", "selection preserves countdown");
                            Fits(window, "late " + id);
                        }
                        Check(window.Bounds == late, "switching restores exact size");
                        window.CurrentSettings.AutoExpand = false;
                        window.RefreshSettingsLayout();
                        Check(window.Bounds == baseline && window.TitleLabel.Font.Size == originalFont, "disable restores baseline");
                        window.CurrentSettings.AutoExpand = true;
                        window.UpdateReminderGrowth(false, true);
                        Check(window.Width > baseline.Width && window.Width < late.Width, "smooth intermediate frame");
                        var frozen = window.Bounds;
                        window.DialogOpen = true;
                        window.UpdateReminderGrowth();
                        Check(window.Bounds == frozen, "dialog freezes growth");
                        window.DialogOpen = false;
                        window.GrowthMoving = true;
                        window.UpdateReminderGrowth();
                        Check(window.Bounds == frozen, "drag freezes growth");
                        window.GrowthMoving = false;
                        try
                        {
                            window.MascotMenuItem.DropDown.Show(window, new Point(10, 10));
                            Check(window.MascotMenuItem.DropDown.Visible, "mascot submenu opened");
                            window.UpdateReminderGrowth();
                            Check(window.Bounds == frozen, "submenu freezes growth");
                        }
                        finally { window.MascotMenuItem.DropDown.Close(); }

                        window.SetPaused(30);
                        window.UpdateReminderGrowth(true, true);
                        Check(window.CurrentState.Mode == "Paused" && window.ClockLabel.Text == "30:00", "pause thirty minutes");
                        Check(window.Size == baseline.Size, "pause resets size");
                        window.ResumeReminder();
                        window.UpdateReminderGrowth(true, true);
                        Check(window.CurrentState.Mode == "Active" && window.Bounds == late, "resume current progress");
                        window.HideReminder();
                        Check(!window.Visible && window.CurrentSettings.SkippedHour == "2026-10-25-12", "hide skips current hour");
                        window.UpdateReminder();
                        Check(!window.Visible && window.CurrentState.Mode == "Skipped", "skipped stays hidden");
                        window.ShowStatus();
                        Check(window.Visible && window.CurrentState.Mode == "Skipped", "show skipped status");
                        Tick(window, "13:50:00");
                        Check(window.Visible && window.ClockLabel.Text == "10:00", "next hour begins new reminder");
                        Tick(window, "13:59:00");
                        window.ShowStatus();
                        window.DialogOpen = true;
                        Tick(window, "14:00:00");
                        Check(window.Visible && window.RestAutoHidePending, "defer hour-end hide for settings");
                        window.DialogOpen = false;
                        window.RefreshSettingsLayout();
                        Check(!window.Visible && !window.RestAutoHidePending, "hide after closing settings");

                        Tick(window, "14:50:00");
                        var original = window.CurrentSettings.Clone();
                        using (var editor = new SettingsDialog(window.CurrentSettings, catalog))
                        {
                            editor.Show(window);
                            Application.DoEvents();
                            foreach (Label label in editor.Controls.OfType<Label>())
                                Check(TextRenderer.MeasureText(label.Text, label.Font, new Size(label.Width, Int32.MaxValue), TextFormatFlags.WordBreak).Height <= label.Height,
                                    "settings label text fits at current DPI");
                            for (int index = 0; index < 4; index++)
                            {
                                editor.MascotChoice.SelectedIndex = index;
                                string id = MascotCatalog.Options[index].Id;
                                Check(Object.ReferenceEquals(editor.MascotPreview.Image, id == "none" ? null : catalog.Images[id]), "settings preview " + id);
                                Check(window.CurrentSettings.Mascot == original.Mascot, "preview does not save");
                            }
                            editor.Checks["Eyes"].Checked = false;
                            editor.ExpandCheck.Checked = false;
                        }
                        Check(window.CurrentSettings.Eyes == original.Eyes && window.CurrentSettings.AutoExpand == original.AutoExpand, "cancel leaves settings intact");
                        using (var editor = new SettingsDialog(window.CurrentSettings, catalog))
                        {
                            editor.MascotChoice.SelectedIndex = 1;
                            editor.Checks["Eyes"].Checked = false;
                            editor.Checks["Move"].Checked = false;
                            editor.ExpandCheck.Checked = false;
                            Check(window.SaveSettings(editor.BuildCandidate()), "save settings");
                        }
                        window.RefreshSettingsLayout();
                        Selection(window, catalog, "fish");
                        Check(!window.CurrentSettings.Eyes && window.CurrentSettings.Water && !window.CurrentSettings.Move && !window.CurrentSettings.AutoExpand, "save all controls");
                        Check(!window.Cards["Eyes"].Visible && window.Cards["Water"].Visible && !window.Cards["Move"].Visible, "single card layout");
                        Fits(window, "single card");
                        window.GrowthMoving = true;
                        window.CurrentSettings.Eyes = window.CurrentSettings.Move = true;
                        window.RefreshSettingsLayout();
                        Check(window.Size == baseline.Size, "explicit settings relayout despite animation freeze");
                        window.GrowthMoving = false;
                        Fits(window, "restored cards");
                        int withImageHeight = window.Height;
                        Bitmap missing = catalog.Images["fish"];
                        try
                        {
                            catalog.Images.Remove("fish");
                            window.RefreshSettingsLayout();
                            Selection(window, catalog, "fish");
                            Check(window.Height < withImageHeight && window.CurrentState.Mode == "Active", "missing asset falls back to compact reminder");
                        }
                        finally { catalog.Images["fish"] = missing; window.RefreshSettingsLayout(); }
                        Check(window.Height == withImageHeight, "asset restores layout");
                        window.CurrentSettings.Eyes = window.CurrentSettings.Water = window.CurrentSettings.Move = false;
                        window.RefreshSettingsLayout();
                        window.ShowStatus();
                        Check(window.CurrentState.Mode == "Disabled" && window.ClockLabel.Text == "未开启", "disabled state");
                        Check(window.Cards.Values.All(c => !c.Visible), "disabled cards hidden");
                        window.SetPaused(0);
                        Check(window.CurrentState.Mode == "Disabled", "disabled wins over pause");
                        window.CurrentSettings.Water = true;
                        window.RefreshSettingsLayout();
                        Check(window.CurrentState.Mode == "Paused" && window.ClockLabel.Text == "已暂停", "manual pause");
                    }
                    // Settings and form disposal must leave the shared catalogue
                    // alive until its owner disposes it at process shutdown.
                    foreach (Bitmap image in catalog.Images.Values)
                        using (var clone = (Bitmap)image.Clone()) Check(clone.Width > 100, "shared image lifetime");
                    Check(File.ReadAllText(settingsPath) == sentinel && !File.Exists(settingsPath + ".bak"), "preview never writes user settings");
                    using (var live = new MainWindow(new AppOptions(), settingsPath, catalog))
                    {
                        Check(live.CurrentSettings.Mascot == "fish" && !live.CurrentSettings.Eyes && live.CurrentSettings.Water, "native window loads legacy JSON and defaults");
                        var changed = live.CurrentSettings.Clone();
                        changed.Mascot = "companion";
                        Check(live.SaveSettings(changed), "native window persists to its configured path");
                        Check(SettingsStore.Load(settingsPath).Mascot == "companion", "saved selection survives reload");
                    }
                    File.WriteAllText(settingsPath, "{broken json");
                    using (var fallback = new MainWindow(new AppOptions(), settingsPath, catalog))
                        Check(fallback.CurrentSettings.Mascot == "fish" && !fallback.CurrentSettings.Eyes, "native window recovers previous backup");
                    Check(File.ReadAllText(settingsPath) == "{broken json", "recovery does not overwrite corrupt input");
                }
                TestArguments();
                return assertions;
            }
            finally
            {
                string resolved = Path.GetFullPath(folder);
                string allowedPrefix = Path.Combine(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory), "CornerBreak-native-ui-");
                if (!resolved.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unexpected test cleanup path.");
                Directory.Delete(resolved, true);
            }
        }
        private static void TestArguments()
        {
            var parsed = AppOptions.Parse(new[] { "--preview", "--preview-at", "2026-10-25T12:59:00", "--preview-mascot", "fish", "--render-preview", "x.png" });
            Check(parsed.Preview && parsed.PreviewAt == At("12:59:00") && parsed.PreviewMascot == "fish", "preview arguments");
            var widgetRender = AppOptions.Parse(new[] { "--preview", "--previewscenario", "Waiting", "--render-widget", "widget.png" });
            Check(widgetRender.RenderWidget == "widget.png" && widgetRender.PreviewScenario == "Waiting", "widget render arguments");
            foreach (var invalidRender in new[] { new[] { "--render-widget", "widget.png" },
                new[] { "--preview", "--render-widget", "widget.png", "--render-settings", "settings.png" } })
            {
                bool rejectedRender = false;
                try { AppOptions.Parse(invalidRender); } catch (ArgumentException) { rejectedRender = true; }
                Check(rejectedRender, "widget rendering remains isolated and single-output");
            }
            foreach (var args in new[] { new[] { "--render-preview", "x.png" }, new[] { "--preview", "--preview-mascot", "invalid" }, new[] { "--preview-at" }, new[] { "--exit", "--show" }, new[] { "--unknown" } })
            {
                bool rejected = false;
                try { AppOptions.Parse(args); } catch (ArgumentException) { rejected = true; }
                Check(rejected, "reject invalid arguments " + args[0]);
            }
        }
    }
}
