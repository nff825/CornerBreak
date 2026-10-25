using System;
using System.Globalization;

namespace CornerBreak
{
    public sealed class ReminderState
    {
        public string Mode { get; set; }
        public string HourKey { get; set; }
        public int SecondsLeft { get; set; }
        public DateTime? Target { get; set; }
        public DateTime? PauseUntil { get; set; }
        public bool PauseExpired { get; set; }
    }

    public sealed class GrowthTarget
    {
        public double Progress { get; set; }
        public double ElapsedFraction { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    public static class ReminderRules
    {
        public static ReminderState GetState(DateTime now, BreakSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            DateTime hourStart = now.Date.AddHours(now.Hour);
            DateTime restStart = hourStart.AddMinutes(50);
            DateTime restEnd = hourStart.AddHours(1);
            bool active = now >= restStart && now < restEnd;
            DateTime target = active ? restEnd : restStart;
            var state = new ReminderState
            {
                Mode = "Waiting",
                HourKey = hourStart.ToString("yyyy-MM-dd-HH", CultureInfo.InvariantCulture),
                SecondsLeft = checked((int)Math.Ceiling((target - now).TotalSeconds)),
                Target = target
            };

            double pauseSeconds = 0;
            if (settings.Paused && settings.PauseUntil != String.Empty)
            {
                DateTime pauseUntil = DateTime.Parse(settings.PauseUntil, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                state.PauseUntil = pauseUntil;
                pauseSeconds = (pauseUntil.ToUniversalTime() - now.ToUniversalTime()).TotalSeconds;
                state.PauseExpired = pauseSeconds <= 0;
            }

            // 全部关闭仍报告暂停到期，让界面能及时清理暂停记录。
            if (!settings.Eyes && !settings.Water && !settings.Move)
            {
                state.Mode = "Disabled";
                return state;
            }
            if (settings.Paused && !state.PauseExpired)
            {
                state.Mode = "Paused";
                state.Target = state.PauseUntil;
                state.SecondsLeft = settings.PauseUntil == String.Empty ? 0 : checked((int)Math.Ceiling(pauseSeconds));
                return state;
            }
            if (active) state.Mode = settings.SkippedHour == state.HourKey ? "Skipped" : "Active";
            return state;
        }

        public static GrowthTarget GetGrowth(DateTime now, BreakSettings settings, double baseWidth,
            double baseHeight, double workWidth, double workHeight)
        {
            ValidateDimension(baseWidth, "baseWidth");
            ValidateDimension(baseHeight, "baseHeight");
            ValidateDimension(workWidth, "workWidth");
            ValidateDimension(workHeight, "workHeight");
            double availableWidth = workWidth - 2 * Math.Min(20, workWidth / 4);
            double availableHeight = workHeight - 2 * Math.Min(20, workHeight / 4);
            double initialWidth = Math.Min(baseWidth, availableWidth);
            double initialHeight = Math.Min(baseHeight, availableHeight);
            double maximumWidth = Math.Min(Math.Max(baseWidth, workWidth * 0.5), availableWidth);
            double maximumHeight = Math.Min(Math.Max(baseHeight, workHeight * 0.8), availableHeight);

            double elapsedFraction = 0;
            ReminderState state = GetState(now, settings);
            if (settings.AutoExpand && state.Mode == "Active")
                elapsedFraction = Math.Max(0, Math.Min(1, (600 - (state.Target.Value - now).TotalSeconds) / 600));
            double progress = elapsedFraction * elapsedFraction;
            return new GrowthTarget
            {
                Progress = progress,
                ElapsedFraction = elapsedFraction,
                Width = initialWidth + (maximumWidth - initialWidth) * progress,
                Height = initialHeight + (maximumHeight - initialHeight) * progress
            };
        }

        public static string FormatCountdown(int seconds)
        {
            int safeSeconds = Math.Max(0, seconds);
            return String.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", safeSeconds / 60, safeSeconds % 60);
        }

        private static void ValidateDimension(double value, string name)
        {
            if (value <= 0 || Double.IsNaN(value) || Double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name, "尺寸必须是有限的正数。");
        }
    }
}
