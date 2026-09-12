using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CampusClock
{
    public class TimetablePage : Grid
    {
        private AppCore core;
        private int weekOffset;
        private TextBlock weekTitle;
        private TextBlock weekRange;
        private ContentControl gridHost;

        public TimetablePage(AppCore core)
        {
            this.core = core;
            RowDefinitions.Add(new RowDefinition());
            RowDefinitions[0].Height = GridLength.Auto;
            RowDefinitions.Add(new RowDefinition());
            RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);

            Grid bar = new Grid();
            bar.Margin = new Thickness(0, 0, 0, 14);
            bar.ColumnDefinitions.Add(new ColumnDefinition());
            bar.ColumnDefinitions.Add(new ColumnDefinition());
            bar.ColumnDefinitions[1].Width = GridLength.Auto;

            StackPanel left = new StackPanel();
            weekTitle = Ui.Text("第 1 周", 21, Palette.TextPrimary, FontWeights.SemiBold);
            weekRange = Ui.Text("", 12.5, Palette.TextMuted);
            weekRange.Margin = new Thickness(0, 3, 0, 0);
            left.Children.Add(weekTitle);
            left.Children.Add(weekRange);
            bar.Children.Add(left);

            StackPanel right = new StackPanel();
            right.Orientation = Orientation.Horizontal;
            right.VerticalAlignment = VerticalAlignment.Center;
            IconBtn prev = new IconBtn(Glyph.ChevronLeft, 12, Palette.TextSecondary, 30);
            prev.Margin = new Thickness(0, 0, 6, 0);
            prev.ToolTip = "上一周";
            prev.Clicked += delegate { SetWeekOffset(weekOffset - 1); };
            TextBtn today = TextBtn.Ghost("回到本周");
            today.Margin = new Thickness(0, 0, 6, 0);
            today.Clicked += delegate { SetWeekOffset(0); };
            IconBtn next = new IconBtn(Glyph.ChevronRight, 12, Palette.TextSecondary, 30);
            next.ToolTip = "下一周";
            next.Clicked += delegate { SetWeekOffset(weekOffset + 1); };
            right.Children.Add(prev);
            right.Children.Add(today);
            right.Children.Add(next);
            Grid.SetColumn(right, 1);
            bar.Children.Add(right);

            Children.Add(bar);

            gridHost = new ContentControl();
            gridHost.HorizontalContentAlignment = HorizontalAlignment.Left;
            gridHost.VerticalContentAlignment = VerticalAlignment.Top;
            Grid.SetRow(gridHost, 1);
            Children.Add(gridHost);
            Rebuild();
        }

        public int WeekOffset { get { return weekOffset; } }

        public void SetWeekOffset(int value)
        {
            weekOffset = value;
            Rebuild();
        }

        public DateTime CurrentWeekStart
        {
            get { return Schedule.WeekStart(DateTime.Today).AddDays(weekOffset * 7); }
        }

        public void Rebuild()
        {
            DateTime weekStart = CurrentWeekStart;
            int index = core.Schedule != null ? core.Schedule.WeekIndex(weekStart) : 1;
            if (index >= 1) weekTitle.Text = "第 " + index.ToString(CultureInfo.InvariantCulture) + " 周";
            else weekTitle.Text = "开学前 " + (1 - index).ToString(CultureInfo.InvariantCulture) + " 周";
            string range = Fmt.ShortDate(weekStart) + " ~ " + Fmt.ShortDate(weekStart.AddDays(6));
            if (DateTime.Today >= weekStart && DateTime.Today < weekStart.AddDays(7)) range += "  ·  本周";
            if (weekOffset != 0) range += "  （非本周）";
            weekRange.Text = range;

            double boxW = ActualWidth > 200 ? ActualWidth - 8 : 900;
            double boxH = ActualHeight > 200 ? ActualHeight - 70 : 520;
            FrameworkElement grid = TimetableRenderer.Build(core.Schedule, core.Config, weekStart, false, boxW, boxH);
            gridHost.Content = grid;
        }

        public void RefreshMetrics()
        {
            Rebuild();
        }
    }

    /// <summary>Checkbox drawn as a circle, used to mark homework as done.</summary>
    public class TickCircle : Border
    {
        public event EventHandler Clicked;
        private TextBlock tick;
        private bool done;

        public bool IsDone
        {
            get { return done; }
            set { done = value; Refresh(); }
        }

        public TickCircle()
        {
            Width = 24;
            Height = 24;
            CornerRadius = new CornerRadius(12);
            BorderThickness = new Thickness(2);
            Background = Palette.Br(Colors.Transparent);
            Cursor = Cursors.Hand;
            tick = Ui.Icon(Glyph.CheckMark, 12, Colors.White);
            tick.Visibility = Visibility.Collapsed;
            Child = tick;
            Refresh();
            MouseEnter += delegate
            {
                if (!done) BorderBrush = Palette.Br(Palette.Alpha(Palette.Accent, 0.85));
            };
            MouseLeave += delegate { Refresh(); };
            MouseLeftButtonUp += delegate (object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                done = !done;
                Refresh();
                if (Clicked != null) Clicked(this, EventArgs.Empty);
            };
        }

        public void Refresh()
        {
            if (done)
            {
                Background = Palette.Br(Palette.Accent);
                BorderBrush = Palette.Br(Palette.Accent);
                tick.Foreground = Palette.Br(Colors.White);
                tick.Visibility = Visibility.Visible;
            }
            else
            {
                Background = Palette.Br(Colors.Transparent);
                BorderBrush = Palette.Br(Palette.Hex("#3A4250"));
                tick.Visibility = Visibility.Collapsed;
            }
        }
    }

    public class HomeworkRow : Border
    {
        private HomeworkItem item;
        private TextBlock course;
        private TextBlock meta;
        private TextBox box;
        private TickCircle circle;
        private AppCore core;
        private bool done;

        public event EventHandler Changed;

        public HomeworkRow(AppCore core, HomeworkItem item)
        {
            this.core = core;
            this.item = item;
            CornerRadius = new CornerRadius(12);
            Background = Palette.Br(Palette.Surface);
            BorderThickness = new Thickness(1);
            BorderBrush = Palette.Br(Palette.BorderSoft);
            Padding = new Thickness(14, 12, 14, 12);
            Margin = new Thickness(0, 0, 0, 10);

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions[0].Width = GridLength.Auto;
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions[1].Width = new GridLength(190);
            g.ColumnDefinitions.Add(new ColumnDefinition());

            circle = new TickCircle();
            circle.VerticalAlignment = VerticalAlignment.Top;
            circle.Margin = new Thickness(0, 2, 14, 0);
            circle.Clicked += delegate
            {
                item.Done = circle.IsDone;
                item.Updated = Fmt.DateTimeText(DateTime.Now);
                core.SaveHomeworkNow();
                done = item.Done;
                ApplyVisual();
                if (Changed != null) Changed(this, EventArgs.Empty);
            };
            g.Children.Add(circle);

            StackPanel head = new StackPanel();
            head.Margin = new Thickness(0, 0, 14, 0);
            course = Ui.Text(item.Course, 14, Palette.TextPrimary, FontWeights.SemiBold, true);
            meta = Ui.Text("", 11, Palette.TextMuted);
            meta.Margin = new Thickness(0, 4, 0, 0);
            head.Children.Add(course);
            head.Children.Add(meta);
            Grid.SetColumn(head, 1);
            g.Children.Add(head);

            box = new TextBox();
            box.AcceptsReturn = true;
            box.TextWrapping = TextWrapping.Wrap;
            box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            box.MinHeight = 56;
            box.MaxHeight = 150;
            box.FontSize = 13;
            box.FontFamily = Palette.UiFont;
            box.Text = item.Text;
            box.TextChanged += delegate
            {
                item.Text = box.Text;
                item.Updated = Fmt.DateTimeText(DateTime.Now);
                core.SaveHomeworkSoon();
            };
            Grid.SetColumn(box, 2);
            g.Children.Add(box);

            Child = g;
            done = item.Done;
            circle.IsDone = item.Done;
            ApplyVisual();
        }

        public void SetExternalText()
        {
            box.Text = item.Text;
            done = item.Done;
            circle.IsDone = item.Done;
            ApplyVisual();
        }

        public void UpdateMeta(AppCore core)
        {
            if (item.LastClearedKey.Length > 0)
            {
                meta.Text = "上次课后清空：" + item.LastClearedKey;
            }
            else
            {
                meta.Text = core.Config.ClearMode == "Manual" ? "未启用自动清空" : "尚未记录上课";
            }
        }

        private void ApplyVisual()
        {
            Color fg = done ? Palette.TextMuted : Palette.TextPrimary;
            course.Foreground = Palette.Br(fg);
            if (done) course.TextDecorations = TextDecorations.Strikethrough;
            else course.TextDecorations = null;
            box.Foreground = done ? Palette.Br(Palette.TextMuted) : Palette.Br(Palette.TextPrimary);
            box.Opacity = done ? 0.75 : 1.0;
            Background = Palette.Br(done ? Palette.Hex("#13161B") : Palette.Surface);
        }
    }

    public class HomeworkPage : Grid
    {
        private AppCore core;
        private WrapPanel chips;
        private StackPanel list;
        private TextBlock footer;
        private List<HomeworkRow> rows = new List<HomeworkRow>();
        private TextBlock clearHint;

        public HomeworkPage(AppCore core)
        {
            this.core = core;
            RowDefinitions.Add(new RowDefinition());
            RowDefinitions[0].Height = GridLength.Auto;
            RowDefinitions.Add(new RowDefinition());
            RowDefinitions[1].Height = GridLength.Auto;
            RowDefinitions.Add(new RowDefinition());
            RowDefinitions[2].Height = new GridLength(1, GridUnitType.Star);
            RowDefinitions.Add(new RowDefinition());
            RowDefinitions[3].Height = GridLength.Auto;

            StackPanel head = new StackPanel();
            head.Margin = new Thickness(0, 0, 0, 12);
            TextBlock title = Ui.Text("作业记录", 21, Palette.TextPrimary, FontWeights.SemiBold);
            clearHint = Ui.Text("", 12.5, Palette.TextMuted);
            clearHint.Margin = new Thickness(0, 4, 0, 0);
            head.Children.Add(title);
            head.Children.Add(clearHint);
            Children.Add(head);

            Border chipCard = Ui.Card(new StackPanel(), 12, Palette.Surface, Palette.BorderSoft, new Thickness(14, 12, 14, 12));
            StackPanel chipStack = (StackPanel)chipCard.Child;
            chipStack.Children.Add(Ui.Text("参与跟踪的课程", 12.5, Palette.TextSecondary, FontWeights.Medium));
            chips = new WrapPanel();
            chips.Margin = new Thickness(0, 10, 0, 0);
            chipStack.Children.Add(chips);
            Grid.SetRow(chipCard, 1);
            chipCard.Margin = new Thickness(0, 0, 0, 14);
            Children.Add(chipCard);

            ScrollViewer sv = new ScrollViewer();
            sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            sv.Padding = new Thickness(0, 0, 6, 0);
            list = new StackPanel();
            sv.Content = list;
            Grid.SetRow(sv, 2);
            Children.Add(sv);

            footer = Ui.Text("", 12, Palette.TextMuted);
            footer.Margin = new Thickness(0, 10, 0, 0);
            Grid.SetRow(footer, 3);
            Children.Add(footer);
            Build();
        }

        public void Build()
        {
            chips.Children.Clear();
            List<string> courses = core.Doc != null ? core.Doc.Courses() : new List<string>();
            if (courses.Count == 0)
            {
                TextBlock hint = Ui.Text("先导入课表，才能选择课程", 12, Palette.TextMuted);
                hint.Margin = new Thickness(0, 2, 0, 2);
                chips.Children.Add(hint);
            }
            for (int i = 0; i < courses.Count; i++)
            {
                Chip chip = new Chip(courses[i]);
                chip.Margin = new Thickness(0, 0, 8, 8);
                chip.IsSelected = core.Config.TrackedCourses.Contains(courses[i]);
                string value = courses[i];
                chip.Toggled += delegate
                {
                    Chip c = (Chip)chip;
                    core.TrackCourse(value, c.IsSelected);
                    RebuildList();
                };
                chips.Children.Add(chip);
            }
            clearHint.Text = "清空规则：" + core.ClearModeText() + "　·　勾选左侧圆圈表示作业已完成";
            RebuildList();
        }

        public void RebuildList()
        {
            list.Children.Clear();
            rows.Clear();
            List<string> tracked = core.Config.TrackedCourses;
            if (tracked.Count == 0)
            {
                StackPanel empty = new StackPanel();
                empty.Margin = new Thickness(0, 30, 0, 0);
                empty.HorizontalAlignment = HorizontalAlignment.Center;
                empty.Children.Add(Ui.Icon(Glyph.Checklist, 30, Palette.TextMuted));
                TextBlock t = Ui.Text("还没有选择课程", 14, Palette.TextSecondary, FontWeights.Medium);
                t.HorizontalAlignment = HorizontalAlignment.Center;
                t.Margin = new Thickness(0, 10, 0, 0);
                empty.Children.Add(t);
                TextBlock t2 = Ui.Text("在上方点选有作业的课程，它们会出现在这里", 12, Palette.TextMuted);
                t2.HorizontalAlignment = HorizontalAlignment.Center;
                t2.Margin = new Thickness(0, 4, 0, 0);
                empty.Children.Add(t2);
                list.Children.Add(empty);
                UpdateFooter();
                return;
            }
            for (int i = 0; i < tracked.Count; i++)
            {
                HomeworkItem item = core.Homework.Ensure(tracked[i]);
                HomeworkRow row = new HomeworkRow(core, item);
                row.UpdateMeta(core);
                row.Changed += delegate { UpdateFooter(); };
                rows.Add(row);
                list.Children.Add(row);
            }
            UpdateFooter();
        }

        public void SyncFromModel()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].SetExternalText();
                rows[i].UpdateMeta(core);
            }
            UpdateFooter();
        }

        private void UpdateFooter()
        {
            int total = core.Config.TrackedCourses.Count;
            int done = 0;
            for (int i = 0; i < total; i++)
            {
                HomeworkItem it = core.Homework.Find(core.Config.TrackedCourses[i]);
                if (it != null && it.Done) done++;
            }
            footer.Text = "已完成 " + done.ToString(CultureInfo.InvariantCulture) + " / " +
                total.ToString(CultureInfo.InvariantCulture) + "　　数据保存在 data\\homework.json";
        }
    }
}
