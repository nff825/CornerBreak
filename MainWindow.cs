using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CornerBreak
{
    public sealed class MainWindow : Form
    {
        private readonly AppOptions options;
        private readonly string settingsPath;
        private readonly MascotCatalog mascots;
        private readonly List<Font> fonts = new List<Font>();
        private readonly Stopwatch previewWatch = new Stopwatch();
        private DateTime previewStart;
        private string settingsWarning = "";
        private bool quitting, manualOpen, allowActivate, timersStarted, resourcesDisposed;
        private string shownHour = "", lastMode = "";
        private double growthWidth, growthHeight;
        private double growthAnchorRight = Double.NaN, growthAnchorBottom = Double.NaN;
        private string growthScreenName = "";
        private readonly Timer timer = new Timer();
        private readonly Timer growthTimer = new Timer();
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private readonly NotifyIcon tray = new NotifyIcon();
        private readonly Icon trayIcon;
        private readonly Label detail, clockCaption, mascotCaption, note;
        private readonly Button hideButton, pauseButton;
        private readonly ToolStripItem skipItem;

        public BreakSettings CurrentSettings { get; private set; }
        public ReminderState CurrentState { get { return ReminderRules.GetState(CurrentTime, CurrentSettings); } }
        public DateTime CurrentTime { get { return options.Preview ? previewStart.Add(previewWatch.Elapsed) : DateTime.Now; } }
        public Label TitleLabel { get; private set; }
        public Label ClockLabel { get; private set; }
        public Label BrandLabel { get; private set; }
        public Dictionary<string, Panel> Cards { get; private set; }
        public FlowLayoutPanel RootLayout { get; private set; }
        public Panel MascotHeader { get; private set; }
        public PictureBox MascotPicture { get; private set; }
        public ToolStripMenuItem MascotMenuItem { get; private set; }
        public ToolStripMenuItem PauseItem { get; private set; }
        public ContextMenuStrip PauseMenu { get; private set; }
        public ToolStripItem ResumeItem { get; private set; }
        public GrowthSurface GrowthSurface { get; private set; }
        public bool DialogOpen { get; set; }
        public bool GrowthMoving { get; set; }
        public bool RestAutoHidePending { get; private set; }
        protected override bool ShowWithoutActivation { get { return !allowActivate; } }

        public MainWindow(AppOptions options, string settingsPath, MascotCatalog mascots)
        {
            // Build all geometry in 96-DPI coordinates. Enabling autoscaling
            // while controls are still being added can consume the initial
            // DPI change before the controls exist, leaving point-sized text
            // enlarged inside unscaled pixel rectangles.
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.None;
            this.options = options;
            this.settingsPath = settingsPath;
            this.mascots = mascots;
            CurrentSettings = new BreakSettings();
            if (!options.Preview)
            {
                try { CurrentSettings = SettingsStore.Load(settingsPath); }
                catch
                {
                    settingsWarning = "设置读取失败，暂用默认设置；原文件已保留。";
                    try
                    {
                        if (File.Exists(settingsPath + ".bak"))
                        {
                            CurrentSettings = SettingsStore.Load(settingsPath + ".bak");
                            settingsWarning = "已使用上次设置备份；原文件已保留。";
                        }
                    }
                    catch { }
                }
            }
            previewStart = options.PreviewAt ?? DateTime.Today.AddHours(12).AddMinutes(50);
            previewWatch.Start();
            if (options.Preview)
            {
                CurrentSettings.Mascot = options.PreviewMascot;
                switch (options.PreviewScenario)
                {
                    case "Waiting": previewStart = previewStart.Date.AddHours(12).AddMinutes(40); break;
                    case "Skipped": CurrentSettings.SkippedHour = previewStart.ToString("yyyy-MM-dd-HH"); break;
                    case "Paused": CurrentSettings.Paused = true; CurrentSettings.PauseUntil = previewStart.AddHours(1).ToString("o"); break;
                    case "Disabled": CurrentSettings.Eyes = CurrentSettings.Water = CurrentSettings.Move = false; break;
                }
            }

            DoubleBuffered = true;
            Text = "CornerBreak · 角落小憩" + (options.Preview ? "（预览）" : "");
            Font = NewFont(9);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(246, 248, 252);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            ClientSize = new Size(420, 480);
            trayIcon = TrayArtwork.Create();
            Icon = trayIcon;
            RootLayout = new FlowLayoutPanel {
                Name = "ReminderLayout", FlowDirection = FlowDirection.TopDown,
                WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill, Padding = new Padding(20, 16, 20, 14)
            };
            RootLayout.SuspendLayout();
            Controls.Add(RootLayout);

            MascotHeader = new Panel { Name = "Mascot", Size = new Size(380, 92), Margin = Padding.Empty };
            var mascotBrand = NewLabel("CORNERBREAK", 25, 10);
            mascotBrand.Font = NewFont(10, FontStyle.Bold);
            mascotBrand.ForeColor = Color.FromArgb(53, 105, 178);
            mascotBrand.Bounds = new Rectangle(0, 15, 220, 25);
            MascotHeader.Controls.Add(mascotBrand);
            mascotCaption = NewLabel("", 30, 9);
            mascotCaption.Bounds = new Rectangle(0, 42, 230, 30);
            mascotCaption.ForeColor = Color.FromArgb(103, 117, 137);
            MascotHeader.Controls.Add(mascotCaption);
            MascotPicture = new PictureBox { Name = "MascotPicture", SizeMode = PictureBoxSizeMode.Zoom,
                Bounds = new Rectangle(240, 0, 140, 88), TabStop = false };
            MascotHeader.Controls.Add(MascotPicture);
            RootLayout.Controls.Add(MascotHeader);
            BrandLabel = NewLabel("CORNERBREAK  /  角落小憩", 25, 9);
            BrandLabel.Name = "Brand";
            BrandLabel.ForeColor = Color.FromArgb(53, 105, 178);
            RootLayout.Controls.Add(BrandLabel);
            TitleLabel = NewLabel("该休息一下了", 40, 19);
            TitleLabel.Name = "Title";
            TitleLabel.Font = NewFont(19, FontStyle.Bold);
            RootLayout.Controls.Add(TitleLabel);
            detail = NewLabel("", 44, 9);
            detail.Name = "Detail";
            detail.ForeColor = Color.FromArgb(103, 117, 137);
            RootLayout.Controls.Add(detail);
            Cards = new Dictionary<string, Panel>();
            AddCard("Eyes", "眼", "让眼睛休息", "暂时离开屏幕，看看远处。", Color.FromArgb(226, 238, 251));
            AddCard("Water", "水", "喝点水", "放下手头的事，喝点水。", Color.FromArgb(225, 242, 238));
            AddCard("Move", "动", "起身活动", "离开工位，站起来走动一下。", Color.FromArgb(253, 238, 218));

            var countdownPanel = new Panel { Name = "Countdown", Size = new Size(380, 71), Margin = new Padding(0, 2, 0, 6) };
            ClockLabel = NewLabel("", 44, 23);
            ClockLabel.Name = "Clock";
            ClockLabel.Size = new Size(174, 44);
            ClockLabel.Font = NewFont(23, FontStyle.Bold);
            countdownPanel.Controls.Add(ClockLabel);
            clockCaption = NewLabel("", 23, 8.5f);
            clockCaption.Bounds = new Rectangle(0, 45, 174, 23);
            clockCaption.ForeColor = Color.FromArgb(103, 117, 137);
            countdownPanel.Controls.Add(clockCaption);
            var scheduleLabel = NewLabel("每小时 50 分 → 整点\r\n留一段时间，照顾自己。", 52, 8.5f);
            scheduleLabel.Bounds = new Rectangle(180, 5, 200, 52);
            scheduleLabel.TextAlign = ContentAlignment.MiddleRight;
            scheduleLabel.ForeColor = Color.FromArgb(103, 117, 137);
            countdownPanel.Controls.Add(scheduleLabel);
            RootLayout.Controls.Add(countdownPanel);

            var actions = new Panel { Name = "Actions", Size = new Size(380, 42), Margin = Padding.Empty };
            hideButton = NewButton("本轮不再提示", 184);
            hideButton.Name = "HideButton";
            hideButton.BackColor = Color.FromArgb(53, 105, 178);
            hideButton.ForeColor = Color.White;
            hideButton.FlatAppearance.BorderSize = 0;
            hideButton.Click += delegate { HideReminder(); };
            actions.Controls.Add(hideButton);
            pauseButton = NewButton("暂停提醒", 184);
            pauseButton.Name = "PauseButton";
            pauseButton.Location = new Point(196, 0);
            pauseButton.Click += delegate {
                if (CurrentSettings.Paused && !CurrentState.PauseExpired) ResumeReminder();
                else PauseMenu.Show(pauseButton, new Point(0, pauseButton.Height));
            };
            actions.Controls.Add(pauseButton);
            RootLayout.Controls.Add(actions);
            var settingsButton = new LinkLabel { Name = "SettingsLink", Text = "提醒设置",
                Size = new Size(380, 27), Margin = Padding.Empty, TextAlign = ContentAlignment.MiddleCenter,
                LinkColor = Color.FromArgb(81, 112, 154), Font = NewFont(9) };
            settingsButton.LinkClicked += delegate { OpenSettings(); };
            RootLayout.Controls.Add(settingsButton);
            note = NewLabel("", 32, 8);
            note.Name = "Note";
            note.ForeColor = Color.FromArgb(154, 101, 34);
            note.Visible = false;
            RootLayout.Controls.Add(note);

            menu.Items.Add("查看状态", null, delegate { ShowStatus(); });
            menu.Items.Add("提醒设置…", null, delegate { OpenSettings(); });
            MascotMenuItem = new ToolStripMenuItem("休息伙伴");
            menu.Items.Add(MascotMenuItem);
            foreach (var choice in MascotCatalog.Options)
            {
                var item = new ToolStripMenuItem(choice.Label) { Tag = choice.Id };
                item.Click += delegate(object sender, EventArgs e) { SetMascot((string)((ToolStripMenuItem)sender).Tag); };
                MascotMenuItem.DropDownItems.Add(item);
            }
            skipItem = menu.Items.Add("本轮不再提示", null, delegate { HideReminder(); });
            PauseItem = new ToolStripMenuItem("暂停提醒");
            menu.Items.Add(PauseItem);
            PauseMenu = new ContextMenuStrip();
            string[] durationLabels = { "暂停 30 分钟", "暂停 1 小时", "暂停 2 小时", "暂停，直到手动恢复" };
            int[] durations = { 30, 60, 120, 0 };
            for (int i = 0; i < durations.Length; i++)
            {
                foreach (var collection in new[] { PauseItem.DropDownItems, PauseMenu.Items })
                {
                    var item = new ToolStripMenuItem(durationLabels[i]) { Tag = durations[i] };
                    item.Click += delegate(object sender, EventArgs e) { SetPaused((int)((ToolStripMenuItem)sender).Tag); };
                    collection.Add(item);
                }
            }
            ResumeItem = menu.Items.Add("恢复提醒", null, delegate { ResumeReminder(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出 CornerBreak", null, delegate { RequestExit(); });
            tray.Icon = trayIcon;
            tray.Text = "CornerBreak · 角落小憩";
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { ShowStatus(); };
            // Preview windows do not need an additional persistent tray icon.
            tray.Visible = !options.Preview;

            RootLayout.ResumeLayout(false);
            AutoScaleMode = AutoScaleMode.Dpi;
            // Changing AutoScaleMode clears AutoScaleDimensions. Set the
            // authored baseline after the mode so it survives the setter.
            AutoScaleDimensions = new SizeF(96, 96);
            ResumeLayout(false);
            PerformAutoScale();
            PerformLayout();
            GrowthSurface = new GrowthSurface(this, RootLayout);
            ResizeBegin += delegate { GrowthMoving = true; };
            ResizeEnd += delegate {
                GrowthMoving = false;
                growthAnchorRight = Right;
                growthAnchorBottom = Bottom;
                growthWidth = Width;
                growthHeight = Height;
                growthScreenName = Screen.FromControl(this).DeviceName;
            };
            timer.Interval = 1000;
            timer.Tick += delegate { UpdateReminder(); };
            growthTimer.Interval = 200;
            growthTimer.Tick += delegate { if (Visible) UpdateReminderGrowth(); };
        }

        private Font NewFont(float size, FontStyle style = FontStyle.Regular)
        {
            var font = new Font("Microsoft YaHei UI", size, style);
            fonts.Add(font);
            return font;
        }

        private Label NewLabel(string text, int height, float size)
        {
            return new Label { Text = text, Size = new Size(380, height), Margin = Padding.Empty,
                Font = NewFont(size), ForeColor = Color.FromArgb(39, 53, 75), TextAlign = ContentAlignment.MiddleLeft };
        }

        private Button NewButton(string text, int width)
        {
            var button = new Button { Text = text, Size = new Size(width, 36), Font = NewFont(9),
                FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Color.FromArgb(46, 75, 116),
                UseVisualStyleBackColor = false, Cursor = Cursors.Hand };
            button.FlatAppearance.BorderColor = Color.FromArgb(203, 214, 229);
            return button;
        }

        private void AddCard(string name, string tagText, string headingText, string body, Color color)
        {
            var panel = new Panel { Name = name, Size = new Size(380, 64), Margin = new Padding(0, 0, 0, 8), BackColor = Color.White };
            var tag = NewLabel(tagText, 38, 13);
            tag.Bounds = new Rectangle(12, 13, 38, 38);
            tag.TextAlign = ContentAlignment.MiddleCenter;
            tag.BackColor = color;
            panel.Controls.Add(tag);
            var heading = NewLabel(headingText, 25, 10);
            heading.Font = NewFont(10, FontStyle.Bold);
            heading.Bounds = new Rectangle(62, 6, 308, 25);
            panel.Controls.Add(heading);
            var description = NewLabel(body, 25, 9);
            description.Bounds = new Rectangle(62, 31, 308, 25);
            description.ForeColor = Color.FromArgb(103, 117, 137);
            panel.Controls.Add(description);
            RootLayout.Controls.Add(panel);
            Cards.Add(name, panel);
        }

        public void SetPreviewTime(DateTime value)
        {
            if (!options.Preview) throw new InvalidOperationException("Only preview windows support a simulated clock.");
            previewWatch.Reset();
            previewStart = value;
        }

        public bool SaveSettings(BreakSettings candidate)
        {
            try
            {
                SettingsStore.Validate(candidate);
                if (!options.Preview) SettingsStore.Save(settingsPath, candidate);
                CurrentSettings = candidate.Clone();
                settingsWarning = "";
                return true;
            }
            catch (Exception error)
            {
                bool previous = DialogOpen;
                DialogOpen = true;
                try { MessageBox.Show(this, "设置未保存，请确认工具目录可以写入。\r\n" + error.Message, "CornerBreak", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                finally { DialogOpen = previous; }
                return false;
            }
        }

        public Screen GetGrowthScreen()
        {
            if (growthScreenName.Length != 0)
                foreach (var screen in Screen.AllScreens) if (screen.DeviceName == growthScreenName) return screen;
            return Visible ? Screen.FromControl(this) : Screen.FromPoint(Cursor.Position);
        }

        public bool IsReminderInteraction()
        {
            return DialogOpen || GrowthMoving || menu.Visible || PauseMenu.Visible || PauseItem.DropDown.Visible ||
                MascotMenuItem.DropDown.Visible || (Visible && Bounds.Contains(Cursor.Position));
        }

        public void UpdateReminderGrowth(bool immediate = false, bool ignoreInteraction = false)
        {
            if (!ignoreInteraction && IsReminderInteraction()) return;
            var screen = GetGrowthScreen();
            growthScreenName = screen.DeviceName;
            var area = screen.WorkingArea;
            var baseline = GrowthSurface.Measure(CurrentSettings.Eyes, CurrentSettings.Water, CurrentSettings.Move, options.Preview || settingsWarning.Length != 0);
            var target = ReminderRules.GetGrowth(CurrentTime, CurrentSettings, baseline.Width, baseline.Height, area.Width, area.Height);
            double marginX = Math.Min(20, area.Width / 4.0), marginY = Math.Min(20, area.Height / 4.0);
            double usableWidth = area.Width - 2 * marginX, usableHeight = area.Height - 2 * marginY;
            if (Double.IsNaN(growthAnchorRight)) growthAnchorRight = area.Right - marginX;
            if (Double.IsNaN(growthAnchorBottom)) growthAnchorBottom = area.Bottom - marginY;
            growthAnchorRight = Math.Max(area.Left + marginX + Math.Min(baseline.Width, usableWidth), Math.Min(area.Right - marginX, growthAnchorRight));
            growthAnchorBottom = Math.Max(area.Top + marginY + Math.Min(baseline.Height, usableHeight), Math.Min(area.Bottom - marginY, growthAnchorBottom));
            double width = Math.Min(target.Width, growthAnchorRight - area.Left - marginX);
            double height = Math.Min(target.Height, growthAnchorBottom - area.Top - marginY);
            if (immediate || !Visible || growthWidth <= 0) { growthWidth = width; growthHeight = height; }
            else { growthWidth += (width - growthWidth) * 0.22; growthHeight += (height - growthHeight) * 0.22; }
            growthWidth = Math.Min(growthWidth, growthAnchorRight - area.Left - marginX);
            growthHeight = Math.Min(growthHeight, growthAnchorBottom - area.Top - marginY);
            int newWidth = Math.Max(1, (int)Math.Floor(growthWidth));
            int newHeight = Math.Max(1, (int)Math.Floor(growthHeight));
            GrowthSurface.Apply(new Rectangle((int)(growthAnchorRight - newWidth), (int)(growthAnchorBottom - newHeight), newWidth, newHeight), baseline);
        }

        private void PositionReminder()
        {
            var screen = Visible ? Screen.FromControl(this) : Screen.FromPoint(Cursor.Position);
            var area = screen.WorkingArea;
            growthScreenName = screen.DeviceName;
            growthAnchorRight = area.Right - Math.Min(20, area.Width / 4.0);
            growthAnchorBottom = area.Bottom - Math.Min(20, area.Height / 4.0);
            UpdateReminderGrowth(true);
        }

        public void ShowStatus()
        {
            manualOpen = true;
            UpdateReminder(false);
            var state = CurrentState;
            if (state.Mode == "Active") shownHour = state.HourKey;
            if (!Visible) PositionReminder();
            allowActivate = true;
            try { Show(); Activate(); }
            finally { allowActivate = false; }
        }

        public void SetPaused(int minutes)
        {
            var candidate = CurrentSettings.Clone();
            candidate.Paused = true;
            candidate.PauseUntil = minutes > 0 ? CurrentTime.AddMinutes(minutes).ToString("o") : "";
            if (!SaveSettings(candidate)) return;
            shownHour = "";
            manualOpen = true;
            UpdateReminder(false);
        }

        public void ResumeReminder()
        {
            var candidate = CurrentSettings.Clone();
            candidate.Paused = false;
            candidate.PauseUntil = "";
            if (!SaveSettings(candidate)) return;
            shownHour = "";
            UpdateReminder();
        }

        public void HideReminder()
        {
            var state = CurrentState;
            if (state.Mode == "Active")
            {
                var candidate = CurrentSettings.Clone();
                candidate.SkippedHour = state.HourKey;
                if (!SaveSettings(candidate)) return;
            }
            manualOpen = false;
            Hide();
            UpdateReminderGrowth(true);
        }

        public void UpdateReminder(bool allowPopup = true)
        {
            var state = CurrentState;
            bool isPaused = CurrentSettings.Paused && !state.PauseExpired;
            skipItem.Enabled = state.Mode == "Active";
            PauseItem.Visible = !isPaused;
            ResumeItem.Visible = isPaused;
            pauseButton.Text = isPaused ? "恢复提醒" : "暂停提醒";
            hideButton.Text = state.Mode == "Active" ? "本轮不再提示" : "收起窗口";
            SuspendLayout();
            RootLayout.SuspendLayout();
            try
            {
                Bitmap image;
                bool hasMascot = CurrentSettings.Mascot != "none" && mascots.Images.TryGetValue(CurrentSettings.Mascot, out image);
                MascotHeader.Visible = hasMascot;
                BrandLabel.Visible = !hasMascot;
                GrowthSurface.MascotVisible = hasMascot;
                MascotPicture.Image = hasMascot ? mascots.Images[CurrentSettings.Mascot] : null;
                foreach (var choice in MascotCatalog.Options) if (choice.Id == CurrentSettings.Mascot) mascotCaption.Text = choice.Caption;
                foreach (ToolStripMenuItem item in MascotMenuItem.DropDownItems) item.Checked = (string)item.Tag == CurrentSettings.Mascot;
                Cards["Eyes"].Visible = CurrentSettings.Eyes;
                Cards["Water"].Visible = CurrentSettings.Water;
                Cards["Move"].Visible = CurrentSettings.Move;
                ClockLabel.Text = ReminderRules.FormatCountdown(state.SecondsLeft);
                switch (state.Mode)
                {
                    case "Active":
                        TitleLabel.Text = "该休息一下了";
                        detail.Text = "给自己一点空隙。到 " + state.Target.Value.ToString("HH:mm") + " 自动结束提醒。";
                        clockCaption.Text = "本轮休息剩余";
                        tray.Text = "CornerBreak · 本轮剩余 " + ClockLabel.Text;
                        break;
                    case "Waiting":
                        TitleLabel.Text = "下一次小憩，稍后见";
                        detail.Text = "下次提醒：" + state.Target.Value.ToString("HH:mm") + "。到时会显示已开启的项目。";
                        clockCaption.Text = "距离下次提醒";
                        tray.Text = "CornerBreak · 下次 " + state.Target.Value.ToString("HH:mm");
                        break;
                    case "Skipped":
                        TitleLabel.Text = "本轮已跳过";
                        detail.Text = "本轮不会再次弹出。下次提醒：" + state.Target.Value.AddMinutes(50).ToString("HH:mm") + "。";
                        clockCaption.Text = "本轮结束倒计时";
                        tray.Text = "CornerBreak · 本轮已跳过";
                        break;
                    case "Paused":
                        TitleLabel.Text = "提醒已暂停";
                        if (state.PauseUntil.HasValue)
                        {
                            detail.Text = "将在 " + state.PauseUntil.Value.ToString("HH:mm") + " 恢复提醒，也可随时手动恢复。";
                            clockCaption.Text = "距离恢复提醒";
                        }
                        else
                        {
                            detail.Text = "暂停期间不会弹出提醒。准备好后，点击「恢复提醒」。";
                            ClockLabel.Text = "已暂停";
                            clockCaption.Text = "等待手动恢复";
                        }
                        tray.Text = "CornerBreak · 已暂停";
                        break;
                    case "Disabled":
                        TitleLabel.Text = "提醒项目已全部关闭";
                        detail.Text = "打开「提醒设置」，选择眼睛、喝水或起身活动。";
                        ClockLabel.Text = "未开启";
                        clockCaption.Text = "随时可以重新开启";
                        tray.Text = "CornerBreak · 提醒项目已关闭";
                        break;
                }
                note.Text = options.Preview ? "预览模式 · 设置不会影响日常使用" : settingsWarning;
                note.Visible = note.Text.Length != 0;
            }
            finally { RootLayout.ResumeLayout(true); ResumeLayout(true); }
            if (lastMode == "Active" && state.Mode == "Waiting") RestAutoHidePending = true;
            if (RestAutoHidePending && !DialogOpen)
            {
                if (state.Mode != "Active") manualOpen = false;
                RestAutoHidePending = false;
            }
            if (state.Mode == "Active" && allowPopup && !DialogOpen && shownHour != state.HourKey)
            {
                shownHour = state.HourKey;
                manualOpen = false;
                if (!Visible) PositionReminder();
                Show();
            }
            else if (state.Mode != "Active" && !manualOpen && !DialogOpen && Visible) Hide();
            if (state.HourKey != shownHour) shownHour = "";
            lastMode = state.Mode;
            if (options.Preview) tray.Text += "（预览）";
            UpdateReminderGrowth();
        }

        public void RefreshSettingsLayout()
        {
            UpdateReminder(false);
            // Explicit edits must fit changed content even while hovered.
            UpdateReminderGrowth(true, true);
        }

        public void SetMascot(string id)
        {
            var candidate = CurrentSettings.Clone();
            candidate.Mascot = id;
            if (SaveSettings(candidate)) RefreshSettingsLayout();
        }

        public void OpenSettings()
        {
            if (DialogOpen) return;
            ShowStatus();
            DialogOpen = true;
            try
            {
                using (var editor = new SettingsDialog(CurrentSettings, mascots))
                    if (editor.ShowDialog(this) == DialogResult.OK) SaveSettings(editor.BuildCandidate());
            }
            finally { DialogOpen = false; RefreshSettingsLayout(); }
        }

        public void StartTimers()
        {
            if (timersStarted) return;
            timersStarted = true;
            if (options.Show || options.Preview || settingsWarning.Length != 0) ShowStatus();
            else UpdateReminder();
            timer.Start();
            growthTimer.Start();
        }

        public void RequestExit()
        {
            if (quitting) return;
            quitting = true;
            timer.Stop();
            growthTimer.Stop();
            tray.Visible = false;
            Application.ExitThread();
        }

        public void Render(string path, bool settingsDialog)
        {
            if (!options.Preview) throw new InvalidOperationException("Rendering requires preview mode.");
            SetPreviewTime(previewStart);
            ShowStatus();
            UpdateReminderGrowth(true, true);
            Form renderForm = this;
            SettingsDialog editor = null;
            try
            {
                if (settingsDialog)
                {
                    editor = new SettingsDialog(CurrentSettings, mascots);
                    editor.Show(this);
                    renderForm = editor;
                }
                renderForm.PerformLayout();
                Application.DoEvents();
                string fullPath = Path.GetFullPath(path);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                using (var bitmap = new Bitmap(renderForm.Width, renderForm.Height))
                {
                    renderForm.DrawToBitmap(bitmap, new Rectangle(Point.Empty, renderForm.Size));
                    bitmap.Save(fullPath, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            finally { if (editor != null) editor.Dispose(); }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (options.Preview || quitting) RequestExit();
            else { e.Cancel = true; HideReminder(); }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            bool releaseResources = disposing && !resourcesDisposed;
            if (releaseResources)
            {
                resourcesDisposed = true;
                timer.Stop(); timer.Dispose();
                growthTimer.Stop(); growthTimer.Dispose();
                tray.Visible = false; tray.Dispose();
                menu.Dispose();
                if (PauseMenu != null) PauseMenu.Dispose();
                if (MascotPicture != null) MascotPicture.Image = null;
            }
            base.Dispose(disposing);
            if (releaseResources)
            {
                if (GrowthSurface != null) GrowthSurface.Dispose();
                if (trayIcon != null) trayIcon.Dispose();
                foreach (var font in fonts) font.Dispose();
                fonts.Clear();
            }
        }
    }

    internal static class TrayArtwork
    {
        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);
        public static Icon Create()
        {
            using (var bitmap = new Bitmap(32, 32))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    graphics.Clear(Color.Transparent);
                    using (var brush = new SolidBrush(Color.FromArgb(53, 105, 178))) graphics.FillEllipse(brush, 1, 1, 30, 30);
                    graphics.FillRectangle(Brushes.White, 10, 9, 4, 14);
                    graphics.FillRectangle(Brushes.White, 18, 9, 4, 14);
                }
                IntPtr handle = bitmap.GetHicon();
                try { using (var icon = Icon.FromHandle(handle)) return (Icon)icon.Clone(); }
                finally { DestroyIcon(handle); }
            }
        }
    }
}
