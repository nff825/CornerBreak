using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CornerBreak
{
    // Retain immutable, DPI-adjusted geometry. Repeated growth never scales a
    // previous frame, and each control owns at most one replacement font.
    public sealed class GrowthSurface : IDisposable
    {
        private sealed class Entry
        {
            public Control Control;
            public Rectangle Bounds;
            public Padding Margin;
            public Padding Padding;
            public Font Font;
            public Font ScaledFont;
        }

        private readonly Form form;
        private readonly FlowLayoutPanel root;
        private readonly List<Entry> entries = new List<Entry>();
        private readonly Padding rootPadding;
        private readonly int frameWidth;
        private readonly int frameHeight;
        private bool applied;
        private Size lastSize;
        private Size lastBaseline;
        public bool MascotVisible { get; set; }

        public GrowthSurface(Form form, FlowLayoutPanel root)
        {
            this.form = form;
            this.root = root;
            rootPadding = root.Padding;
            frameWidth = form.Width - form.ClientSize.Width;
            frameHeight = form.Height - form.ClientSize.Height;
            Capture(root);
            form.AutoSize = false;
            form.AutoScaleMode = AutoScaleMode.None;
            root.AutoSize = false;
        }

        private void Capture(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                entries.Add(new Entry { Control = child, Bounds = child.Bounds,
                    Margin = child.Margin, Padding = child.Padding, Font = child.Font });
                Capture(child);
            }
        }

        public Size Measure(bool eyes, bool water, bool move, bool note)
        {
            int width = 1, height = rootPadding.Vertical;
            foreach (var entry in entries)
            {
                if (entry.Control.Parent != root) continue;
                string name = entry.Control.Name;
                if ((name == "Eyes" && !eyes) || (name == "Water" && !water) ||
                    (name == "Move" && !move) || (name == "Note" && !note) ||
                    (name == "Mascot" && !MascotVisible) || (name == "Brand" && MascotVisible)) continue;
                height += entry.Bounds.Height + entry.Margin.Vertical;
                width = Math.Max(width, entry.Bounds.Width + entry.Margin.Horizontal);
            }
            return new Size(width + rootPadding.Horizontal + frameWidth, height + frameHeight);
        }

        private static int Pixel(double value) { return Math.Max(0, (int)Math.Floor(value + 0.00001)); }
        private static Padding ScalePadding(Padding value, double x, double y)
        {
            return new Padding(Pixel(value.Left*x), Pixel(value.Top*y), Pixel(value.Right*x), Pixel(value.Bottom*y));
        }

        public void Apply(Rectangle bounds, Size baseline)
        {
            // Most 200 ms ticks do not cross a pixel boundary. Avoid relayout
            // and font assignments on those frames or while at normal size.
            if (applied && form.Bounds == bounds && lastSize == bounds.Size && lastBaseline == baseline) return;
            double x = Math.Max(1, bounds.Width-frameWidth) / (double)Math.Max(1, baseline.Width-frameWidth);
            double y = Math.Max(1, bounds.Height-frameHeight) / (double)Math.Max(1, baseline.Height-frameHeight);
            double fontScale = Math.Min(x,y);
            form.SuspendLayout();
            root.SuspendLayout();
            foreach (var entry in entries) entry.Control.SuspendLayout();
            try
            {
                form.Bounds = bounds;
                root.Padding = ScalePadding(rootPadding,x,y);
                foreach (var entry in entries)
                {
                    var control = entry.Control;
                    control.Margin = ScalePadding(entry.Margin,x,y);
                    control.Padding = ScalePadding(entry.Padding,x,y);
                    control.Size = new Size(Math.Max(1,Pixel(entry.Bounds.Width*x)),Math.Max(1,Pixel(entry.Bounds.Height*y)));
                    if (control.Parent != root)
                        control.Location = new Point(Pixel(entry.Bounds.X*x),Pixel(entry.Bounds.Y*y));
                    float size = (float)Math.Max(4,Math.Round(entry.Font.Size*fontScale,1));
                    if (Math.Abs(size-entry.Font.Size) < 0.06)
                    {
                        control.Font = entry.Font;
                        if (entry.ScaledFont != null) { entry.ScaledFont.Dispose(); entry.ScaledFont = null; }
                    }
                    else if (entry.ScaledFont == null || Math.Abs(size-entry.ScaledFont.Size) > 0.06)
                    {
                        var next = new Font(entry.Font.FontFamily,size,entry.Font.Style,entry.Font.Unit);
                        var previous = entry.ScaledFont;
                        control.Font = next;
                        entry.ScaledFont = next;
                        if (previous != null) previous.Dispose();
                    }
                }
            }
            finally
            {
                foreach (var entry in entries) entry.Control.ResumeLayout(false);
                root.ResumeLayout(true);
                form.ResumeLayout(true);
            }
            applied = true;
            lastSize = bounds.Size;
            lastBaseline = baseline;
        }

        public void Dispose()
        {
            foreach (var entry in entries)
            {
                if (entry.ScaledFont != null) { entry.ScaledFont.Dispose(); entry.ScaledFont = null; }
            }
        }
    }
}
