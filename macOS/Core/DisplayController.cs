using System;

namespace CornerBreak
{
    public enum BreakView { Hidden, Widget, Details }

    public sealed class BreakPresentation
    {
        public ReminderState State { get; private set; }
        public BreakView View { get; private set; }
        public DateTime Now { get; private set; }
        public BreakPresentation(ReminderState state, BreakView view, DateTime now)
        { State = state; View = view; Now = now; }
    }

    // No UI or clock dependency: every transition receives the caller's local
    // time. The desktop owns persistence and decides when interaction ends.
    public sealed class DisplayController
    {
        private BreakSettings settings;
        private bool manualDetails;
        private string shownHour = String.Empty;
        private string collapsedHour = String.Empty;
        private string previousMode = String.Empty;
        public BreakSettings Settings { get { return settings.Clone(); } }
        public BreakPresentation Current { get; private set; }
        public ReminderState State { get { return Current.State; } }
        public BreakView View { get { return Current.View; } }

        public DisplayController(BreakSettings initialSettings)
        {
            SettingsStore.Validate(initialSettings);
            settings = initialSettings.Clone();
            // Do not consult the real clock here: a caller may be about to
            // supply a frozen preview time whose pause is still in effect.
            Current = new BreakPresentation(new ReminderState {
                Mode = "Waiting", HourKey = String.Empty
            }, settings.FloatingMode ? BreakView.Widget : BreakView.Hidden, DateTime.MinValue);
        }

        public DisplayController(BreakSettings initialSettings, DateTime initialTime) : this(initialSettings)
        { Tick(initialTime, true); }

        public BreakPresentation Tick(DateTime now, bool deferAutoExpand = false)
        {
            ReminderState state = ReminderRules.GetState(now, settings);
            if (state.PauseExpired)
            {
                // A saved expired deadline remains self-expiring on reopen.
                // Clearing it in memory does not require timer-driven writes.
                settings.Paused = false;
                settings.PauseUntil = String.Empty;
                state = ReminderRules.GetState(now, settings);
            }
            if ((previousMode == "Active" || previousMode == "Skipped") &&
                state.Mode != "Active" && state.Mode != "Skipped") manualDetails = false;
            if (state.Mode == "Active" && !manualDetails && !deferAutoExpand && shownHour != state.HourKey)
            {
                shownHour = state.HourKey;
                collapsedHour = String.Empty;
            }
            bool showDetails = manualDetails ||
                (state.Mode == "Active" && shownHour == state.HourKey && collapsedHour != state.HourKey);
            BreakView view = showDetails ? BreakView.Details : settings.FloatingMode ? BreakView.Widget : BreakView.Hidden;
            if (state.HourKey != shownHour) shownHour = String.Empty;
            previousMode = state.Mode;
            Current = new BreakPresentation(state, view, now);
            return Current;
        }

        public BreakPresentation OpenDetails(DateTime now)
        {
            // Settle a crossed hour before applying explicit user intent, so
            // an hour-end transition cannot immediately undo this click.
            Tick(now, true);
            manualDetails = true;
            collapsedHour = String.Empty;
            ReminderState state = ReminderRules.GetState(now, settings);
            if (state.Mode == "Active") shownHour = state.HourKey;
            return Tick(now, true);
        }

        public BreakPresentation CollapseDetails(DateTime now)
        {
            ReminderState state = ReminderRules.GetState(now, settings);
            if (state.Mode == "Active") shownHour = collapsedHour = state.HourKey;
            manualDetails = false;
            return Tick(now, true);
        }

        public BreakPresentation SkipCurrent(DateTime now)
        {
            ReminderState state = ReminderRules.GetState(now, settings);
            if (state.Mode == "Active") settings.SkippedHour = state.HourKey;
            manualDetails = false;
            return Tick(now, true);
        }

        public BreakPresentation SetPaused(DateTime now, int minutes = 0)
        {
            if (minutes < 0) throw new ArgumentOutOfRangeException("minutes");
            BreakSettings next = settings.Clone();
            next.Paused = true;
            next.PauseUntil = minutes == 0 ? String.Empty : now.AddMinutes(minutes).ToString("o");
            SettingsStore.Validate(next);
            settings = next;
            shownHour = collapsedHour = String.Empty;
            manualDetails = false;
            return Tick(now, true);
        }

        public BreakPresentation Resume(DateTime now)
        {
            settings.Paused = false;
            settings.PauseUntil = String.Empty;
            shownHour = collapsedHour = String.Empty;
            manualDetails = false;
            return Tick(now);
        }

        public BreakPresentation SetFloating(DateTime now, bool enabled)
        {
            settings.FloatingMode = enabled;
            manualDetails = false;
            if (!enabled) collapsedHour = String.Empty;
            return Tick(now);
        }

        public BreakPresentation ApplySettings(BreakSettings candidate, DateTime now)
        {
            SettingsStore.Validate(candidate);
            settings = candidate.Clone();
            if (ReminderRules.GetState(now, settings).Mode != "Active") manualDetails = false;
            return Tick(now, true);
        }
    }
}
