using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CampusClock
{
    /// <summary>
    /// Timetable rendered on a real time axis: the vertical position and height of every
    /// course is proportional to its start / end clock time, with faint hour grid lines.
    /// </summary>
    public static class TimetableRenderer
    {
        public class Metrics
        {
            public double TimeColWidth;
            public double CellWidth;
            public double HeaderHeight;
            public double FontSize;
            public double PxPerHour;
            public int DayCount;
            public int RangeStartMin;
            public int RangeEndMin;

            public double PxPerMin { get { return PxPerHour / 60.0; } }

            public double BodyHeight
            {
                get { return (RangeEndMin - RangeStartMin) * PxPerMin; }
            }

            public double Y(int minute)
            {
                return (minute - RangeStartMin) * PxPerMin;
            }
        }

        /// <summary>Visible time window of the day, snapped to whole hours.</summary>
        public static void RangeMinutes(Schedule schedule, out int startMin, out int endMin)
        {
            int min = int.MaxValue;
            int max = int.MinValue;
            if (schedule != null && schedule.Doc != null)
            {
                for (int i = 0; i < schedule.Doc.Rules.Count; i++)
                {
                    CourseRule r = schedule.Doc.Rules[i];
                    if (r.StartMin < min) min = r.StartMin;
                    if (r.EndMin > max) max = r.EndMin;
                }
            }
            if (min == int.MaxValue)
            {
                startMin = 8 * 60;
                endMin = 20 * 60;
                return;
            }
            startMin = (min / 60) * 60;
            endMin = ((max + 59) / 60) * 60;
            if (endMin - startMin < 6 * 60) endMin = startMin + 6 * 60;
            if (endMin > 24 * 60) endMin = 24 * 60;
        }

        public static Metrics ComputeMetrics(AppConfig cfg, Schedule schedule, bool compact, double boxW, double boxH)
        {
            Metrics m = new Metrics();
            m.DayCount = cfg.ShowWeekend ? 7 : 5;
            RangeMinutes(schedule, out m.RangeStartMin, out m.RangeEndMin);
            double hours = Math.Max(1.0, (m.RangeEndMin - m.RangeStartMin) / 60.0);
            if (compact)
            {
                m.TimeColWidth = Math.Max(32, boxW * 0.10);
                m.CellWidth = Math.Max(24, (boxW - m.TimeColWidth) / m.DayCount);
                m.HeaderHeight = Math.Max(20, boxH * 0.068);
                m.PxPerHour = Math.Max(13, (boxH - m.HeaderHeight) / hours);
                double fs = Math.Min(m.CellWidth / 6.4, m.PxPerHour / 2.9);
                m.FontSize = Math.Max(8, Math.Min(14.0, fs));
            }
            else
            {
                m.TimeColWidth = 64;
                m.CellWidth = cfg.CellWidth;
                m.HeaderHeight = 38;
                m.PxPerHour = cfg.PxPerHour;
                m.FontSize = cfg.FontSize;
            }
            return m;
        }

        public static FrameworkElement Build(Schedule schedule, AppConfig cfg, DateTime weekStart,
            bool compact, double boxW, double boxH)
        {
            if (schedule == null || schedule.Doc == null || schedule.Doc.Rules.Count == 0) return EmptyState(compact);
            Metrics m = ComputeMetrics(cfg, schedule, compact, boxW, boxH);
            DateTime today = DateTime.Today;
            DateTime now = DateTime.Now;
            int nowMin = now.Hour * 60 + now.Minute;
            int todayIndex = -1;
            for (int d = 0; d < m.DayCount; d++)
            {
                if (weekStart.AddDays(d).Date == today) todayIndex = d;
            }

            // ---------------- day header ----------------
            Canvas header = new Canvas();
            header.Width = m.DayCount * m.CellWidth;
            header.Height = m.HeaderHeight;
            for (int d = 0; d < m.DayCount; d++)
            {
                DateTime date = weekStart.AddDays(d);
                bool isToday = date.Date == today;
                Border head = new Border();
                head.Width = m.CellWidth;
                head.Height = m.HeaderHeight;
                head.Background = Palette.Br(isToday ? Palette.Alpha(Palette.Accent, 0.16) : Colors.Transparent);
                head.BorderBrush = Palette.Br(Palette.BorderSoft);
                head.BorderThickness = new Thickness(0, 0, 1, 1);
                StackPanel sp = new StackPanel();
                sp.VerticalAlignment = VerticalAlignment.Center;
                sp.HorizontalAlignment = HorizontalAlignment.Center;
                TextBlock dayName = Ui.Text(Fmt.WeekDayCN(date.DayOfWeek), m.FontSize + 0.5,
                    isToday ? Palette.Accent : Palette.TextPrimary, FontWeights.SemiBold);
                dayName.HorizontalAlignment = HorizontalAlignment.Center;
                TextBlock dateText = Ui.Text(Fmt.ShortDate(date), Math.Max(8, m.FontSize - 2.5),
                    isToday ? Palette.Alpha(Palette.Accent, 0.85) : Palette.TextMuted);
                dateText.HorizontalAlignment = HorizontalAlignment.Center;
                sp.Children.Add(dayName);
                sp.Children.Add(dateText);
                head.Child = sp;
                Canvas.SetLeft(head, d * m.CellWidth);
                header.Children.Add(head);
            }

            // ---------------- time gutter ----------------
            Canvas gutter = new Canvas();
            gutter.Width = m.TimeColWidth;
            gutter.Height = m.BodyHeight;
            for (int t = m.RangeStartMin; t <= m.RangeEndMin; t += 60)
            {
                TextBlock label = Ui.Text(Fmt.Minutes(t), Math.Max(8, m.FontSize - 2.5), Palette.TextMuted);
                label.HorizontalAlignment = HorizontalAlignment.Right;
                label.Width = m.TimeColWidth - 10;
                label.TextAlignment = TextAlignment.Right;
                Canvas.SetLeft(label, 0);
                Canvas.SetTop(label, m.Y(t) - 8);
                gutter.Children.Add(label);
            }
            if (!compact && m.TimeColWidth >= 54)
            {
                AddSectionLabel(gutter, m, 8 * 60, "上午");
                AddSectionLabel(gutter, m, 12 * 60, "下午");
                AddSectionLabel(gutter, m, 18 * 60, "晚上");
            }

            // ---------------- body ----------------
            Canvas body = new Canvas();
            body.Width = m.DayCount * m.CellWidth;
            body.Height = m.BodyHeight;
            body.Background = Palette.Br(Colors.Transparent);
            body.ClipToBounds = true;
            AddBands(body, m, body.Width);
            AddDaySeparators(body, m);
            AddHourLines(body, m, body.Width);

            List<Session> sessions = schedule.WeekSessions(weekStart);
            for (int i = 0; i < sessions.Count; i++)
            {
                Session s = sessions[i];
                int dayIndex = (int)Math.Round((s.Start.Date - weekStart.Date).TotalDays);
                if (dayIndex < 0 || dayIndex >= m.DayCount) continue;
                double top = m.Y(s.Rule.StartMin);
                double height = Math.Max(20, (s.Rule.EndMin - s.Rule.StartMin) * m.PxPerMin - 3);
                Color color = Palette.CourseColor(s.Rule.ColorIndex);
                Border card = BuildCard(s, m, compact, color, cfg.CardOpacity, height);
                double x = dayIndex * m.CellWidth + 3 + (s.Lane * (m.CellWidth - 6) / Math.Max(1, s.LaneCount));
                double w = (m.CellWidth - 6) / Math.Max(1, s.LaneCount);
                card.Width = w;
                card.Height = height;
                Canvas.SetLeft(card, x);
                Canvas.SetTop(card, top + 1);
                body.Children.Add(card);
            }

            AddNowMarker(body, gutter, m, nowMin, todayIndex);

            // ---------------- compose ----------------
            Grid outer = new Grid();
            outer.ColumnDefinitions.Add(new ColumnDefinition());
            outer.ColumnDefinitions[0].Width = new GridLength(m.TimeColWidth);
            outer.ColumnDefinitions.Add(new ColumnDefinition());
            outer.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            outer.RowDefinitions.Add(new RowDefinition());
            outer.RowDefinitions[0].Height = new GridLength(m.HeaderHeight);
            outer.RowDefinitions.Add(new RowDefinition());
            outer.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);

            Border corner = new Border();
            corner.Background = Palette.Br(Palette.Alpha(Colors.White, 0.02));
            corner.BorderBrush = Palette.Br(Palette.BorderSoft);
            corner.BorderThickness = new Thickness(0, 0, 1, 1);
            TextBlock cornerText = Ui.Text("时间", Math.Max(8, m.FontSize - 2.5), Palette.TextMuted);
            cornerText.HorizontalAlignment = HorizontalAlignment.Center;
            cornerText.VerticalAlignment = VerticalAlignment.Center;
            corner.Child = cornerText;
            outer.Children.Add(corner);

            Border headerClip = new Border();
            headerClip.ClipToBounds = true;
            headerClip.Background = Palette.Br(Palette.Alpha(Colors.White, 0.02));
            headerClip.Child = header;
            Grid.SetColumn(headerClip, 1);
            outer.Children.Add(headerClip);

            Border gutterClip = new Border();
            gutterClip.ClipToBounds = true;
            gutterClip.BorderBrush = Palette.Br(Palette.BorderSoft);
            gutterClip.BorderThickness = new Thickness(0, 0, 1, 0);
            gutterClip.Child = gutter;
            Grid.SetRow(gutterClip, 1);
            outer.Children.Add(gutterClip);

            if (compact)
            {
                Grid.SetRow(body, 1);
                Grid.SetColumn(body, 1);
                outer.Children.Add(body);
            }
            else
            {
                ScrollViewer sv = new ScrollViewer();
                sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                sv.Content = body;
                sv.Padding = new Thickness(0);
                TranslateTransform headerShift = new TranslateTransform();
                header.RenderTransform = headerShift;
                TranslateTransform gutterShift = new TranslateTransform();
                gutter.RenderTransform = gutterShift;
                sv.ScrollChanged += delegate (object sender, ScrollChangedEventArgs e)
                {
                    headerShift.X = -e.HorizontalOffset;
                    gutterShift.Y = -e.VerticalOffset;
                };
                Grid.SetRow(sv, 1);
                Grid.SetColumn(sv, 1);
                outer.Children.Add(sv);
            }
            return outer;
        }

        private static void AddSectionLabel(Canvas gutter, Metrics m, int minute, string text)
        {
            if (minute <= m.RangeStartMin || minute >= m.RangeEndMin) return;
            TextBlock label = Ui.Text(text, Math.Max(8, m.FontSize - 3), Palette.Alpha(Palette.TextMuted, 0.85));
            label.Width = m.TimeColWidth - 10;
            label.TextAlignment = TextAlignment.Right;
            Canvas.SetLeft(label, 0);
            Canvas.SetTop(label, m.Y(minute) + 3);
            gutter.Children.Add(label);
        }

        private static void AddBands(Canvas body, Metrics m, double width)
        {
            double noon = m.Y(12 * 60);
            double evening = m.Y(18 * 60);
            AddBand(body, noon, Math.Min(evening, m.BodyHeight) - noon, Palette.Alpha(Colors.White, 0.022), width);
            AddBand(body, evening, m.BodyHeight - evening, Palette.Alpha(Colors.White, 0.046), width);
        }

        private static void AddBand(Canvas body, double top, double height, Color color, double width)
        {
            if (height <= 0) return;
            Border band = new Border();
            band.Width = width;
            band.Height = height;
            band.Background = Palette.Br(color);
            band.IsHitTestVisible = false;
            Canvas.SetLeft(band, 0);
            Canvas.SetTop(band, top);
            body.Children.Add(band);
        }

        private static void AddHourLines(Canvas body, Metrics m, double width)
        {
            for (int t = m.RangeStartMin; t <= m.RangeEndMin; t += 60)
            {
                bool boundary = (t == 12 * 60 || t == 18 * 60);
                Line line = new Line();
                line.X1 = 0;
                line.X2 = width;
                line.Y1 = m.Y(t);
                line.Y2 = line.Y1;
                line.Stroke = Palette.Br(Palette.Alpha(Colors.White, boundary ? 0.14 : 0.055));
                line.StrokeThickness = boundary ? 1.2 : 1;
                line.StrokeDashArray = new DoubleCollection(boundary
                    ? new double[] { 2.5, 2.5 }
                    : new double[] { 1.2, 3.2 });
                line.IsHitTestVisible = false;
                body.Children.Add(line);
            }
        }

        private static void AddDaySeparators(Canvas body, Metrics m)
        {
            for (int d = 0; d <= m.DayCount; d++)
            {
                Line line = new Line();
                double x = d * m.CellWidth;
                line.X1 = x;
                line.X2 = x;
                line.Y1 = 0;
                line.Y2 = m.BodyHeight;
                line.Stroke = Palette.Br(Palette.Alpha(Colors.White, 0.05));
                line.StrokeThickness = 1;
                line.IsHitTestVisible = false;
                body.Children.Add(line);
            }
        }

        private static void AddNowMarker(Canvas body, Canvas gutter, Metrics m, int nowMin, int todayIndex)
        {
            if (nowMin < m.RangeStartMin || nowMin > m.RangeEndMin) return;
            double y = m.Y(nowMin);

            Line faint = new Line();
            faint.X1 = 0;
            faint.X2 = body.Width;
            faint.Y1 = y;
            faint.Y2 = y;
            faint.Stroke = Palette.Br(Palette.Alpha(Palette.Accent, 0.22));
            faint.StrokeThickness = 1;
            faint.StrokeDashArray = new DoubleCollection(new double[] { 2, 3 });
            faint.IsHitTestVisible = false;
            body.Children.Add(faint);

            if (todayIndex >= 0)
            {
                Line strong = new Line();
                strong.X1 = todayIndex * m.CellWidth + 1;
                strong.X2 = (todayIndex + 1) * m.CellWidth - 1;
                strong.Y1 = y;
                strong.Y2 = y;
                strong.Stroke = Palette.Br(Palette.Alpha(Palette.Accent, 0.95));
                strong.StrokeThickness = 2;
                strong.IsHitTestVisible = false;
                body.Children.Add(strong);

                Ellipse dot = new Ellipse();
                dot.Width = 7;
                dot.Height = 7;
                dot.Fill = Palette.Br(Palette.Accent);
                Canvas.SetLeft(dot, todayIndex * m.CellWidth + 1);
                Canvas.SetTop(dot, y - 3.5);
                dot.IsHitTestVisible = false;
                body.Children.Add(dot);
            }

            Ellipse gutterDot = new Ellipse();
            gutterDot.Width = 5;
            gutterDot.Height = 5;
            gutterDot.Fill = Palette.Br(Palette.Accent);
            Canvas.SetLeft(gutterDot, m.TimeColWidth - 8);
            Canvas.SetTop(gutterDot, y - 2.5);
            gutterDot.IsHitTestVisible = false;
            gutter.Children.Add(gutterDot);
        }

        private static Border BuildCard(Session s, Metrics m, bool compact, Color color, double opacity, double height)
        {
            Border card = new Border();
            card.CornerRadius = new CornerRadius(compact ? 6 : 9);
            card.Background = Palette.Br(Palette.Alpha(color, opacity));
            card.BorderBrush = Palette.Br(Palette.Alpha(color, 0.5));
            card.BorderThickness = new Thickness(1);
            card.ClipToBounds = true;
            card.SnapsToDevicePixels = true;
            card.ToolTip = BuildTooltip(s);

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions[0].Width = new GridLength(compact ? 2.5 : 3.5);
            g.ColumnDefinitions.Add(new ColumnDefinition());
            Border bar = new Border();
            bar.Background = Palette.Br(color);
            bar.CornerRadius = new CornerRadius(2);
            bar.Margin = new Thickness(compact ? 2 : 3, compact ? 3 : 4, 0, compact ? 3 : 4);
            g.Children.Add(bar);

            StackPanel sp = new StackPanel();
            sp.Margin = new Thickness(compact ? 3 : 6, compact ? 2 : 3, compact ? 3 : 5, 2);
            TextBlock title = Ui.Text(s.Course, m.FontSize, Palette.Mix(color, Colors.White, 0.58),
                FontWeights.SemiBold, true);
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            title.LineHeight = m.FontSize * 1.22;
            sp.Children.Add(title);
            bool roomy = height >= m.FontSize * 3.1;
            if (roomy)
            {
                TextBlock time = Ui.Text(s.Rule.TimeText, Math.Max(8, m.FontSize - 2.5), Palette.TextMuted);
                sp.Children.Add(time);
                if (s.PeriodLabel.Length > 0 && height >= m.FontSize * 4.2 && !compact)
                {
                    sp.Children.Add(Ui.Text(s.PeriodLabel, Math.Max(8, m.FontSize - 2.5), Palette.TextMuted));
                }
                if (s.Location.Length > 0 && height >= m.FontSize * 4.2)
                {
                    TextBlock loc = Ui.Text(s.Location, Math.Max(8, m.FontSize - 1.5),
                        Palette.Mix(Palette.TextSecondary, color, 0.25), FontWeights.Normal, true);
                    loc.TextTrimming = TextTrimming.CharacterEllipsis;
                    sp.Children.Add(loc);
                }
            }
            Grid.SetColumn(sp, 1);
            g.Children.Add(sp);
            card.Child = g;
            return card;
        }

        private static object BuildTooltip(Session s)
        {
            StackPanel sp = new StackPanel();
            sp.Children.Add(Ui.Text(s.Course, 13, Palette.TextPrimary, FontWeights.SemiBold));
            string line = Fmt.WeekDayCN(s.Start.DayOfWeek) + " " + Fmt.DateTimeText(s.Start) + " - " +
                s.End.ToString("HH:mm", CultureInfo.InvariantCulture);
            sp.Children.Add(Ui.Text(line, 12, Palette.TextSecondary));
            if (s.Location.Length > 0) sp.Children.Add(Ui.Text(s.Location, 12, Palette.TextSecondary));
            if (s.PeriodLabel.Length > 0) sp.Children.Add(Ui.Text(s.PeriodLabel, 12, Palette.TextMuted));
            return sp;
        }

        public static FrameworkElement EmptyState(bool compact)
        {
            StackPanel sp = new StackPanel();
            sp.VerticalAlignment = VerticalAlignment.Center;
            sp.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Children.Add(Ui.Icon(Glyph.Calendar, compact ? 22 : 34, Palette.TextMuted));
            TextBlock t = Ui.Text("还没有课表", compact ? 12 : 15, Palette.TextSecondary, FontWeights.Medium);
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.Margin = new Thickness(0, 8, 0, 0);
            sp.Children.Add(t);
            TextBlock t2 = Ui.Text("请导入 .ics 文件", compact ? 10.5 : 12.5, Palette.TextMuted);
            t2.HorizontalAlignment = HorizontalAlignment.Center;
            t2.Margin = new Thickness(0, 4, 0, 0);
            sp.Children.Add(t2);
            return sp;
        }
    }
}
