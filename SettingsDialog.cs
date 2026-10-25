using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CornerBreak
{
    public sealed class SettingsDialog : Form
    {
        private readonly BreakSettings original;
        private readonly MascotCatalog mascots;
        private readonly List<Font> fonts = new List<Font>();
        private bool resourcesDisposed;
        public Dictionary<string, CheckBox> Checks { get; private set; }
        public CheckBox ExpandCheck { get; private set; }
        public ComboBox MascotChoice { get; private set; }
        public PictureBox MascotPreview { get; private set; }

        public SettingsDialog(BreakSettings settings, MascotCatalog catalog)
        {
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.None;
            original = settings.Clone();
            mascots = catalog;
            Text = "CornerBreak · 提醒设置";
            Font = NewFont(9);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(400, 482);
            BackColor = Color.FromArgb(246, 248, 252);

            AddLabel("选择想收到的提醒", new Rectangle(20, 14, 360, 35), 14, false);
            AddLabel("休息伙伴", new Rectangle(22, 57, 220, 25), 9, false);
            MascotChoice = new ComboBox {
                Name = "MascotChoice", AccessibleName = "休息伙伴",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(22, 88), Size = new Size(220, 30)
            };
            MascotPreview = new PictureBox {
                Name = "MascotPreview", SizeMode = PictureBoxSizeMode.Zoom,
                Bounds = new Rectangle(258, 52, 120, 98), TabStop = false
            };
            Controls.Add(MascotPreview);
            Controls.Add(MascotChoice);
            int selected = 0;
            for (int i = 0; i < MascotCatalog.Options.Length; i++)
            {
                MascotChoice.Items.Add(MascotCatalog.Options[i].Label);
                if (MascotCatalog.Options[i].Id == settings.Mascot) selected = i;
            }
            MascotChoice.SelectedIndexChanged += delegate { UpdateMascotPreview(); };
            MascotChoice.SelectedIndex = selected;

            Checks = new Dictionary<string, CheckBox>();
            AddCheck("Eyes", "眼睛休息 — 离开屏幕，看看远处", 162, settings.Eyes);
            AddCheck("Water", "喝水提醒 — 工作间隙喝点水", 205, settings.Water);
            AddCheck("Move", "起身活动 — 离开工位，走动一下", 248, settings.Move);
            ExpandCheck = new CheckBox {
                Name = "AutoExpand", Text = "随倒计时逐渐放大",
                Bounds = new Rectangle(22, 301, 356, 36), Checked = settings.AutoExpand
            };
            Controls.Add(ExpandCheck);
            AddLabel("共用每小时 50 分至整点的提醒时段。\r\n放大时文字也会变大，鼠标停留时暂停。\r\n全部项目关闭后不自动提醒。",
                new Rectangle(22, 347, 356, 60), 9, true);
            var save = NewButton("保存", new Point(146, 427), DialogResult.OK);
            var cancel = NewButton("取消", new Point(268, 427), DialogResult.Cancel);
            AcceptButton = save;
            CancelButton = cancel;
            // Apply system DPI once, after every 96-DPI rectangle exists.
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            ResumeLayout(false);
            PerformAutoScale();
            PerformLayout();
        }

        private Font NewFont(float size)
        {
            var font = new Font("Microsoft YaHei UI", size);
            fonts.Add(font);
            return font;
        }

        private void AddLabel(string text, Rectangle bounds, float size, bool muted)
        {
            Controls.Add(new Label {
                Text = text, Bounds = bounds, Font = NewFont(size),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = muted ? Color.FromArgb(103, 117, 137) : Color.FromArgb(39, 53, 75)
            });
        }

        private void AddCheck(string name, string text, int y, bool value)
        {
            var check = new CheckBox { Name = name, Text = text,
                Bounds = new Rectangle(22, y, 356, 36), Checked = value };
            Checks.Add(name, check);
            Controls.Add(check);
        }

        private Button NewButton(string text, Point location, DialogResult result)
        {
            var button = new Button {
                Text = text, Size = new Size(110, 36), Location = location,
                Font = NewFont(9), FlatStyle = FlatStyle.Flat, BackColor = Color.White,
                ForeColor = Color.FromArgb(46, 75, 116), UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand, DialogResult = result
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(203, 214, 229);
            Controls.Add(button);
            return button;
        }

        private void UpdateMascotPreview()
        {
            Bitmap image;
            int index = MascotChoice.SelectedIndex;
            MascotPreview.Image = index >= 0 && mascots.Images.TryGetValue(MascotCatalog.Options[index].Id, out image) ? image : null;
        }

        // Edits are isolated until the owner explicitly saves this candidate.
        public BreakSettings BuildCandidate()
        {
            var candidate = original.Clone();
            candidate.Eyes = Checks["Eyes"].Checked;
            candidate.Water = Checks["Water"].Checked;
            candidate.Move = Checks["Move"].Checked;
            candidate.AutoExpand = ExpandCheck.Checked;
            candidate.Mascot = MascotCatalog.Options[Math.Max(0, MascotChoice.SelectedIndex)].Id;
            return candidate;
        }

        protected override void Dispose(bool disposing)
        {
            bool releaseResources = disposing && !resourcesDisposed;
            if (releaseResources)
            {
                resourcesDisposed = true;
                if (MascotPreview != null) MascotPreview.Image = null;
            }
            base.Dispose(disposing);
            if (releaseResources) { foreach (var font in fonts) font.Dispose(); fonts.Clear(); }
        }
    }
}
