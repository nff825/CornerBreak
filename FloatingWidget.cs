using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CornerBreak
{
    // A persistent, non-activating entry point. The main window owns display
    // policy, menus and persistence; this form owns only its compact view.
    public sealed class FloatingWidget : Form
    {
        private sealed class LayoutEntry
        {
            public Control Control;
            public Rectangle Bounds;
            public Font Font;
            public Font ScaledFont;
        }

        private readonly MascotCatalog mascots;
        private readonly List<Font> fonts = new List<Font>();
        private readonly Timer pointerTimer = new Timer();
        private readonly Label hint;
        private readonly List<LayoutEntry> layoutEntries = new List<LayoutEntry>();
        private Size baselineSize, lastLayoutSize;
        private Rectangle baselineTextArea;
        private bool hasMascot, lastLayoutHasMascot;
        private bool mouseHeld, dragging, captureInterrupted, resourcesDisposed;
        private Point pressScreen, pressLocation;

        public event EventHandler OpenRequested;
        public event EventHandler PositionChangedByUser;
        public Label StateLabel { get; private set; }
        public Label CountdownLabel { get; private set; }
        public PictureBox MascotPicture { get; private set; }
        public bool IsPointerInteraction { get { return mouseHeld; } }
        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                // TOOLWINDOW and NOACTIVATE keep the small companion out of
                // the taskbar and prevent a drag from taking keyboard focus.
                parameters.ExStyle |= 0x00000080 | 0x08000000;
                return parameters;
            }
        }

        public FloatingWidget(MascotCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException("catalog");
            mascots = catalog;
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            Text = "CornerBreak · 常驻小挂件";
            AccessibleName = "CornerBreak 常驻小挂件";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(246, 248, 252);
            ClientSize = new Size(260, 100);
            Font = NewFont(9, FontStyle.Regular);
            Cursor = Cursors.Hand;

            MascotPicture = new PictureBox {
                Name = "FloatingMascot", Bounds = new Rectangle(8, 10, 82, 78),
                SizeMode = PictureBoxSizeMode.Zoom, TabStop = false,
                AccessibleName = "休息伙伴"
            };
            StateLabel = new Label {
                Name = "FloatingState", Text = "下一次休息", Bounds = new Rectangle(98, 15, 152, 23),
                Font = NewFont(9, FontStyle.Regular), ForeColor = Color.FromArgb(68, 91, 123),
                TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true
            };
            CountdownLabel = new Label {
                Name = "FloatingCountdown", Text = "还有 00:00", Bounds = new Rectangle(98, 39, 152, 29),
                Font = NewFont(13, FontStyle.Bold), ForeColor = Color.FromArgb(39, 53, 75),
                TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true
            };
            hint = new Label {
                Name = "FloatingHint", Text = "点击查看 · 拖动移动", Bounds = new Rectangle(98, 72, 152, 18),
                Font = NewFont(7.5f, FontStyle.Regular), ForeColor = Color.FromArgb(103, 117, 137),
                TextAlign = ContentAlignment.MiddleLeft
            };
            Controls.Add(MascotPicture);
            Controls.Add(StateLabel);
            Controls.Add(CountdownLabel);
            Controls.Add(hint);
            AttachPointerEvents(this);
            pointerTimer.Interval = 30;
            pointerTimer.Tick += PointerTick;

            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            ResumeLayout(false);
            PerformAutoScale();
            PerformLayout();
            baselineSize = Size;
            baselineTextArea = new Rectangle(StateLabel.Left, StateLabel.Top, StateLabel.Width, hint.Bottom - StateLabel.Top);
            foreach (Control control in Controls)
                layoutEntries.Add(new LayoutEntry { Control = control, Bounds = control.Bounds, Font = control.Font });
        }

        private Font NewFont(float size, FontStyle style)
        {
            var font = new Font("Microsoft YaHei UI", size, style);
            fonts.Add(font);
            return font;
        }

        public void UpdateContent(ReminderState state, BreakSettings settings)
        {
            if (state == null) throw new ArgumentNullException("state");
            if (settings == null) throw new ArgumentNullException("settings");
            Bitmap image;
            hasMascot = settings.Mascot != "none" && mascots.Images.TryGetValue(settings.Mascot, out image);
            MascotPicture.Image = hasMascot ? mascots.Images[settings.Mascot] : null;
            switch (state.Mode)
            {
                case "Active":
                    StateLabel.Text = "休息时间";
                    CountdownLabel.Text = "剩余 " + ReminderRules.FormatCountdown(state.SecondsLeft);
                    break;
                case "Skipped":
                    StateLabel.Text = "本轮已跳过";
                    CountdownLabel.Text = state.Target.HasValue ? "下次 " + state.Target.Value.AddMinutes(50).ToString("HH:mm") : "等待下次提醒";
                    break;
                case "Paused":
                    StateLabel.Text = "提醒已暂停";
                    CountdownLabel.Text = state.PauseUntil.HasValue ? "恢复 " + ReminderRules.FormatCountdown(state.SecondsLeft) : "等待手动恢复";
                    break;
                case "Disabled":
                    StateLabel.Text = "提醒已关闭";
                    CountdownLabel.Text = "点击打开设置";
                    break;
                default:
                    StateLabel.Text = state.Target.HasValue ? "下次休息 " + state.Target.Value.ToString("HH:mm") : "下一次休息";
                    CountdownLabel.Text = "还有 " + ReminderRules.FormatCountdown(state.SecondsLeft);
                    break;
            }
            AccessibleDescription = StateLabel.Text + "，" + CountdownLabel.Text + "。单击查看详情，拖动移动位置。";
            ClampToScreen();
        }

        public void Place(int? x, int? y)
        {
            if (x.HasValue != y.HasValue) throw new ArgumentException("浮窗坐标必须成对提供。");
            if (x.HasValue)
                Location = new Point(x.Value, y.Value);
            else
            {
                var screen = Screen.PrimaryScreen ?? Screen.FromPoint(Cursor.Position);
                var area = screen.WorkingArea;
                Location = new Point(area.Right - Width - 20, area.Bottom - Height - 20);
            }
            ClampToScreen();
        }

        public void ClampToScreen()
        {
            var area = Screen.FromRectangle(Bounds).WorkingArea;
            // Always derive the desired size from the immutable DPI baseline:
            // restoring a larger screen must not retain a previous shrink.
            Rectangle clamped = ClampBounds(new Rectangle(Location, baselineSize), area);
            ApplyResponsiveLayout(clamped.Size);
            Location = clamped.Location;
        }

        public static Rectangle ClampBounds(Rectangle desired, Rectangle workArea)
        {
            if (workArea.Width <= 0 || workArea.Height <= 0)
                throw new ArgumentOutOfRangeException("workArea", "工作区尺寸必须大于零。");
            int width = Math.Min(workArea.Width, Math.Max(1, desired.Width));
            int height = Math.Min(workArea.Height, Math.Max(1, desired.Height));
            int left = Math.Max(workArea.Left, Math.Min(workArea.Right - width, desired.Left));
            int top = Math.Max(workArea.Top, Math.Min(workArea.Bottom - height, desired.Top));
            return new Rectangle(left, top, width, height);
        }

        private void ApplyResponsiveLayout(Size size)
        {
            if (lastLayoutSize == size && lastLayoutHasMascot == hasMascot) return;
            double x = size.Width / (double)Math.Max(1, baselineSize.Width);
            double y = size.Height / (double)Math.Max(1, baselineSize.Height);
            double scale = Math.Min(x, y);
            bool showMascot = hasMascot && scale >= 0.75;
            bool showHint = x >= 0.65 && y >= 0.75;
            SuspendLayout();
            try
            {
                Size = size;
                foreach (var entry in layoutEntries)
                {
                    Rectangle bounds = entry.Bounds;
                    int left = Math.Max(0, (int)Math.Round(bounds.Left * x));
                    int top = Math.Max(0, (int)Math.Round(bounds.Top * y));
                    entry.Control.Bounds = new Rectangle(left, top,
                        Math.Max(1, Math.Min(size.Width - left, (int)Math.Round(bounds.Width * x))),
                        Math.Max(1, Math.Min(size.Height - top, (int)Math.Round(bounds.Height * y))));
                }
                MascotPicture.Visible = showMascot;
                hint.Visible = showHint;
                int paddingX = Math.Max(1, Math.Min(size.Width / 4, (int)Math.Round(12 * baselineSize.Width / 260.0 * x)));
                int textLeft = showMascot ? (int)Math.Round(baselineTextArea.Left * x) : paddingX;
                int textWidth = Math.Max(1, size.Width - textLeft - paddingX);
                foreach (Label label in new[] { StateLabel, CountdownLabel, hint })
                {
                    label.Left = Math.Min(Math.Max(0, size.Width - 1), textLeft);
                    label.Width = Math.Min(textWidth, size.Width - label.Left);
                }
                if (!showHint)
                {
                    int paddingY = Math.Max(1, Math.Min(size.Height / 4, (int)Math.Round(8 * baselineSize.Height / 100.0 * y)));
                    int available = Math.Max(1, size.Height - paddingY * 2);
                    int stateHeight = Math.Max(1, (int)Math.Floor(available * 0.4));
                    StateLabel.Bounds = new Rectangle(StateLabel.Left, Math.Min(paddingY, size.Height - 1), textWidth, stateHeight);
                    CountdownLabel.Bounds = new Rectangle(CountdownLabel.Left, Math.Min(size.Height - 1, paddingY + stateHeight), textWidth,
                        Math.Max(1, Math.Min(available - stateHeight, size.Height - paddingY - stateHeight)));
                }
                foreach (var entry in layoutEntries)
                {
                    if (!(entry.Control is Label)) continue;
                    // Small but usable work areas prioritise the status text.
                    // The height cap still permits extreme synthetic areas to
                    // stay bounded instead of clipping their controls.
                    float desiredSize = (float)Math.Min(entry.Font.Size, Math.Max(6.5, entry.Font.Size * scale));
                    float heightCap = (float)(entry.Control.Height * 72.0 / Math.Max(1, 96 * baselineSize.Height / 100.0) * 0.72);
                    desiredSize = (float)Math.Max(1, Math.Min(desiredSize, heightCap));
                    desiredSize = (float)Math.Round(desiredSize, 1);
                    if (Math.Abs(desiredSize - entry.Font.Size) < 0.06)
                    {
                        entry.Control.Font = entry.Font;
                        if (entry.ScaledFont != null) { entry.ScaledFont.Dispose(); entry.ScaledFont = null; }
                    }
                    else if (entry.ScaledFont == null || Math.Abs(entry.ScaledFont.Size - desiredSize) > 0.06)
                    {
                        var font = new Font(entry.Font.FontFamily, desiredSize, entry.Font.Style, entry.Font.Unit);
                        var previous = entry.ScaledFont;
                        entry.Control.Font = font;
                        entry.ScaledFont = font;
                        if (previous != null) previous.Dispose();
                    }
                }
            }
            finally { ResumeLayout(false); }
            lastLayoutSize = size;
            lastLayoutHasMascot = hasMascot;
        }

        private void AttachPointerEvents(Control control)
        {
            control.MouseDown += PointerDown;
            control.MouseMove += PointerMove;
            control.MouseUp += PointerUp;
            foreach (Control child in control.Controls) AttachPointerEvents(child);
        }

        private void PointerDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            pressScreen = Cursor.Position;
            pressLocation = Location;
            mouseHeld = true;
            dragging = false;
            captureInterrupted = false;
            pointerTimer.Start();
            Capture = true;
        }

        private void PointerMove(object sender, MouseEventArgs e)
        {
            if (!mouseHeld) return;
            if ((Control.MouseButtons & MouseButtons.Left) == 0)
            {
                FinishPointer(false);
                return;
            }
            UpdatePointerPosition(Cursor.Position);
        }

        private void PointerTick(object sender, EventArgs e)
        {
            if (!mouseHeld)
            {
                pointerTimer.Stop();
                return;
            }
            // A NOACTIVATE window must not depend on unrestricted foreground
            // mouse capture. Poll only during this user-initiated gesture.
            if ((Control.MouseButtons & MouseButtons.Left) == 0)
            {
                FinishPointer(false);
                return;
            }
            UpdatePointerPosition(Cursor.Position);
        }

        private void UpdatePointerPosition(Point current)
        {
            if (!dragging)
            {
                Size threshold = SystemInformation.DragSize;
                var clickArea = new Rectangle(pressScreen.X - threshold.Width / 2, pressScreen.Y - threshold.Height / 2, threshold.Width, threshold.Height);
                dragging = !clickArea.Contains(current);
            }
            if (dragging)
            {
                Location = new Point(pressLocation.X + current.X - pressScreen.X, pressLocation.Y + current.Y - pressScreen.Y);
                ClampToScreen();
            }
        }

        private void PointerUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !mouseHeld) return;
            UpdatePointerPosition(Cursor.Position);
            FinishPointer(!captureInterrupted && Bounds.Contains(Cursor.Position));
        }

        private void FinishPointer(bool allowOpen)
        {
            if (!mouseHeld) return;
            bool moved = dragging;
            mouseHeld = dragging = false;
            captureInterrupted = false;
            pointerTimer.Stop();
            Capture = false;
            if (moved)
            {
                ClampToScreen();
                var changed = PositionChangedByUser;
                if (changed != null) changed(this, EventArgs.Empty);
            }
            else if (allowOpen)
            {
                var open = OpenRequested;
                if (open != null) open(this, EventArgs.Empty);
            }
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture && mouseHeld)
            {
                captureInterrupted = true;
                if ((Control.MouseButtons & MouseButtons.Left) == 0) FinishPointer(false);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Color.FromArgb(203, 214, 229)))
                e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, ClientSize.Width - 1), Math.Max(0, ClientSize.Height - 1));
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            bool releaseResources = disposing && !resourcesDisposed;
            if (releaseResources)
            {
                resourcesDisposed = true;
                mouseHeld = dragging = false;
                pointerTimer.Stop();
                pointerTimer.Dispose();
                if (Capture) Capture = false;
                if (MascotPicture != null) MascotPicture.Image = null;
            }
            base.Dispose(disposing);
            if (releaseResources)
            {
                foreach (var entry in layoutEntries)
                    if (entry.ScaledFont != null) { entry.ScaledFont.Dispose(); entry.ScaledFont = null; }
                layoutEntries.Clear();
                foreach (Font font in fonts) font.Dispose();
                fonts.Clear();
            }
        }
    }
}
