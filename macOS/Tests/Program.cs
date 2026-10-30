using CornerBreak;
using CornerBreak.Tests;
using System.Globalization;

static class Program
{
    static int count;
    static void Check(bool value, string message)
    {
        count++;
        if (!value) throw new InvalidOperationException(message);
    }
    static DateTime At(string time) => DateTime.Parse("2026-10-26T" + time, CultureInfo.InvariantCulture);
    static void View(DisplayController controller, string time, BreakView expected, string mode, bool defer = false)
    {
        var current = controller.Tick(At(time), defer);
        Check(current.View == expected, time + " display " + expected);
        Check(current.State.Mode == mode, time + " state " + mode);
    }
    static void TestController()
    {
        var initial = new BreakSettings();
        var c = new DisplayController(initial, At("12:40:00"));
        initial.FloatingMode = false;
        Check(c.Settings.FloatingMode, "constructor isolates source settings");
        var detached = c.Settings;
        detached.Eyes = false;
        Check(c.Settings.Eyes, "settings snapshot cannot mutate controller");
        View(c, "12:49:59", BreakView.Widget, "Waiting");
        View(c, "12:50:00", BreakView.Widget, "Active", true);
        View(c, "12:50:01", BreakView.Details, "Active");
        Check(c.State.SecondsLeft == 599, "deferred popup retains actual time");
        c.CollapseDetails(At("12:51:00"));
        View(c, "12:52:00", BreakView.Widget, "Active");
        Check(c.Settings.SkippedHour == "", "collapse does not skip");
        c.OpenDetails(At("12:52:00"));
        Check(c.View == BreakView.Details, "explicit details open");
        c.SkipCurrent(At("12:52:00"));
        View(c, "12:59:59", BreakView.Widget, "Skipped");
        Check(c.Settings.SkippedHour == "2026-10-26-12", "skip records exact hour");
        c.OpenDetails(At("12:59:59"));
        View(c, "13:00:00", BreakView.Widget, "Waiting");
        c.OpenDetails(At("13:00:00"));
        Check(c.View == BreakView.Details, "explicit click after hour crossing remains open");
        c.CollapseDetails(At("13:00:01"));
        View(c, "13:50:00", BreakView.Details, "Active");
        c.SetPaused(At("13:50:00"), 30);
        View(c, "14:19:59", BreakView.Widget, "Paused");
        View(c, "14:20:00", BreakView.Widget, "Waiting");
        Check(!c.Settings.Paused, "expired pause is cleared in memory");
        View(c, "14:55:00", BreakView.Details, "Active");
        c.SetPaused(At("14:55:00"), 1);
        View(c, "14:56:00", BreakView.Details, "Active");
        Check(c.State.SecondsLeft == 240, "resume never restarts ten-minute period");
        c.SetPaused(At("14:56:00"));
        View(c, "15:50:00", BreakView.Widget, "Paused");
        c.Resume(At("15:55:00"));
        Check(c.View == BreakView.Details && c.State.SecondsLeft == 300, "manual resume at actual schedule");
        c.SetFloating(At("16:00:00"), false);
        View(c, "16:40:00", BreakView.Hidden, "Waiting");
        View(c, "16:50:00", BreakView.Details, "Active");
        c.CollapseDetails(At("16:51:00"));
        View(c, "16:52:00", BreakView.Hidden, "Active");
        c.SetFloating(At("17:00:00"), true);
        View(c, "17:40:00", BreakView.Widget, "Waiting");
        var disabled = c.Settings;
        disabled.Eyes = disabled.Water = disabled.Move = false;
        c.ApplySettings(disabled, At("17:50:00"));
        View(c, "17:50:00", BreakView.Widget, "Disabled");
        var paused = new BreakSettings { Paused = true, PauseUntil = At("12:55:00").ToString("o") };
        var noHiddenClock = new DisplayController(paused);
        Check(noHiddenClock.Settings.Paused, "constructing a preview never expires pause using real time");
        View(noHiddenClock, "12:54:00", BreakView.Widget, "Paused");
        View(noHiddenClock, "12:55:00", BreakView.Details, "Active");
        var reopenedSkip = new DisplayController(new BreakSettings { SkippedHour = "2026-10-26-12" }, At("12:56:00"));
        View(reopenedSkip, "12:59:00", BreakView.Widget, "Skipped");
        View(reopenedSkip, "13:50:00", BreakView.Details, "Active");
    }
    static void TestRecovery()
    {
        var folder = Path.Combine(Path.GetTempPath(), "CornerBreak-mac-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "settings.json");
            SettingsStore.Save(path, new BreakSettings { Mascot = "fish", FloatingX = -80, FloatingY = 90 });
            SettingsStore.Save(path, new BreakSettings { Mascot = "companion" });
            File.WriteAllText(path, "{broken");
            var recovered = SettingsStore.LoadWithRecovery(path, out var warning);
            Check(recovered.Mascot == "fish" && warning.Length > 0, "damaged settings recover prior backup");
            Check(File.ReadAllText(path) == "{broken", "recovery preserves original damaged file");
            var dataPath = AppPaths.GetSettingsPath();
            Check(!dataPath.Contains("CornerBreak.exe", StringComparison.OrdinalIgnoreCase), "Mac host never writes Windows portable data");
        }
        finally { Directory.Delete(folder, true); }
    }
    static int Main()
    {
        try
        {
            int shared = NativeCoreTests.Run();
            Console.WriteLine("PASS: " + shared + " shared Windows/Mac rule and settings assertions.");
            TestController();
            TestRecovery();
            Console.WriteLine("PASS: " + count + " Mac controller and recovery assertions.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
