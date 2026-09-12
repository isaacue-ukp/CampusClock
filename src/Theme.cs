using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace CampusClock
{
    public static class Palette
    {
        public static Color WindowBg = Hex("#0F1115");
        public static Color Surface = Hex("#15181E");
        public static Color SurfaceAlt = Hex("#1A1E25");
        public static Color Elevated = Hex("#20252E");
        public static Color Hover = Hex("#232833");
        public static Color Border = Hex("#282E38");
        public static Color BorderSoft = Hex("#1F242C");
        public static Color TextPrimary = Hex("#E7EAF0");
        public static Color TextSecondary = Hex("#A3ADBC");
        public static Color TextMuted = Hex("#6D7787");
        public static Color Good = Hex("#4CC38A");
        public static Color Warn = Hex("#E5A34A");
        public static Color Danger = Hex("#E5534B");
        public static Color Accent = Hex("#6D8DFF");

        public static readonly Color[] CourseColors = new Color[]
        {
            Hex("#6D8DFF"), Hex("#4CC38A"), Hex("#E5A34A"), Hex("#E5738B"), Hex("#A48CF0"),
            Hex("#4BB8C8"), Hex("#D98A5B"), Hex("#7FBF5F"), Hex("#6FA8DC"), Hex("#C77FBF")
        };

        public static FontFamily UiFont = BuildFont();
        public static FontFamily IconFont = new FontFamily("Segoe MDL2 Assets");
        public static FontFamily MonoFont = new FontFamily("Cascadia Mono, Consolas, Courier New");

        private static FontFamily BuildFont()
        {
            try
            {
                return new FontFamily("Segoe UI Variable Text, Segoe UI, Microsoft YaHei UI, Microsoft YaHei, Arial");
            }
            catch
            {
                return new FontFamily("Segoe UI");
            }
        }

        public static void SetAccent(string hex)
        {
            Accent = Hex(hex);
        }

        public static Color Hex(string hex)
        {
            try
            {
                if (string.IsNullOrEmpty(hex)) return Colors.Gray;
                if (hex[0] == '#') hex = hex.Substring(1);
                if (hex.Length == 3)
                {
                    string r = hex.Substring(0, 1);
                    string g = hex.Substring(1, 1);
                    string b = hex.Substring(2, 1);
                    hex = r + r + g + g + b + b;
                }
                if (hex.Length == 6)
                {
                    byte r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    byte g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    byte b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    return Color.FromRgb(r, g, b);
                }
                if (hex.Length == 8)
                {
                    byte a = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    byte r = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    byte g = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    byte b = byte.Parse(hex.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    return Color.FromArgb(a, r, g, b);
                }
            }
            catch
            {
            }
            return Colors.Gray;
        }

        public static Color Alpha(Color c, double a)
        {
            byte v = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(255 * a)));
            return Color.FromArgb(v, c.R, c.G, c.B);
        }

        public static Color Mix(Color a, Color b, double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromRgb(
                (byte)Math.Round(a.R + (b.R - a.R) * t),
                (byte)Math.Round(a.G + (b.G - a.G) * t),
                (byte)Math.Round(a.B + (b.B - a.B) * t));
        }

        public static SolidColorBrush Br(Color c)
        {
            SolidColorBrush b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        public static Color CourseColor(int index)
        {
            if (index < 0) index = 0;
            return CourseColors[index % CourseColors.Length];
        }
    }

    public static class Glyph
    {
        public const string Calendar = "\uE787";
        public const string Checklist = "\uE73A";
        public const string CheckMark = "\uE73E";
        public const string Settings = "\uE713";
        public const string Pin = "\uE718";
        public const string Folder = "\uE8B7";
        public const string Refresh = "\uE72C";
        public const string Close = "\uE8BB";
        public const string Minimize = "\uE921";
        public const string Maximize = "\uE922";
        public const string Restore = "\uE923";
        public const string Download = "\uE896";
        public const string ChevronLeft = "\uE76B";
        public const string ChevronRight = "\uE76C";
        public const string Info = "\uE946";
        public const string Clock = "\uE823";
        public const string Edit = "\uE70F";
        public const string Undo = "\uE7A7";
        public const string Add = "\uE710";
        public const string PinOff = "\uE77A";
        public const string OpenWindow = "\uE8A7";
    }

    public static class Ui
    {
        public static TextBlock Text(string text, double size, Color color)
        {
            return Text(text, size, color, FontWeights.Normal, false);
        }

        public static TextBlock Text(string text, double size, Color color, FontWeight weight)
        {
            return Text(text, size, color, weight, false);
        }

        public static TextBlock Text(string text, double size, Color color, FontWeight weight, bool wrap)
        {
            TextBlock t = new TextBlock();
            t.Text = text;
            t.FontSize = size;
            t.Foreground = Palette.Br(color);
            t.FontWeight = weight;
            t.FontFamily = Palette.UiFont;
            t.TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
            TextOptions.SetTextFormattingMode(t, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(t, TextRenderingMode.ClearType);
            return t;
        }

        public static TextBlock Icon(string glyph, double size, Color color)
        {
            TextBlock t = new TextBlock();
            t.Text = glyph;
            t.FontSize = size;
            t.Foreground = Palette.Br(color);
            t.FontFamily = Palette.IconFont;
            t.VerticalAlignment = VerticalAlignment.Center;
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.TextAlignment = TextAlignment.Center;
            return t;
        }

        public static Border Card(UIElement child, double radius, Color bg, Color border, Thickness padding)
        {
            Border b = new Border();
            b.CornerRadius = new CornerRadius(radius);
            b.Background = Palette.Br(bg);
            b.BorderBrush = Palette.Br(border);
            b.BorderThickness = new Thickness(1);
            b.Padding = padding;
            b.Child = child;
            return b;
        }

        public static Border Panel()
        {
            Border b = new Border();
            b.CornerRadius = new CornerRadius(12);
            b.Background = Palette.Br(Palette.Surface);
            b.BorderBrush = Palette.Br(Palette.BorderSoft);
            b.BorderThickness = new Thickness(1);
            b.Padding = new Thickness(16);
            return b;
        }

        public static Border Divider()
        {
            Border b = new Border();
            b.Height = 1;
            b.Background = Palette.Br(Palette.BorderSoft);
            b.Margin = new Thickness(0, 6, 0, 6);
            return b;
        }

        public static DropShadowEffect Shadow(double blur, double opacity)
        {
            DropShadowEffect e = new DropShadowEffect();
            e.BlurRadius = blur;
            e.ShadowDepth = 2;
            e.Direction = 270;
            e.Opacity = opacity;
            e.Color = Colors.Black;
            return e;
        }

        public static void Animate(UIElement el, double opacity, int ms)
        {
            FrameworkElement fe = el as FrameworkElement;
            if (ms <= 0 || (fe != null && !fe.IsLoaded))
            {
                el.Opacity = opacity;
                return;
            }
            DoubleAnimation a = new DoubleAnimation(opacity, TimeSpan.FromMilliseconds(ms));
            a.EasingFunction = new CubicEase();
            ((CubicEase)a.EasingFunction).EasingMode = EasingMode.EaseOut;
            el.BeginAnimation(UIElement.OpacityProperty, a);
        }
    }

    /// <summary>Flat icon button with hover feedback.</summary>
    public class IconBtn : Border
    {
        private TextBlock icon;
        private Color baseBg = Colors.Transparent;

        public event EventHandler Clicked;

        public IconBtn(string glyph, double glyphSize, Color fg, double box)
        {
            Width = box;
            Height = box;
            CornerRadius = new CornerRadius(8);
            Background = Palette.Br(Colors.Transparent);
            Cursor = Cursors.Hand;
            icon = Ui.Icon(glyph, glyphSize, fg);
            Child = icon;
            MouseEnter += delegate { if (IsEnabled) Background = Palette.Br(Palette.Hover); };
            MouseLeave += delegate { Background = Palette.Br(baseBg); };
            MouseLeftButtonDown += delegate (object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                if (Clicked != null) Clicked(this, EventArgs.Empty);
            };
        }

        public void SetGlyph(string glyph)
        {
            icon.Text = glyph;
        }

        public void SetForeground(Color c)
        {
            icon.Foreground = Palette.Br(c);
        }
    }

    /// <summary>Pill shaped text button.</summary>
    public class TextBtn : Border
    {
        private TextBlock label;
        private Color bg;
        private Color bgHover;
        private Color fg;
        private Color borderColor;

        public event EventHandler Clicked;

        public static TextBtn Primary(string text, Color accent)
        {
            TextBtn b = new TextBtn(text, Palette.Alpha(accent, 0.18), Palette.Alpha(accent, 0.30), accent, Palette.Alpha(accent, 0.45));
            return b;
        }

        public static TextBtn Ghost(string text)
        {
            TextBtn b = new TextBtn(text, Colors.Transparent, Palette.Hover, Palette.TextSecondary, Palette.Border);
            return b;
        }

        public TextBtn(string text, Color background, Color backgroundHover, Color foreground, Color borderCol)
        {
            label = Ui.Text(text, 12.5, foreground, FontWeights.Medium);
            label.VerticalAlignment = VerticalAlignment.Center;
            Child = label;
            Padding = new Thickness(14, 7, 14, 7);
            CornerRadius = new CornerRadius(9);
            bg = background;
            bgHover = backgroundHover;
            fg = foreground;
            borderColor = borderCol;
            Background = Palette.Br(bg);
            BorderBrush = Palette.Br(borderColor);
            BorderThickness = new Thickness(1);
            Cursor = Cursors.Hand;
            MouseEnter += delegate { Background = Palette.Br(bgHover); };
            MouseLeave += delegate { Background = Palette.Br(bg); };
            MouseLeftButtonUp += delegate (object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                if (Clicked != null) Clicked(this, EventArgs.Empty);
            };
        }

        public void SetText(string text)
        {
            label.Text = text;
        }

        public void SetAccent(Color accent)
        {
            label.Foreground = Palette.Br(accent);
            bg = Palette.Alpha(accent, 0.18);
            bgHover = Palette.Alpha(accent, 0.30);
            borderColor = Palette.Alpha(accent, 0.45);
            Background = Palette.Br(bg);
            BorderBrush = Palette.Br(borderColor);
        }
    }

    /// <summary>Selectable tag / chip used for choosing courses.</summary>
    public class Chip : Border
    {
        private TextBlock label;
        private TextBlock tick;
        private bool selected;
        private string value = "";

        public event EventHandler Toggled;

        public string Value { get { return value; } }

        public Chip(string text)
        {
            value = text;
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition());
            label = Ui.Text(text, 12.5, Palette.TextSecondary, FontWeights.Medium);
            label.VerticalAlignment = VerticalAlignment.Center;
            tick = Ui.Icon(Glyph.CheckMark, 11, Palette.WindowBg);
            Grid.SetColumn(tick, 1);
            tick.Margin = new Thickness(6, 0, 0, 0);
            tick.Visibility = Visibility.Collapsed;
            g.Children.Add(label);
            g.Children.Add(tick);
            Child = g;
            Padding = new Thickness(12, 6, 12, 6);
            CornerRadius = new CornerRadius(20);
            BorderThickness = new Thickness(1);
            Cursor = Cursors.Hand;
            Apply();
            MouseLeftButtonUp += delegate (object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                IsSelected = !IsSelected;
                if (Toggled != null) Toggled(this, EventArgs.Empty);
            };
            MouseEnter += delegate { if (!selected) Background = Palette.Br(Palette.Hover); };
            MouseLeave += delegate { if (!selected) Background = Palette.Br(Colors.Transparent); };
        }

        public bool IsSelected
        {
            get { return selected; }
            set
            {
                selected = value;
                Apply();
            }
        }

        private void Apply()
        {
            tick.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
            if (selected)
            {
                Background = Palette.Br(Palette.Alpha(Palette.Accent, 0.18));
                BorderBrush = Palette.Br(Palette.Alpha(Palette.Accent, 0.45));
                label.Foreground = Palette.Br(Palette.Accent);
                tick.Foreground = Palette.Br(Palette.Accent);
            }
            else
            {
                Background = Palette.Br(Colors.Transparent);
                BorderBrush = Palette.Br(Palette.Border);
                label.Foreground = Palette.Br(Palette.TextSecondary);
            }
        }
    }

    /// <summary>Modern minimal slider (custom drawn, no default WPF chrome).</summary>
    public class MiniSlider : Grid
    {
        private Canvas canvas = new Canvas();
        private Border track;
        private Border fill;
        private Ellipse thumb;
        private bool dragging;

        public double MinValue = 0;
        public double MaxValue = 100;
        public double Step = 1;

        public event EventHandler ValueChanged;

        private double value;

        public double Value
        {
            get { return value; }
            set
            {
                double v = Math.Max(MinValue, Math.Min(MaxValue, value));
                if (Step > 0) v = Math.Round(v / Step) * Step;
                if (Math.Abs(v - this.value) < 0.0001) return;
                this.value = v;
                Layout();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        public MiniSlider(double min, double max, double initial, double step)
        {
            MinValue = min;
            MaxValue = max;
            Step = step;
            Height = 22;
            Cursor = Cursors.Hand;
            track = new Border();
            track.Height = 4;
            track.CornerRadius = new CornerRadius(2);
            track.Background = Palette.Br(Palette.Border);
            track.VerticalAlignment = VerticalAlignment.Center;
            fill = new Border();
            fill.Height = 4;
            fill.CornerRadius = new CornerRadius(2);
            fill.Background = Palette.Br(Palette.Accent);
            fill.VerticalAlignment = VerticalAlignment.Center;
            fill.HorizontalAlignment = HorizontalAlignment.Left;
            thumb = new Ellipse();
            thumb.Width = 14;
            thumb.Height = 14;
            thumb.Fill = Palette.Br(Palette.TextPrimary);
            thumb.VerticalAlignment = VerticalAlignment.Center;
            canvas.Children.Add(track);
            canvas.Children.Add(fill);
            canvas.Children.Add(thumb);
            Children.Add(canvas);
            value = Math.Max(min, Math.Min(max, initial));
            SizeChanged += delegate { Layout(); };
            Loaded += delegate { Layout(); };
            MouseLeftButtonDown += OnDown;
            MouseMove += OnMove;
            MouseLeftButtonUp += OnUp;
            MouseEnter += delegate { thumb.Fill = Palette.Br(Colors.White); };
            MouseLeave += delegate { if (!dragging) thumb.Fill = Palette.Br(Palette.TextPrimary); };
        }

        private void OnDown(object sender, MouseButtonEventArgs e)
        {
            dragging = true;
            CaptureMouse();
            SetFromPoint(e.GetPosition(this).X);
        }

        private void OnMove(object sender, MouseEventArgs e)
        {
            if (!dragging) return;
            SetFromPoint(e.GetPosition(this).X);
        }

        private void OnUp(object sender, MouseButtonEventArgs e)
        {
            if (!dragging) return;
            dragging = false;
            ReleaseMouseCapture();
            thumb.Fill = Palette.Br(Palette.TextPrimary);
        }

        private void SetFromPoint(double x)
        {
            double w = ActualWidth > 1 ? ActualWidth : Width;
            if (w <= 1) return;
            double ratio = (x - 7) / (w - 14);
            ratio = Math.Max(0, Math.Min(1, ratio));
            Value = MinValue + ratio * (MaxValue - MinValue);
        }

        public void Refresh()
        {
            Layout();
        }

        private void Layout()
        {
            double w = ActualWidth;
            if (w <= 1) return;
            double ratio = (value - MinValue) / Math.Max(0.0001, MaxValue - MinValue);
            double tw = Math.Max(0, w - 14);
            Canvas.SetLeft(track, 0);
            Canvas.SetTop(track, Height / 2 - 2);
            track.Width = w;
            Canvas.SetLeft(fill, 0);
            Canvas.SetTop(fill, Height / 2 - 2);
            fill.Width = Math.Max(2, ratio * w);
            Canvas.SetLeft(thumb, ratio * tw);
            Canvas.SetTop(thumb, Height / 2 - 7);
        }
    }

    /// <summary>iOS style toggle switch.</summary>
    public class Switch : Grid
    {
        private Border track;
        private Ellipse thumb;
        private bool isOn;
        private bool ready = false;

        public event EventHandler Toggled;

        public bool IsOn
        {
            get { return isOn; }
            set
            {
                if (isOn == value) return;
                isOn = value;
                Apply(true);
                if (ready && Toggled != null) Toggled(this, EventArgs.Empty);
            }
        }

        public Switch(bool initial)
        {
            Width = 42;
            Height = 24;
            Cursor = Cursors.Hand;
            track = new Border();
            track.Width = 42;
            track.Height = 24;
            track.CornerRadius = new CornerRadius(12);
            thumb = new Ellipse();
            thumb.Width = 18;
            thumb.Height = 18;
            thumb.Fill = Palette.Br(Colors.White);
            thumb.HorizontalAlignment = HorizontalAlignment.Left;
            thumb.VerticalAlignment = VerticalAlignment.Center;
            childrenAdd();
            isOn = initial;
            Apply(false);
            MouseLeftButtonUp += delegate (object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                isOn = !isOn;
                Apply(true);
                if (Toggled != null) Toggled(this, EventArgs.Empty);
            };
            ready = true;
        }

        private void childrenAdd()
        {
            Children.Add(track);
            Children.Add(thumb);
        }

        private void Apply(bool animate)
        {
            track.Background = Palette.Br(isOn ? Palette.Accent : Palette.Hex("#2C313B"));
            double left = isOn ? 21 : 3;
            Thickness target = new Thickness(left, 0, 0, 0);
            if (animate)
            {
                ThicknessAnimation a = new ThicknessAnimation(target, TimeSpan.FromMilliseconds(140));
                a.EasingFunction = new CubicEase();
                ((CubicEase)a.EasingFunction).EasingMode = EasingMode.EaseOut;
                thumb.BeginAnimation(Ellipse.MarginProperty, a);
            }
            else
            {
                thumb.Margin = target;
            }
        }
    }
}
