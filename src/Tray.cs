using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CampusClock
{
    public class TrayIcon : IDisposable
    {
        private NotifyIcon ni;
        private ToolStripMenuItem ballItem;
        private Icon icon;

        public event EventHandler OpenRequested;
        public event EventHandler ImportRequested;
        public event EventHandler BallToggleRequested;
        public event EventHandler ExitRequested;

        public TrayIcon(string tooltip)
        {
            icon = CreateIcon(32);
            ni = new NotifyIcon();
            ni.Icon = icon;
            ni.Text = tooltip;
            ni.Visible = true;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Renderer = new DarkRenderer();
            menu.ShowImageMargin = false;
            menu.Font = new Font("Microsoft YaHei UI", 9f);

            ToolStripMenuItem open = new ToolStripMenuItem("打开主窗口");
            open.Click += delegate { if (OpenRequested != null) OpenRequested(this, EventArgs.Empty); };
            ToolStripMenuItem import = new ToolStripMenuItem("导入课表 (.ics)");
            import.Click += delegate { if (ImportRequested != null) ImportRequested(this, EventArgs.Empty); };
            ballItem = new ToolStripMenuItem("显示悬浮球");
            ballItem.Click += delegate { if (BallToggleRequested != null) BallToggleRequested(this, EventArgs.Empty); };
            ToolStripMenuItem farewell = new ToolStripMenuItem("退出 CampusClock");
            farewell.Click += delegate { if (ExitRequested != null) ExitRequested(this, EventArgs.Empty); };

            menu.Items.Add(open);
            menu.Items.Add(import);
            menu.Items.Add(ballItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(farewell);
            ni.ContextMenuStrip = menu;
            ni.DoubleClick += delegate { if (OpenRequested != null) OpenRequested(this, EventArgs.Empty); };
        }

        public void SetBallEnabled(bool enabled)
        {
            if (ballItem != null) ballItem.Checked = enabled;
        }

        public void ShowBalloon(string title, string text)
        {
            try
            {
                ni.BalloonTipTitle = title;
                ni.BalloonTipText = text;
                ni.ShowBalloonTip(4000);
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            try
            {
                if (ni != null)
                {
                    ni.Visible = false;
                    ni.Dispose();
                    ni = null;
                }
                if (icon != null)
                {
                    icon.Dispose();
                    icon = null;
                }
            }
            catch
            {
            }
        }

        /// <summary>Draw the app icon at runtime (rounded square + timetable grid + check).</summary>
        public static Icon CreateIcon(int size)
        {
            Bitmap bmp = DrawIconBitmap(size);
            IntPtr h = bmp.GetHicon();
            try
            {
                using (Icon temp = Icon.FromHandle(h))
                {
                    return (Icon)temp.Clone();
                }
            }
            finally
            {
                DestroyIcon(h);
                bmp.Dispose();
            }
        }

        public static Bitmap DrawIconBitmap(int size)
        {
            Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                Rectangle rect = new Rectangle(1, 1, size - 2, size - 2);
                using (GraphicsPath path = RoundedRect(rect, size * 0.28f))
                {
                    Color accent = Color.FromArgb(255, Palette.Accent.R, Palette.Accent.G, Palette.Accent.B);
                    using (LinearGradientBrush bg = new LinearGradientBrush(rect,
                        Color.FromArgb(255, Math.Min(255, accent.R + 26), Math.Min(255, accent.G + 26), Math.Min(255, accent.B + 26)),
                        Color.FromArgb(255, (int)(accent.R * 0.62), (int)(accent.G * 0.62), (int)(accent.B * 0.68)), 45f))
                    {
                        g.FillPath(bg, path);
                    }
                }
                float pad = size * 0.24f;
                float inner = size - pad * 2;
                using (Pen p = new Pen(Color.FromArgb(235, 255, 255, 255), Math.Max(1f, size * 0.055f)))
                {
                    g.DrawLine(p, pad, pad + inner * 0.30f, size - pad, pad + inner * 0.30f);
                    g.DrawLine(p, pad + inner * 0.34f, pad, pad + inner * 0.34f, size - pad * 0.72f);
                    g.DrawLine(p, pad, pad + inner * 0.64f, size - pad, pad + inner * 0.64f);
                }
                float r = size * 0.30f;
                RectangleF dot = new RectangleF(size - r - size * 0.06f, size - r - size * 0.06f, r, r);
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(255, 76, 195, 138)))
                {
                    g.FillEllipse(sb, dot);
                }
                using (Pen check = new Pen(Color.White, Math.Max(1.2f, size * 0.075f)))
                {
                    check.StartCap = LineCap.Round;
                    check.EndCap = LineCap.Round;
                    g.DrawLines(check, new PointF[]
                    {
                        new PointF(dot.Left + dot.Width * 0.26f, dot.Top + dot.Height * 0.52f),
                        new PointF(dot.Left + dot.Width * 0.44f, dot.Top + dot.Height * 0.70f),
                        new PointF(dot.Left + dot.Width * 0.76f, dot.Top + dot.Height * 0.32f)
                    });
                }
            }
            return bmp;
        }

        /// <summary>Write a multi-size .ico (PNG entries) used for the exe icon and shortcuts.</summary>
        public static void WriteIco(string path)
        {
            int[] sizes = new int[] { 16, 24, 32, 48, 64, 128, 256 };
            List<byte[]> blobs = new List<byte[]>();
            for (int i = 0; i < sizes.Length; i++)
            {
                using (Bitmap bmp = DrawIconBitmap(sizes[i]))
                using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    blobs.Add(ms.ToArray());
                }
            }
            using (System.IO.FileStream fs = new System.IO.FileStream(path, System.IO.FileMode.Create,
                System.IO.FileAccess.Write))
            using (System.IO.BinaryWriter w = new System.IO.BinaryWriter(fs))
            {
                w.Write((short)0);
                w.Write((short)1);
                w.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)0);
                    w.Write((byte)0);
                    w.Write((short)1);
                    w.Write((short)32);
                    w.Write(blobs[i].Length);
                    w.Write(offset);
                    offset += blobs[i].Length;
                }
                for (int i = 0; i < blobs.Count; i++) w.Write(blobs[i]);
            }
        }

        public static GraphicsPath RoundedRect(Rectangle rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = radius * 2;
            if (d <= 0) d = 1;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);

        private class DarkRenderer : ToolStripProfessionalRenderer
        {
            public DarkRenderer()
                : base(new DarkColors())
            {
                RoundedEdges = false;
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = Color.FromArgb(255, 231, 234, 240);
                base.OnRenderItemText(e);
            }
        }

        private class DarkColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground
            {
                get { return Color.FromArgb(255, 26, 30, 37); }
            }

            public override Color MenuBorder
            {
                get { return Color.FromArgb(255, 44, 50, 60); }
            }

            public override Color MenuItemBorder
            {
                get { return Color.FromArgb(255, 58, 66, 80); }
            }

            public override Color MenuItemSelected
            {
                get { return Color.FromArgb(255, 42, 48, 60); }
            }

            public override Color MenuItemSelectedGradientBegin
            {
                get { return Color.FromArgb(255, 42, 48, 60); }
            }

            public override Color MenuItemSelectedGradientEnd
            {
                get { return Color.FromArgb(255, 42, 48, 60); }
            }

            public override Color SeparatorDark
            {
                get { return Color.FromArgb(255, 40, 46, 56); }
            }

            public override Color SeparatorLight
            {
                get { return Color.FromArgb(255, 40, 46, 56); }
            }

            public override Color ImageMarginGradientBegin
            {
                get { return Color.FromArgb(255, 26, 30, 37); }
            }

            public override Color ImageMarginGradientMiddle
            {
                get { return Color.FromArgb(255, 26, 30, 37); }
            }

            public override Color ImageMarginGradientEnd
            {
                get { return Color.FromArgb(255, 26, 30, 37); }
            }
        }
    }
}
