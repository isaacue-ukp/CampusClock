using System;
using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace CampusClock
{
    /// <summary>Dark styles that are easier to express as XAML than as code.</summary>
    public static class ThemeResources
    {
        private static bool applied;
        private static ResourceDictionary current;

        public static string ToHex(Color c)
        {
            return string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
        }

        public static string ToHexA(Color c)
        {
            return string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}{3:X2}", c.A, c.R, c.G, c.B);
        }

        public static void Apply()
        {
            if (applied) return;
            applied = true;
            try
            {
                current = (ResourceDictionary)XamlReader.Parse(Xaml());
                if (Application.Current != null)
                {
                    Application.Current.Resources.MergedDictionaries.Add(current);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("加载深色滚动条样式失败（不影响功能）：" + ex.Message);
            }
        }

        /// <summary>
        /// Replaces the accent-bound brushes in place. Controls that reference them through
        /// DynamicResource (text box caret / selection / focus border) update live when the user picks
        /// a new accent colour, without reloading the whole dictionary.
        /// </summary>
        public static void Refresh()
        {
            if (current == null) return;
            try
            {
                current["CC.Accent"] = Brush(Palette.Accent);
                current["CC.AccentSoft"] = Brush(Palette.Alpha(Palette.Accent, 0.45));
                current["CC.AccentFocus"] = Brush(Palette.Alpha(Palette.Accent, 0.75));
            }
            catch (Exception ex)
            {
                Log.Warn("刷新强调色样式失败：" + ex.Message);
            }
        }

        private static SolidColorBrush Brush(Color c)
        {
            SolidColorBrush b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        public static string Xaml()
        {
            string track = ToHex(Palette.Hex("#3A4150"));
            string barBg = ToHex(Palette.Hex("#14171C"));
            string tipBg = ToHex(Palette.Hex("#20252E"));
            string tipBorder = ToHex(Palette.Border);
            string tipFg = ToHex(Palette.TextPrimary);
            return
                "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" " +
                "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">" +
                "<SolidColorBrush x:Key=\"CC.Accent\" Color=\"" + ToHex(Palette.Accent) + "\"/>" +
                "<SolidColorBrush x:Key=\"CC.AccentSoft\" Color=\"" + ToHexA(Palette.Alpha(Palette.Accent, 0.45)) + "\"/>" +
                "<SolidColorBrush x:Key=\"CC.AccentFocus\" Color=\"" + ToHexA(Palette.Alpha(Palette.Accent, 0.75)) + "\"/>" +
                "<Style TargetType=\"{x:Type ScrollBar}\">" +
                "<Setter Property=\"Background\" Value=\"" + barBg + "\"/>" +
                "<Setter Property=\"Width\" Value=\"10\"/>" +
                "<Setter Property=\"MinWidth\" Value=\"10\"/>" +
                "<Setter Property=\"Template\">" +
                "<Setter.Value>" +
                "<ControlTemplate TargetType=\"{x:Type ScrollBar}\">" +
                "<Grid Background=\"{TemplateBinding Background}\">" +
                "<Track x:Name=\"PART_Track\" IsDirectionReversed=\"True\">" +
                "<Track.DecreaseRepeatButton>" +
                "<RepeatButton Command=\"ScrollBar.PageUpCommand\" Opacity=\"0\" Focusable=\"False\" IsTabStop=\"False\"/>" +
                "</Track.DecreaseRepeatButton>" +
                "<Track.IncreaseRepeatButton>" +
                "<RepeatButton Command=\"ScrollBar.PageDownCommand\" Opacity=\"0\" Focusable=\"False\" IsTabStop=\"False\"/>" +
                "</Track.IncreaseRepeatButton>" +
                "<Track.Thumb>" +
                "<Thumb>" +
                "<Thumb.Template>" +
                "<ControlTemplate TargetType=\"{x:Type Thumb}\">" +
                "<Border CornerRadius=\"5\" Background=\"" + track + "\" Margin=\"2,1\"/>" +
                "</ControlTemplate>" +
                "</Thumb.Template>" +
                "</Thumb>" +
                "</Track.Thumb>" +
                "</Track>" +
                "</Grid>" +
                "</ControlTemplate>" +
                "</Setter.Value>" +
                "</Setter>" +
                "</Style>" +
                "<Style TargetType=\"{x:Type ToolTip}\">" +
                "<Setter Property=\"Background\" Value=\"" + tipBg + "\"/>" +
                "<Setter Property=\"Foreground\" Value=\"" + tipFg + "\"/>" +
                "<Setter Property=\"BorderBrush\" Value=\"" + tipBorder + "\"/>" +
                "<Setter Property=\"Padding\" Value=\"10,7\"/>" +
                "<Setter Property=\"FontSize\" Value=\"12\"/>" +
                "</Style>" +
                "<Style TargetType=\"{x:Type TextBox}\">" +
                "<Setter Property=\"Background\" Value=\"" + ToHex(Palette.Hex("#12151A")) + "\"/>" +
                "<Setter Property=\"Foreground\" Value=\"" + ToHex(Palette.TextPrimary) + "\"/>" +
                "<Setter Property=\"BorderBrush\" Value=\"" + ToHex(Palette.Border) + "\"/>" +
                "<Setter Property=\"BorderThickness\" Value=\"1\"/>" +
                "<Setter Property=\"CaretBrush\" Value=\"{DynamicResource CC.Accent}\"/>" +
                "<Setter Property=\"SelectionBrush\" Value=\"{DynamicResource CC.AccentSoft}\"/>" +
                "<Setter Property=\"Padding\" Value=\"9,7\"/>" +
                "<Setter Property=\"FontFamily\" Value=\"Segoe UI, Microsoft YaHei UI, Microsoft YaHei\"/>" +
                "<Setter Property=\"FontSize\" Value=\"13\"/>" +
                "<Setter Property=\"Template\">" +
                "<Setter.Value>" +
                "<ControlTemplate TargetType=\"{x:Type TextBox}\">" +
                "<Border x:Name=\"bd\" CornerRadius=\"9\" Background=\"{TemplateBinding Background}\" " +
                "BorderBrush=\"{TemplateBinding BorderBrush}\" BorderThickness=\"{TemplateBinding BorderThickness}\">" +
                "<ScrollViewer x:Name=\"PART_ContentHost\" Margin=\"{TemplateBinding Padding}\" " +
                "VerticalScrollBarVisibility=\"{TemplateBinding VerticalScrollBarVisibility}\" " +
                "HorizontalScrollBarVisibility=\"{TemplateBinding HorizontalScrollBarVisibility}\"/>" +
                "</Border>" +
                "<ControlTemplate.Triggers>" +
                "<Trigger Property=\"IsKeyboardFocusWithin\" Value=\"True\">" +
                "<Setter TargetName=\"bd\" Property=\"BorderBrush\" Value=\"{DynamicResource CC.AccentFocus}\"/>" +
                "</Trigger>" +
                "</ControlTemplate.Triggers>" +
                "</ControlTemplate>" +
                "</Setter.Value>" +
                "</Setter>" +
                "</Style>" +
                "</ResourceDictionary>";
        }
    }
}
