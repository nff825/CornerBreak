using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CornerBreak.Tests
{
    // Exercise the real forms against a frozen preview clock. These tests do
    // not start timers, read user settings or manufacture desktop input.
    public static class NativeFloatingTests
    {
        private static int assertions;

        private static void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new Exception("Native floating UI: " + message);
        }

        private static DateTime At(string time)
        {
            return DateTime.Parse("2026-10-26T" + time, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void Tick(MainWindow window, string time)
        {
            window.SetPreviewTime(At(time));
            window.UpdateReminder();
            window.UpdateReminderGrowth(true, true);
        }

        private static MainWindow Preview(string path, MascotCatalog catalog, string time)
        {
            return new MainWindow(new AppOptions { Preview = true, PreviewAt = At(time) }, path, catalog);
        }

        private static void Surface(MainWindow window, bool details, bool widget, string context)
        {
            Check(window.Widget != null, context + ": widget exists");
            Check(window.Visible == details, context + ": detail visibility");
            Check(window.Widget.Visible == widget, context + ": widget visibility");
            Check(!(window.Visible && window.Widget.Visible), context + ": only one reminder surface");
        }

        private static void WidgetFits(FloatingWidget widget, string context)
        {
            Check(Screen.AllScreens.Any(screen => screen.WorkingArea.Contains(widget.Bounds)),
                context + ": widget remains within a connected screen working area");
            Check(widget.Width > 0 && widget.Height > 0, context + ": positive widget dimensions");
            foreach (Control child in widget.Controls)
                if (child.Visible)
                    Check(child.Left >= 0 && child.Top >= 0 && child.Right <= widget.ClientSize.Width && child.Bottom <= widget.ClientSize.Height,
                        context + ": child fits " + child.Name);
            foreach (Label label in new[] { widget.StateLabel, widget.CountdownLabel })
                Check(TextRenderer.MeasureText(label.Text, label.Font, new Size(label.Width, Int32.MaxValue), TextFormatFlags.WordBreak).Height <= label.Height,
                    context + ": status text fits " + label.Text);
        }

        private static void TestLifecycle(string settingsPath, MascotCatalog catalog)
        {
            using (var window = Preview(settingsPath, catalog, "12:40:00"))
            {
                Tick(window, "12:40:00");
                Check(window.CurrentSettings.FloatingMode, "floating is the default");
                Surface(window, false, true, "waiting");
                Check(window.Widget.TopMost && !window.Widget.ShowInTaskbar, "widget is topmost without a taskbar entry");
                Check(window.Widget.StateLabel.Text.Contains("12:50"), "waiting exposes next reminder time");
                Check(window.Widget.CountdownLabel.Text.Contains("10:00"), "waiting uses the actual countdown");
                WidgetFits(window.Widget, "waiting");
                Size compactSize = window.Widget.Size;

                Tick(window, "12:50:00");
                Surface(window, true, false, "rest begins");
                Check(window.CurrentState.Mode == "Active" && window.ClockLabel.Text == "10:00", "rest starts on the wall-clock boundary");
                window.CollapseDetails();
                Surface(window, false, true, "collapse active details");
                Check(window.CurrentSettings.SkippedHour == String.Empty && window.CurrentState.Mode == "Active",
                    "collapse does not write a skip");
                Tick(window, "12:51:00");
                Surface(window, false, true, "collapsed active tick");
                Check(window.Widget.CountdownLabel.Text.Contains("09:00"), "collapsed rest still counts down");
                Check(window.Widget.Size == compactSize, "growth never enlarges the compact widget");

                window.ShowStatus();
                Surface(window, true, false, "manually reopen details");
                Check(window.CurrentState.Mode == "Active" && window.ClockLabel.Text == "09:00", "manual open keeps the real rest time");
                window.Close();
                Check(!window.IsDisposed, "real detail close is cancelled in floating mode");
                Surface(window, false, true, "real detail close returns to widget");
                Check(window.CurrentSettings.SkippedHour == String.Empty && window.CurrentState.Mode == "Active",
                    "real detail close does not skip the rest period");
                Tick(window, "12:52:00");
                Surface(window, false, true, "closed details stay compact for this hour");
                window.ShowStatus();
                window.HideReminder();
                Surface(window, false, true, "skip rest");
                Check(window.CurrentSettings.SkippedHour == "2026-10-26-12" && window.CurrentState.Mode == "Skipped",
                    "skip stores only the current hour");
                Tick(window, "12:59:00");
                Surface(window, false, true, "skipped hour remains compact");
                Check(window.Widget.StateLabel.Text.Contains("跳过"), "widget identifies skipped rest");
                Check(window.Widget.CountdownLabel.Text.Contains("13:50"), "skip shows next actual reminder");
                window.ShowStatus();
                Check(window.CurrentState.Mode == "Skipped", "manual open does not undo skip");
                window.CollapseDetails();
                Tick(window, "13:00:00");
                Surface(window, false, true, "hour end");
                Check(window.CurrentState.Mode == "Waiting", "skip stops applying after this hour");
                Tick(window, "13:50:00");
                Surface(window, true, false, "next rest");
                Check(window.CurrentState.Mode == "Active" && window.ClockLabel.Text == "10:00", "new hour can remind again");
                Tick(window, "14:00:00");
                Surface(window, false, true, "next rest ends");
                Check(window.Widget.StateLabel.Text.Contains("14:50"), "end shows next rest");
            }
        }

        private static void TestPauseAndDisabled(string settingsPath, MascotCatalog catalog)
        {
            using (var window = Preview(settingsPath, catalog, "12:55:00"))
            {
                Tick(window, "12:55:00");
                window.SetPaused(30);
                Surface(window, false, true, "timed pause");
                Check(window.CurrentState.Mode == "Paused" && window.Widget.StateLabel.Text.Contains("暂停"), "pause is visible in widget");
                Check(window.Widget.CountdownLabel.Text.Contains("30:00"), "pause countdown shown");
                Tick(window, "13:25:00");
                Surface(window, false, true, "pause expires outside rest");
                Check(window.CurrentState.Mode == "Waiting" && !window.CurrentSettings.Paused, "expired pause clears and follows schedule");
                Tick(window, "13:50:00");
                Surface(window, true, false, "rest after expired pause");

                window.SetPaused(0);
                Surface(window, false, true, "manual pause");
                Check(window.Widget.StateLabel.Text.Contains("暂停") && window.Widget.CountdownLabel.Text.Contains("手动"),
                    "indefinite pause has a clear manual-resume status");
                window.ResumeReminder();
                Surface(window, true, false, "resume within rest");
                Check(window.ClockLabel.Text == "10:00", "resume uses remaining rest time");

                window.SetPaused(30);
                Tick(window, "14:20:00");
                Surface(window, false, true, "pause expires while waiting");
                Tick(window, "14:55:00");
                window.SetPaused(1);
                Tick(window, "14:56:00");
                Surface(window, true, false, "pause expires inside rest");
                Check(window.ClockLabel.Text == "04:00", "timed resume never restarts ten minutes");

                window.CurrentSettings.Eyes = window.CurrentSettings.Water = window.CurrentSettings.Move = false;
                window.RefreshSettingsLayout();
                window.CollapseDetails();
                Surface(window, false, true, "all reminders disabled");
                Check(window.CurrentState.Mode == "Disabled" && window.Widget.StateLabel.Text.Contains("关闭"),
                    "disabled widget explains no reminders are enabled");
                Tick(window, "15:50:00");
                Surface(window, false, true, "disabled at a scheduled boundary");
            }
        }

        private static void TestMenuAtRestBoundary(string settingsPath, MascotCatalog catalog)
        {
            using (var window = Preview(settingsPath, catalog, "12:49:59"))
            {
                Tick(window, "12:49:59");
                Surface(window, false, true, "widget before rest boundary");
                ContextMenuStrip contextMenu = window.Widget.ContextMenuStrip;
                Check(contextMenu != null, "widget exposes its real context menu");
                try
                {
                    contextMenu.Show(window.Widget, new Point(8, 8));
                    Check(contextMenu.Visible, "widget context menu is open");
                    Tick(window, "12:50:00");
                    Surface(window, false, true, "menu defers detail popup at rest boundary");
                    Check(window.CurrentState.Mode == "Active", "schedule proceeds while context menu stays open");
                }
                finally { contextMenu.Close(); }
                Check(!contextMenu.Visible, "widget context menu closes");
                Tick(window, "12:50:01");
                Surface(window, true, false, "details appear after menu interaction ends");
                Check(window.CurrentState.SecondsLeft == 599 && window.ClockLabel.Text == "09:59",
                    "deferred popup keeps the actual countdown instead of restarting ten minutes");
            }
        }

        private static void TestModeAndSettings(string settingsPath, MascotCatalog catalog)
        {
            using (var window = Preview(settingsPath, catalog, "12:40:00"))
            {
                Tick(window, "12:40:00");
                window.SetFloatingMode(false);
                window.CollapseDetails();
                Tick(window, "12:41:00");
                Surface(window, false, false, "scheduled mode while waiting");
                Check(!window.CurrentSettings.FloatingMode, "mode toggle updates settings");
                Tick(window, "12:50:00");
                Surface(window, true, false, "scheduled mode still reminds");
                Tick(window, "13:00:00");
                Surface(window, false, false, "scheduled mode hides at hour end");
                window.SetFloatingMode(true);
                Surface(window, false, true, "enable floating while waiting");

                var original = new BreakSettings { FloatingMode = true, FloatingX = -320, FloatingY = 120 };
                using (var editor = new SettingsDialog(original, catalog))
                {
                    Check(editor.FloatingCheck.Checked, "settings checkbox reflects current mode");
                    editor.FloatingCheck.Checked = false;
                    Check(original.FloatingMode, "editing checkbox does not mutate original settings");
                    BreakSettings candidate = editor.BuildCandidate();
                    Check(!candidate.FloatingMode && candidate.FloatingX == -320 && candidate.FloatingY == 120,
                        "saved candidate changes only mode and retains position");
                }
                Check(original.FloatingMode && original.FloatingX == -320 && original.FloatingY == 120,
                    "discarding settings leaves mode and position intact");
            }
        }

        private static void TestWidgetRendering(string settingsPath, MascotCatalog catalog)
        {
            using (var window = Preview(settingsPath, catalog, "12:40:00"))
            {
                Tick(window, "12:40:00");
                foreach (string mascot in new[] { "cat", "fish", "companion", "none", "fish" })
                {
                    window.SetMascot(mascot);
                    Check(window.CurrentSettings.Mascot == mascot, "widget mascot selection " + mascot);
                    Check(Object.ReferenceEquals(window.Widget.MascotPicture.Image, mascot == "none" ? null : catalog.Images[mascot]),
                        "widget shares selected mascot " + mascot);
                    Check(window.Widget.StateLabel.Text.Length > 0 && window.Widget.CountdownLabel.Text.Length > 0,
                        "status survives role selection " + mascot);
                    WidgetFits(window.Widget, "mascot " + mascot);
                }

                Bitmap fish = catalog.Images["fish"];
                try
                {
                    catalog.Images.Remove("fish");
                    window.RefreshSettingsLayout();
                    Check(window.Widget.MascotPicture.Image == null && window.Widget.StateLabel.Text.Length > 0,
                        "missing mascot gracefully keeps text status");
                }
                finally
                {
                    catalog.Images["fish"] = fish;
                    window.RefreshSettingsLayout();
                }
                Check(Object.ReferenceEquals(window.Widget.MascotPicture.Image, fish), "mascot reappears when available");

                foreach (Point point in new[] { new Point(-1000000, -1000000), new Point(1000000, 1000000),
                    new Point(-250, 80), new Point(0, 0) })
                {
                    window.Widget.Place(point.X, point.Y);
                    WidgetFits(window.Widget, "position clamped " + point);
                }
                window.Widget.Place(null, null);
                WidgetFits(window.Widget, "default position");
                window.Widget.Location = new Point(-1000000, -1000000);
                window.Widget.ClampToScreen();
                WidgetFits(window.Widget, "disconnected or unavailable screen position");
            }

            foreach (Bitmap image in catalog.Images.Values)
                using (var clone = (Bitmap)image.Clone())
                    Check(clone.Width > 100, "disposing widget leaves shared mascot alive");
        }

        private static void TestPositionPersistence(string folder, MascotCatalog catalog)
        {
            string settingsPath = Path.Combine(folder, "position-persistence", "settings.json");
            Point savedLocation;
            using (var window = new MainWindow(new AppOptions(), settingsPath, catalog))
            {
                // A moved or disconnected screen can leave historical coordinates
                // far off screen. Save the actual clamped location, then reload it.
                window.Widget.Place(-1000000, -1000000);
                savedLocation = window.Widget.Location;
                Check(window.SaveWidgetPosition(), "widget position persists to the dedicated settings path");
                BreakSettings saved = SettingsStore.Load(settingsPath);
                Check(saved.FloatingX == savedLocation.X && saved.FloatingY == savedLocation.Y,
                    "saved coordinates reflect the visible clamped position");
                window.SetFloatingMode(false);
                Check(!SettingsStore.Load(settingsPath).FloatingMode, "scheduled display mode is persisted");
            }
            using (var reopened = new MainWindow(new AppOptions(), settingsPath, catalog))
            {
                Check(!reopened.CurrentSettings.FloatingMode, "scheduled display mode survives reopening");
                Check(reopened.CurrentSettings.FloatingX == savedLocation.X && reopened.CurrentSettings.FloatingY == savedLocation.Y,
                    "widget coordinates survive reopening");
                Check(reopened.Widget.Location == savedLocation, "reopened widget applies saved visible coordinates");
                Check(Screen.AllScreens.Any(screen => screen.WorkingArea.Contains(reopened.Widget.Bounds)),
                    "reopened widget is fully on screen");
                string skippedBeforeClose = reopened.CurrentSettings.SkippedHour;
                reopened.ShowStatus();
                reopened.Close();
                Check(!reopened.IsDisposed && !reopened.Visible && !reopened.Widget.Visible,
                    "closing timed-mode details only hides the presentation");
                Check(reopened.CurrentSettings.SkippedHour == skippedBeforeClose,
                    "closing timed-mode details does not persist a skip");
            }
        }

        private static void TestClampBounds()
        {
            Rectangle negativeScreen = new Rectangle(-1920, 0, 1920, 1040);
            Rectangle result = FloatingWidget.ClampBounds(new Rectangle(-1880, 70, 260, 100), negativeScreen);
            Check(result == new Rectangle(-1880, 70, 260, 100), "negative-coordinate monitor retains an in-bounds position");
            result = FloatingWidget.ClampBounds(new Rectangle(-1000000, 1000000, 260, 100), negativeScreen);
            Check(negativeScreen.Contains(result) && result.Size == new Size(260, 100),
                "offscreen bounds are clamped without changing a fitting size");

            Rectangle smallArea = new Rectangle(20, 30, 120, 60);
            result = FloatingWidget.ClampBounds(new Rectangle(-200, -200, 390, 150), smallArea);
            Check(result == smallArea, "widget larger than a small work area is constrained to that area");
            Rectangle tinyArea = new Rectangle(-10, -20, 1, 1);
            result = FloatingWidget.ClampBounds(new Rectangle(100, 100, 260, 100), tinyArea);
            Check(result == tinyArea, "one-pixel work area keeps finite positive bounds");

            bool rejected = false;
            try { FloatingWidget.ClampBounds(new Rectangle(0, 0, 260, 100), new Rectangle(0, 0, 0, 60)); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "empty work area is rejected explicitly");
        }

        public static int Run()
        {
            assertions = 0;
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CornerBreak-native-floating-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string settingsPath = Path.Combine(folder, "settings.json");
            const string sentinel = "{\"Version\":1,\"Mascot\":\"fish\",\"FloatingMode\":false,\"FloatingX\":42,\"FloatingY\":64}";
            File.WriteAllText(settingsPath, sentinel);
            try
            {
                using (var catalog = new MascotCatalog())
                {
                    TestLifecycle(settingsPath, catalog);
                    TestPauseAndDisabled(settingsPath, catalog);
                    TestMenuAtRestBoundary(settingsPath, catalog);
                    TestModeAndSettings(settingsPath, catalog);
                    TestWidgetRendering(settingsPath, catalog);
                    TestPositionPersistence(folder, catalog);
                    TestClampBounds();
                }
                Check(File.ReadAllText(settingsPath) == sentinel && !File.Exists(settingsPath + ".bak"),
                    "all preview mode operations preserve on-disk user settings");
                return assertions;
            }
            finally
            {
                string resolved = Path.GetFullPath(folder);
                string allowedPrefix = Path.Combine(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory), "CornerBreak-native-floating-");
                if (!resolved.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Unexpected floating test cleanup path.");
                Directory.Delete(resolved, true);
            }
        }
    }
}

