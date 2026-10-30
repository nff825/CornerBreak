using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace CornerBreak.Tests
{
    public static class NativeCoreTests
    {
        private static int assertions;

        public static int Run()
        {
            assertions = 0;
            TestSchedule();
            TestPauseAndSkip();
            TestGrowth();
            TestSettingsValidation();
            TestFloatingSettings();
            TestPersistence();
            return assertions;
        }

        private static DateTime Time(string value)
        {
            return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        private static void Equal<T>(T actual, T expected, string message)
        {
            if (!Object.Equals(actual, expected))
                throw new InvalidOperationException(message + "; expected [" + expected + "], actual [" + actual + "]");
            assertions++;
        }

        private static void Near(double actual, double expected, string message)
        {
            if (Double.IsNaN(actual) || Double.IsInfinity(actual) || Math.Abs(actual - expected) > 0.0000001)
                throw new InvalidOperationException(message + "; expected [" + expected + "], actual [" + actual + "]");
            assertions++;
        }

        private static void Throws(Action action, string contains, string message)
        {
            Exception caught = null;
            try { action(); }
            catch (Exception error) { caught = error; }
            if (caught == null || (contains.Length > 0 && caught.Message.IndexOf(contains, StringComparison.OrdinalIgnoreCase) < 0))
                throw new InvalidOperationException(message + "; expected exception containing [" + contains + "]", caught);
            assertions++;
        }

        private static void TestSchedule()
        {
            string[] times = { "2026-10-25T12:49:59", "2026-10-25T12:49:59.900", "2026-10-25T12:50:00",
                "2026-10-25T12:57:00", "2026-10-25T12:59:59", "2026-10-25T13:00:00",
                "2026-10-25T23:59:59", "2026-10-26T00:00:00" };
            string[] modes = { "Waiting", "Waiting", "Active", "Active", "Active", "Waiting", "Active", "Waiting" };
            int[] seconds = { 1, 1, 600, 180, 1, 3000, 1, 3000 };
            string[] targets = { "2026-10-25T12:50:00", "2026-10-25T12:50:00", "2026-10-25T13:00:00",
                "2026-10-25T13:00:00", "2026-10-25T13:00:00", "2026-10-25T13:50:00",
                "2026-10-26T00:00:00", "2026-10-26T00:50:00" };
            for (int i = 0; i < times.Length; i++)
            {
                ReminderState state = ReminderRules.GetState(Time(times[i]), new BreakSettings());
                Equal(state.Mode, modes[i], "schedule mode " + times[i]);
                Equal(state.SecondsLeft, seconds[i], "schedule seconds " + times[i]);
                Equal(state.Target, (DateTime?)Time(targets[i]), "schedule target " + times[i]);
            }
            Equal(ReminderRules.GetState(Time("2026-10-26T00:00:00"), new BreakSettings()).HourKey, "2026-10-26-00", "midnight hour key");
            Equal(ReminderRules.FormatCountdown(180), "03:00", "late start remaining time");
            Equal(ReminderRules.FormatCountdown(1), "00:01", "seconds zero padding");
            Equal(ReminderRules.FormatCountdown(-1), "00:00", "negative seconds");
            Equal(ReminderRules.FormatCountdown(3600), "60:00", "hour-long pause");
        }

        private static void TestPauseAndSkip()
        {
            var settings = new BreakSettings();
            Equal(settings.Version, 1, "default version");
            Equal(settings.AutoExpand, true, "default growth");
            Equal(settings.Mascot, "cat", "default companion");
            settings.SkippedHour = "2026-10-25-12";
            Equal(ReminderRules.GetState(Time("2026-10-25T12:57:00"), settings).Mode, "Skipped", "skip this hour");
            Equal(ReminderRules.GetState(Time("2026-10-25T13:00:00"), settings).Mode, "Waiting", "skip expires on hour boundary");
            Equal(ReminderRules.GetState(Time("2026-10-25T14:57:00"), settings).Mode, "Active", "wake across hours");
            Equal(ReminderRules.GetState(Time("2026-10-25T14:57:00"), settings).SecondsLeft, 180, "wake retains current time");
            Equal(ReminderRules.GetState(Time("2026-10-26T12:57:00"), settings).Mode, "Active", "skip excludes subsequent dates");

            settings.Paused = true;
            ReminderState state = ReminderRules.GetState(Time("2026-10-25T12:57:00"), settings);
            Equal(state.Mode, "Paused", "pause precedes skip");
            Equal(state.Target, (DateTime?)null, "manual pause no target");
            Equal(state.SecondsLeft, 0, "manual pause no countdown");
            settings.PauseUntil = "2026-10-25T13:52:00";
            state = ReminderRules.GetState(Time("2026-10-25T13:51:59.900"), settings);
            Equal(state.Mode, "Paused", "timed pause before expiry");
            Equal(state.SecondsLeft, 1, "pause rounds up fractional seconds");
            Equal(state.PauseUntil, (DateTime?)Time(settings.PauseUntil), "pause deadline exposed");
            state = ReminderRules.GetState(Time("2026-10-25T13:52:00"), settings);
            Equal(state.Mode, "Active", "timed pause immediate resume");
            Equal(state.SecondsLeft, 480, "resume real remaining time");
            Equal(state.PauseExpired, true, "expiry exposed");
            Equal(settings.Paused, true, "state is pure");
            settings.PauseUntil = "2026-10-25T13:30:00";
            Equal(ReminderRules.GetState(Time("2026-10-25T13:30:00"), settings).Mode, "Waiting", "pause expiry outside rest");
            settings.Paused = false;
            settings.SkippedHour = String.Empty;
            Equal(ReminderRules.GetState(Time("2026-10-25T12:57:00"), settings).Mode, "Active", "ignore old pause deadline when resumed");
            settings.Paused = true;
            settings.PauseUntil = String.Empty;
            settings.Eyes = settings.Water = settings.Move = false;
            state = ReminderRules.GetState(Time("2026-10-25T12:57:00"), settings);
            Equal(state.Mode, "Disabled", "all off precedes pause");
            Equal(state.PauseExpired, false, "manual pause does not expire while disabled");
            Equal(state.PauseUntil, (DateTime?)null, "manual pause no deadline");
            settings.PauseUntil = "2026-10-25T13:52:00";
            state = ReminderRules.GetState(Time("2026-10-25T13:51:59"), settings);
            Equal(state.Mode, "Disabled", "disabled precedes unexpired pause");
            Equal(state.PauseExpired, false, "disabled before deadline");
            foreach (string at in new[] { "2026-10-25T13:52:00", "2026-10-25T14:00:00" })
            {
                state = ReminderRules.GetState(Time(at), settings);
                Equal(state.Mode, "Disabled", "disabled stays disabled after deadline");
                Equal(state.PauseExpired, true, "disabled exposes pause expiry");
                Equal(state.PauseUntil, (DateTime?)Time(settings.PauseUntil), "disabled exposes expired deadline");
            }
            settings.Paused = false;
            state = ReminderRules.GetState(Time("2026-10-25T14:00:00"), settings);
            Equal(state.PauseExpired, false, "resumed no stale expiry");
            Equal(state.PauseUntil, (DateTime?)null, "resumed no stale deadline");
            for (int i = 0; i < 3; i++)
            {
                settings.Eyes = i == 0; settings.Water = i == 1; settings.Move = i == 2;
                Equal(ReminderRules.GetState(Time("2026-10-25T12:57:00"), settings).Mode, "Active", "one reminder enabled " + i);
            }
            settings = new BreakSettings { Paused = true, PauseUntil = "2026-10-25T13:52:00Z" };
            DateTime utcNow = Time("2026-10-25T13:51:00Z");
            Equal(ReminderRules.GetState(utcNow.ToLocalTime(), settings).SecondsLeft, 60, "UTC/local pause comparison");
            settings.PauseUntil = "2026-10-25T21:52:00+08:00";
            Equal(ReminderRules.GetState(utcNow.ToLocalTime(), settings).SecondsLeft, 60, "offset/local pause comparison");
        }

        private static void TestGrowth()
        {
            var settings = new BreakSettings();
            string[] times = { "2026-10-25T12:49:59", "2026-10-25T12:50:00", "2026-10-25T12:55:00",
                "2026-10-25T12:57:00", "2026-10-25T12:59:00", "2026-10-25T13:00:00" };
            double[] fractions = { 0, 0, .5, .7, .9, 0 };
            for (int i = 0; i < times.Length; i++)
            {
                GrowthTarget result = ReminderRules.GetGrowth(Time(times[i]), settings, 400, 300, 1920, 1080);
                double progress = fractions[i] * fractions[i];
                Near(result.ElapsedFraction, fractions[i], "growth elapsed " + times[i]);
                Near(result.Progress, progress, "growth curve " + times[i]);
                Near(result.Width, 400 + 560 * progress, "growth width " + times[i]);
                Near(result.Height, 300 + 564 * progress, "growth height " + times[i]);
            }
            GrowthTarget precise = ReminderRules.GetGrowth(Time("2026-10-25T12:55:00.500"), settings, 400, 300, 1920, 1080);
            Near(precise.ElapsedFraction, 300.5 / 600, "sub-second elapsed");
            Near(precise.Progress, Math.Pow(300.5 / 600, 2), "growth independent of rounded countdown");
            GrowthTarget late = ReminderRules.GetGrowth(Time("2026-10-25T12:59:59.999"), settings, 400, 300, 1920, 1080);
            Equal(late.Width < 960 && late.Width > 959.9, true, "width limit");
            Equal(late.Height < 864 && late.Height > 863.9, true, "height limit");
            Near(ReminderRules.GetGrowth(Time("2026-10-25T14:57:00"), settings, 400, 300, 1920, 1080).Progress, .49, "wake growth uses current hour");
            for (int mode = 0; mode < 4; mode++)
            {
                var inactive = new BreakSettings();
                if (mode == 0) inactive.Paused = true;
                if (mode == 1) inactive.SkippedHour = "2026-10-25-12";
                if (mode == 2) inactive.Eyes = inactive.Water = inactive.Move = false;
                if (mode == 3) inactive.AutoExpand = false;
                GrowthTarget result = ReminderRules.GetGrowth(Time("2026-10-25T12:59:00"), inactive, 400, 300, 1920, 1080);
                Near(result.Progress, 0, "inactive growth " + mode);
                Near(result.Width, 400, "inactive base width " + mode);
                Near(result.Height, 300, "inactive base height " + mode);
            }
            var resumed = new BreakSettings { Paused = true, PauseUntil = "2026-10-25T12:55:00" };
            Near(ReminderRules.GetGrowth(Time("2026-10-25T12:57:00"), resumed, 400, 300, 1920, 1080).Progress, .49, "resume actual progress");
            Equal(resumed.Paused, true, "growth is pure");
            GrowthTarget small = ReminderRules.GetGrowth(Time("2026-10-25T12:59:00"), settings, 400, 300, 320, 240);
            Near(small.Width, 280, "small screen width");
            Near(small.Height, 200, "small screen height");
            GrowthTarget large = ReminderRules.GetGrowth(Time("2026-10-25T12:59:00"), settings, 1100, 900, 1920, 1080);
            Near(large.Width, 1100, "base exceeds half width");
            Near(large.Height, 900, "base exceeds 80 percent height");
            foreach (double size in new[] { .5, 1, 20, 40, 80 })
                foreach (string at in new[] { "2026-10-25T12:49:00", "2026-10-25T12:59:59.999" })
                {
                    GrowthTarget tiny = ReminderRules.GetGrowth(Time(at), settings, 400, 300, size, size);
                    Equal(tiny.Width > 0 && tiny.Width <= size, true, "tiny screen width " + size);
                    Equal(tiny.Height > 0 && tiny.Height <= size, true, "tiny screen height " + size);
                }
            for (int index = 0; index < 4; index++)
                foreach (double invalid in new[] { 0, -1, Double.NaN, Double.PositiveInfinity, Double.NegativeInfinity })
                {
                    var dimensions = new[] { 400.0, 300.0, 1920.0, 1080.0 };
                    dimensions[index] = invalid;
                    Throws(delegate { ReminderRules.GetGrowth(Time("2026-10-25T12:59:00"), settings,
                        dimensions[0], dimensions[1], dimensions[2], dimensions[3]); }, String.Empty, "reject invalid dimension " + index);
                }
        }

        private static void TestSettingsValidation()
        {
            // 实际旧版 PowerShell 设置的格式；测试不读取用户的 Data 目录。
            const string oldJson = "{\r\n    \"Version\":  1,\r\n    \"Eyes\":  false,\r\n    \"Water\":  true,\r\n    \"Move\":  true,\r\n    \"Paused\":  false,\r\n    \"PauseUntil\":  \"\",\r\n    \"SkippedHour\":  \"2026-10-25-12\"\r\n}";
            BreakSettings old = SettingsStore.Parse(oldJson);
            Equal(old.Eyes, false, "legacy eyes preserved");
            Equal(old.AutoExpand, true, "legacy missing growth default");
            Equal(old.Mascot, "cat", "legacy missing mascot default");
            Equal(old.SkippedHour, "2026-10-25-12", "legacy skipped hour preserved");
            BreakSettings partial = SettingsStore.Parse("{\"Eyes\":false,\"FutureOption\":123}");
            Equal(partial.Water, true, "missing boolean defaults");
            Equal(partial.Version, 1, "missing version defaults");
            Equal(partial.PauseUntil, String.Empty, "missing pause deadline defaults");
            Equal(SettingsStore.Parse("{\"eyes\":false}").Eyes, false, "legacy property lookup case-insensitive");
            BreakSettings clone = old.Clone();
            clone.Mascot = "fish"; clone.Eyes = true;
            Equal(old.Mascot, "cat", "clone mascot independent");
            Equal(old.Eyes, false, "clone boolean independent");
            foreach (string mascot in new[] { "cat", "fish", "companion", "none" })
                Equal(SettingsStore.Parse("{\"Mascot\":\"" + mascot + "\"}").Mascot, mascot, "valid mascot " + mascot);
            string[] invalidJson = { "{oops", "", "null", "[{}]", "[]", "1", "\"cat\"", "true" };
            foreach (string json in invalidJson)
                Throws(delegate { SettingsStore.Parse(json); }, String.Empty, "reject nonobject or invalid JSON " + json);
            foreach (string field in new[] { "Eyes", "Water", "Move", "AutoExpand", "FloatingMode", "Paused" })
                foreach (string wrongType in new[] { "\"false\"", "1", "null", "[]", "{}" })
                {
                    string json = "{\"" + field + "\":" + wrongType + "}";
                    Throws(delegate { SettingsStore.Parse(json); }, field, "strict boolean " + json);
                }
            foreach (string value in new[] { "2", "\"1\"", "1.0", "null", "true", "[]" })
            {
                string json = "{\"Version\":" + value + "}";
                Throws(delegate { SettingsStore.Parse(json); }, "Version", "strict version " + json);
            }
            foreach (string value in new[] { "\"dragon\"", "\"Cat\"", "\" cat\"", "\"\"", "true", "false", "null", "1", "[\"cat\"]", "{}" })
            {
                string json = "{\"Mascot\":" + value + "}";
                Throws(delegate { SettingsStore.Parse(json); }, "Mascot", "strict mascot " + json);
            }
            foreach (string value in new[] { "\"not-a-date\"", "null", "false", "1", "[]" })
            {
                string json = "{\"PauseUntil\":" + value + "}";
                Throws(delegate { SettingsStore.Parse(json); }, "PauseUntil", "strict pause date " + json);
            }
            foreach (string value in new[] { "\"2026-02-30-12\"", "\"2026-10-25-24\"", "null", "false", "[]" })
            {
                string json = "{\"SkippedHour\":" + value + "}";
                Throws(delegate { SettingsStore.Parse(json); }, "SkippedHour", "strict skip date " + json);
            }
            Throws(delegate { SettingsStore.Validate(null); }, "settings", "null settings rejected");
        }

        private static void TestFloatingSettings()
        {
            var defaults = new BreakSettings();
            Equal(defaults.FloatingMode, true, "floating mode default");
            Equal(defaults.FloatingX, (int?)null, "floating default X unpositioned");
            Equal(defaults.FloatingY, (int?)null, "floating default Y unpositioned");
            var legacy = SettingsStore.Parse("{\"Version\":1,\"Eyes\":false,\"Mascot\":\"fish\",\"Paused\":true,\"SkippedHour\":\"2026-10-25-12\"}");
            Equal(legacy.FloatingMode, true, "legacy settings gain default floating mode");
            Equal(legacy.FloatingX, (int?)null, "legacy X remains unpositioned");
            Equal(legacy.FloatingY, (int?)null, "legacy Y remains unpositioned");
            Equal(legacy.Version, 1, "floating migration keeps settings schema version");
            Equal(legacy.Eyes, false, "floating migration retains reminder choices");
            Equal(legacy.Paused, true, "floating migration retains pause");
            Equal(legacy.SkippedHour, "2026-10-25-12", "floating migration retains skip");
            Equal(SettingsStore.Parse("{\"FloatingMode\":false}").FloatingMode, false, "timed popup mode accepted");
            Equal(SettingsStore.Parse("{\"floatingmode\":false,\"floatingx\":-1900,\"floatingy\":72}").FloatingX,
                (int?)-1900, "floating fields case-insensitive");
            foreach (int coordinate in new[] { -1000000, -1920, -1, 0, 1920, 1000000 })
            {
                string json = "{\"FloatingX\":" + coordinate.ToString(CultureInfo.InvariantCulture) +
                    ",\"FloatingY\":" + (-coordinate).ToString(CultureInfo.InvariantCulture) + "}";
                var positioned = SettingsStore.Parse(json);
                Equal(positioned.FloatingX, (int?)coordinate, "valid signed X " + coordinate);
                Equal(positioned.FloatingY, (int?)-coordinate, "valid signed Y " + coordinate);
                var clone = positioned.Clone();
                clone.FloatingX = 1; clone.FloatingY = 2; clone.FloatingMode = false;
                Equal(positioned.FloatingX, (int?)coordinate, "floating clone position independent");
                Equal(positioned.FloatingMode, true, "floating clone mode independent");
            }
            foreach (string json in new[] {
                "{}", "{\"FloatingX\":null}", "{\"FloatingY\":null}",
                "{\"FloatingX\":null,\"FloatingY\":null}" })
            {
                var unpositioned = SettingsStore.Parse(json);
                Equal(unpositioned.FloatingX.HasValue || unpositioned.FloatingY.HasValue, false, "nullable floating position " + json);
            }
            foreach (string json in new[] {
                "{\"FloatingX\":0}", "{\"FloatingY\":0}",
                "{\"FloatingX\":null,\"FloatingY\":0}", "{\"FloatingX\":0,\"FloatingY\":null}" })
                Throws(delegate { SettingsStore.Parse(json); }, "FloatingX/FloatingY", "position must be a pair " + json);
            foreach (string field in new[] { "FloatingX", "FloatingY" })
                foreach (string wrongValue in new[] { "true", "false", "\"1\"", "1.0", "1e0", "[]", "{}",
                    "1000001", "-1000001", "2147483648", "9223372036854775807" })
                {
                    string other = field == "FloatingX" ? "FloatingY" : "FloatingX";
                    string json = "{\"" + field + "\":" + wrongValue + ",\"" + other + "\":0}";
                    Throws(delegate { SettingsStore.Parse(json); }, field, "strict floating coordinate " + json);
                }
            foreach (var invalid in new[] {
                new BreakSettings { FloatingX = 0 }, new BreakSettings { FloatingY = 0 },
                new BreakSettings { FloatingX = Int32.MinValue, FloatingY = 0 },
                new BreakSettings { FloatingX = 0, FloatingY = Int32.MaxValue } })
                Throws(delegate { SettingsStore.Validate(invalid); }, "FloatingX/FloatingY", "invalid in-memory floating position");
            var whiteListed = SettingsStore.Parse("{\"FloatingMode\":false,\"FloatingX\":-30,\"FloatingY\":50,\"FutureOption\":\"discard\"}");
            string safeJson = new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(whiteListed.Clone());
            Equal(safeJson.Contains("FutureOption"), false, "unknown field excluded from floating save model");
            Equal(SettingsStore.Parse(safeJson).FloatingMode, false, "floating mode survives serialization");
            Equal(SettingsStore.Parse(safeJson).FloatingY, (int?)50, "floating coordinate survives serialization");
        }

        private static void TestPersistence()
        {
            string testRoot = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
            string directory = Path.GetFullPath(Path.Combine(testRoot, "temporary-core-" + Guid.NewGuid().ToString("N")));
            string testPrefix = testRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!directory.StartsWith(testPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Test temporary path must stay inside its test output directory.");
            string path = Path.Combine(directory, "nested", "settings.json");
            try
            {
                BreakSettings missing = SettingsStore.Load(path);
                Equal(missing.Eyes, true, "missing file default");
                Equal(missing.AutoExpand, true, "missing file growth default");
                Equal(missing.Mascot, "cat", "missing file mascot default");
                Equal(missing.FloatingMode, true, "missing file floating default");
                Equal(Directory.Exists(directory), false, "read default does not create directory");
                var invalid = new BreakSettings { Mascot = "invalid" };
                Throws(delegate { SettingsStore.Save(path, invalid); }, "Mascot", "invalid save rejected before IO");
                Equal(Directory.Exists(directory), false, "invalid save creates nothing");

                var saved = new BreakSettings { Eyes = false, AutoExpand = false, FloatingMode = false,
                    FloatingX = -2100, FloatingY = 144, Paused = true,
                    PauseUntil = "2026-10-25T13:52:00", SkippedHour = "2026-10-25-12" };
                SettingsStore.Save(path, saved);
                BreakSettings loaded = SettingsStore.Load(path);
                Equal(loaded.Version, saved.Version, "version roundtrip");
                Equal(loaded.Eyes, saved.Eyes, "eyes roundtrip");
                Equal(loaded.Water, saved.Water, "water roundtrip");
                Equal(loaded.Move, saved.Move, "move roundtrip");
                Equal(loaded.AutoExpand, saved.AutoExpand, "growth roundtrip");
                Equal(loaded.FloatingMode, saved.FloatingMode, "floating mode roundtrip");
                Equal(loaded.FloatingX, saved.FloatingX, "floating X roundtrip");
                Equal(loaded.FloatingY, saved.FloatingY, "floating Y roundtrip");
                Equal(loaded.Mascot, saved.Mascot, "mascot roundtrip");
                Equal(loaded.Paused, saved.Paused, "paused roundtrip");
                Equal(loaded.PauseUntil, saved.PauseUntil, "deadline roundtrip");
                Equal(loaded.SkippedHour, saved.SkippedHour, "skip roundtrip");
                Equal(File.Exists(path + ".bak"), false, "first save no spurious backup");
                byte[] bytes = File.ReadAllBytes(path);
                Equal(bytes.Length > 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf, true, "UTF8 BOM preserved");
                saved.Water = false;
                saved.FloatingMode = true;
                saved.FloatingX = -2400;
                SettingsStore.Save(path, saved);
                Equal(SettingsStore.Load(path).Water, false, "replacement effective");
                Equal(SettingsStore.Load(path).FloatingMode, true, "floating mode replacement effective");
                Equal(SettingsStore.Load(path).FloatingX, (int?)-2400, "floating position replacement effective");
                Equal(SettingsStore.Load(path + ".bak").FloatingMode, false, "backup previous floating mode");
                Equal(SettingsStore.Load(path + ".bak").FloatingX, (int?)-2100, "backup previous floating position");
                Equal(SettingsStore.Load(path + ".bak").Water, true, "backup previous value");
                saved.Move = false;
                SettingsStore.Save(path, saved);
                Equal(SettingsStore.Load(path + ".bak").Water, false, "third save updates backup");
                Equal(SettingsStore.Load(path + ".bak").Move, true, "backup exactly one version old");
                string previous = "cat";
                foreach (string mascot in new[] { "fish", "companion", "none", "cat" })
                {
                    saved.Mascot = mascot;
                    SettingsStore.Save(path, saved);
                    Equal(SettingsStore.Load(path).Mascot, mascot, "mascot persisted " + mascot);
                    Equal(SettingsStore.Load(path + ".bak").Mascot, previous, "mascot backup " + previous);
                    previous = mascot;
                }
                string currentJson = File.ReadAllText(path);
                string backupJson = File.ReadAllText(path + ".bak");
                foreach (string badMascot in new[] { "dragon", "Cat", "", " cat", null })
                {
                    invalid = saved.Clone(); invalid.Mascot = badMascot;
                    Throws(delegate { SettingsStore.Save(path, invalid); }, "Mascot", "invalid mascot not saved");
                    Equal(File.ReadAllText(path), currentJson, "invalid save preserves settings bytes");
                    Equal(File.ReadAllText(path + ".bak"), backupJson, "invalid save preserves backup bytes");
                }
                invalid = saved.Clone(); invalid.Version = 2;
                Throws(delegate { SettingsStore.Save(path, invalid); }, "Version", "invalid version not saved");
                invalid = saved.Clone(); invalid.PauseUntil = null;
                Throws(delegate { SettingsStore.Save(path, invalid); }, "PauseUntil", "null pause not saved");
                invalid = saved.Clone(); invalid.SkippedHour = "2026-02-30-12";
                Throws(delegate { SettingsStore.Save(path, invalid); }, "SkippedHour", "invalid skip not saved");
                Equal(File.ReadAllText(path), currentJson, "all invalid saves preserve current");
                Equal(Directory.GetFiles(Path.GetDirectoryName(path), "*.tmp").Length, 0, "no temporary files left");
                Throws(delegate { SettingsStore.Load(directory); }, "文件夹", "directory path rejected");

                File.WriteAllText(path, "{oops", Encoding.UTF8);
                Throws(delegate { SettingsStore.Load(path); }, "无法读取", "corrupt main surfaces error to UI");
                Equal(SettingsStore.Load(path + ".bak").Mascot, "none", "corrupt main leaves readable backup");
                Equal(File.ReadAllText(path), "{oops", "load does not overwrite corrupt settings");
            }
            finally
            {
                // 删除前重新确认绝对路径边界；只清理本测试的 GUID 目录。
                string cleanupPath = Path.GetFullPath(directory);
                if (!cleanupPath.StartsWith(testPrefix, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Refusing to clean a path outside the test output directory.");
                if (Directory.Exists(cleanupPath)) Directory.Delete(cleanupPath, true);
            }
        }
    }
}
