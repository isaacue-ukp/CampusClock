using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace CampusClock
{
    /// <summary>Desktop floating ball / hover expanded timetable + homework panel.</summary>
    public class Widget : Window
    {
        private AppCore core;
        private MainWindow main;

        private Border shell;
        private Grid collapsed;
        private Border leftHalf;
        private Border rightHalf;
        private Grid expandedLayer;
        private ContentControl bodyHost;
        private TextBlock headerTitle;
        private TextBlock headerSub;
        private IconBtn pinBtn;
        private Border tabTime;
        private Border tabHw;
        private TextBlock tabTimeText;
        private TextBlock tabHwText;

        private DispatcherTimer animTimer;
        private DispatcherTimer collapseTimer;

        private bool expanded;
        private bool animating;
        private bool animExpanding;
        private DateTime animStart;
        private double animFromW;
        private double animFromH;
        private double animToW;
        private double animToH;
        private double anchorLeft;
        private double anchorTop;
        private double anchorW;
        private double anchorH;
        private bool expandRight;
        private bool expandDown;
        private bool allowClose;

        private bool dragging;
        private Point dragOrigin;        // pointer offset inside the window when the drag started
        private bool dragMoved;
        private bool suppressHoverExpand;   // set when the user collapses while the pointer is still inside
        private DispatcherTimer disarmTimer;   // polls the real pointer position until it has left the widget
        private bool simMode;               // --selftest only: replaces IsMouseOver / Mouse.LeftButton
        private bool simPointerInside;
        private bool simButtonDown;

        private string pane = "timetable";
        private bool contentDirty = true;

        public Widget(AppCore core, MainWindow main)
        {
            this.core = core;
            this.main = main;
            this.pane = core.Config.BallPane;

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Palette.Br(Colors.Transparent);
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            Topmost = core.Config.BallTopmost;
            WindowStartupLocation = WindowStartupLocation.Manual;
            ShowActivated = false;
            SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

            shell = new Border();
            shell.CornerRadius = new CornerRadius(BallRadius());
            shell.Background = BuildShellBrush();
            shell.BorderBrush = Palette.Br(Palette.Alpha(Palette.Accent, 0.55));
            shell.BorderThickness = new Thickness(1);
            shell.ClipToBounds = true;
            shell.Effect = Ui.Shadow(26, 0.5);

            Grid layers = new Grid();
            layers.Children.Add(BuildCollapsed());
            layers.Children.Add(BuildExpanded());
            shell.Child = layers;
            Content = shell;

            animTimer = new DispatcherTimer();
            animTimer.Interval = TimeSpan.FromMilliseconds(15);
            animTimer.Tick += delegate { OnAnimTick(); };

            collapseTimer = new DispatcherTimer();
            collapseTimer.Interval = TimeSpan.FromMilliseconds(420);
            collapseTimer.Tick += delegate { OnCollapseTimerTick(); };

            // Safety net for the "closed under the pointer" state: it is cleared by the real pointer
            // position, not by a MouseLeave event (resizing the window produces spurious leave/enter pairs).
            disarmTimer = new DispatcherTimer();
            disarmTimer.Interval = TimeSpan.FromMilliseconds(400);
            disarmTimer.Tick += delegate { OnDisarmTimerTick(); };

            // The drag lifecycle lives on the window: the window is the element that holds the mouse
            // capture, so children stop receiving move/up events as soon as capture is taken.
            AddHandler(UIElement.MouseMoveEvent, new MouseEventHandler(OnPointerMove), true);
            AddHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler(OnPointerUp), true);
            AddHandler(UIElement.LostMouseCaptureEvent, new MouseEventHandler(OnLostMouseCapture), true);

            ApplyConfig();
            MouseLeave += delegate { OnPointerLeaveWidget(); };
            Deactivated += delegate { ScheduleCollapse(); };
        }

        /// <summary>Collapsed ball: height = BallSize, width = height * this factor.</summary>
        private const double BallAspect = 1.6;

        private double BallRadius()
        {
            return core.Config.BallSize * 0.34;
        }

        private double BallHeight()
        {
            return core.Config.BallSize;
        }

        private double BallWidth()
        {
            return Math.Round(core.Config.BallSize * BallAspect);
        }

        private Brush BuildShellBrush()
        {
            LinearGradientBrush b = new LinearGradientBrush();
            b.StartPoint = new Point(0, 0);
            b.EndPoint = new Point(1, 1);
            b.GradientStops.Add(new GradientStop(Palette.Hex("#22262F"), 0));
            b.GradientStops.Add(new GradientStop(Palette.Hex("#171A21"), 1));
            return b;
        }

        private FrameworkElement BuildCollapsed()
        {
            collapsed = new Grid();
            collapsed.ColumnDefinitions.Add(new ColumnDefinition());
            collapsed.ColumnDefinitions.Add(new ColumnDefinition());

            leftHalf = BuildHalf(Glyph.Calendar, "课表", true);
            rightHalf = BuildHalf(Glyph.Checklist, "作业", false);
            collapsed.Children.Add(leftHalf);
            collapsed.Children.Add(rightHalf);

            Border divider = new Border();
            divider.Width = 1;
            divider.HorizontalAlignment = HorizontalAlignment.Center;
            divider.Margin = new Thickness(0, 12, 0, 12);
            divider.Background = Palette.Br(Palette.Alpha(Colors.White, 0.16));
            divider.IsHitTestVisible = false;
            collapsed.Children.Add(divider);
            return collapsed;
        }

        private Border BuildHalf(string glyph, string label, bool left)
        {
            Border half = new Border();
            half.Background = Palette.Br(Colors.Transparent);
            half.Cursor = Cursors.Hand;
            StackPanel sp = new StackPanel();
            sp.VerticalAlignment = VerticalAlignment.Center;
            sp.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Margin = new Thickness(6, 0, 6, 0);
            TextBlock icon = Ui.Icon(glyph, 15, Palette.TextSecondary);
            TextBlock caption = Ui.Text(label, 9.5, Palette.TextMuted);
            caption.HorizontalAlignment = HorizontalAlignment.Center;
            caption.Margin = new Thickness(0, 3, 0, 0);
            sp.Children.Add(icon);
            sp.Children.Add(caption);
            half.Child = sp;
            Grid.SetColumn(half, left ? 0 : 1);

            half.MouseEnter += delegate
            {
                half.Background = Palette.Br(Palette.Alpha(Palette.Accent, 0.22));
                icon.Foreground = Palette.Br(Palette.Accent);
                caption.Foreground = Palette.Br(Palette.Accent);
                TryHoverExpand(left);
            };
            half.MouseLeave += delegate
            {
                half.Background = Palette.Br(Colors.Transparent);
                icon.Foreground = Palette.Br(Palette.TextSecondary);
                caption.Foreground = Palette.Br(Palette.TextMuted);
                if (!expanded) ScheduleCollapse();
            };
            half.MouseLeftButtonDown += delegate (object s, MouseButtonEventArgs e)
            {
                // Do not expand here: we only know whether this is a click or a drag after mouse-up.
                BeginDrag(e.GetPosition(this));
                e.Handled = true;
            };
            return half;
        }

        private FrameworkElement BuildExpanded()
        {
            expandedLayer = new Grid();
            expandedLayer.Visibility = Visibility.Collapsed;
            expandedLayer.RowDefinitions.Add(new RowDefinition());
            expandedLayer.RowDefinitions[0].Height = new GridLength(42);
            expandedLayer.RowDefinitions.Add(new RowDefinition());
            expandedLayer.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
            expandedLayer.RowDefinitions.Add(new RowDefinition());
            expandedLayer.RowDefinitions[2].Height = new GridLength(26);

            // header
            Border header = new Border();
            header.Background = Palette.Br(Palette.Alpha(Colors.White, 0.04));
            header.BorderBrush = Palette.Br(Palette.Alpha(Colors.White, 0.07));
            header.BorderThickness = new Thickness(0, 0, 0, 1);
            header.Cursor = Cursors.SizeAll;
            header.MouseLeftButtonDown += delegate (object s, MouseButtonEventArgs e)
            {
                if (IsInteractive(e.OriginalSource)) return;
                BeginDrag(e.GetPosition(this));
                e.Handled = true;
            };

            Grid hg = new Grid();
            hg.Margin = new Thickness(8, 0, 6, 0);
            hg.ColumnDefinitions.Add(new ColumnDefinition());
            hg.ColumnDefinitions.Add(new ColumnDefinition());
            hg.ColumnDefinitions[1].Width = GridLength.Auto;

            StackPanel tabs = new StackPanel();
            tabs.Orientation = Orientation.Horizontal;
            tabs.VerticalAlignment = VerticalAlignment.Center;
            tabTime = BuildTab(Glyph.Calendar, "课表", true);
            tabHw = BuildTab(Glyph.Checklist, "作业", false);
            tabs.Children.Add(tabTime);
            tabs.Children.Add(tabHw);
            hg.Children.Add(tabs);

            StackPanel actions = new StackPanel();
            actions.Orientation = Orientation.Horizontal;
            actions.VerticalAlignment = VerticalAlignment.Center;
            pinBtn = new IconBtn(core.Config.BallPinned ? Glyph.Pin : Glyph.PinOff, 12,
                core.Config.BallPinned ? Palette.Accent : Palette.TextSecondary, 26);
            pinBtn.ToolTip = "固定展开（不自动收起）";
            pinBtn.Clicked += delegate
            {
                core.Config.BallPinned = !core.Config.BallPinned;
                core.SaveConfig();
                UpdatePin();
                if (!core.Config.BallPinned) ScheduleCollapse();
            };
            IconBtn openMain = new IconBtn(Glyph.OpenWindow, 12, Palette.TextSecondary, 26);
            openMain.ToolTip = "打开主窗口";
            openMain.Clicked += delegate
            {
                if (main != null) main.RestoreFromTray();
            };
            IconBtn collapseBtn = new IconBtn(Glyph.Close, 11, Palette.TextSecondary, 26);
            collapseBtn.ToolTip = "收起";
            collapseBtn.Clicked += delegate { Collapse(); };
            actions.Children.Add(pinBtn);
            actions.Children.Add(openMain);
            actions.Children.Add(collapseBtn);
            Grid.SetColumn(actions, 1);
            hg.Children.Add(actions);
            header.Child = hg;
            expandedLayer.Children.Add(header);

            bodyHost = new ContentControl();
            bodyHost.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            bodyHost.VerticalContentAlignment = VerticalAlignment.Stretch;
            bodyHost.Margin = new Thickness(10, 8, 10, 0);
            Grid.SetRow(bodyHost, 1);
            expandedLayer.Children.Add(bodyHost);

            Border footer = new Border();
            headerTitle = Ui.Text("", 11, Palette.TextMuted);
            headerTitle.VerticalAlignment = VerticalAlignment.Center;
            footer.Child = headerTitle;
            footer.Margin = new Thickness(12, 0, 12, 0);
            Grid.SetRow(footer, 2);
            expandedLayer.Children.Add(footer);
            headerSub = headerTitle;

            shell.ContextMenu = BuildContextMenu();
            return expandedLayer;
        }

        private Border BuildTab(string glyph, string label, bool left)
        {
            Border tab = new Border();
            tab.CornerRadius = new CornerRadius(7);
            tab.Padding = new Thickness(9, 5, 11, 5);
            tab.Margin = new Thickness(0, 0, 6, 0);
            tab.Cursor = Cursors.Hand;
            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            TextBlock icon = Ui.Icon(glyph, 12, Palette.TextSecondary);
            icon.Margin = new Thickness(0, 0, 6, 0);
            TextBlock text = Ui.Text(label, 12, Palette.TextSecondary, FontWeights.Medium);
            sp.Children.Add(icon);
            sp.Children.Add(text);
            tab.Child = sp;
            tab.MouseLeftButtonUp += delegate
            {
                pane = left ? "timetable" : "homework";
                core.Config.BallPane = pane;
                core.SaveConfig();
                contentDirty = true;
                RefreshContent();
                UpdateTabs();
            };
            if (left) tabTimeText = text; else tabHwText = text;
            return tab;
        }

        private ContextMenu BuildContextMenu()
        {
            ContextMenu menu = new ContextMenu();
            MenuItem open = new MenuItem();
            open.Header = "打开主窗口";
            open.Click += delegate { if (main != null) main.RestoreFromTray(); };
            MenuItem pin = new MenuItem();
            pin.Header = "固定展开";
            pin.Click += delegate
            {
                core.Config.BallPinned = !core.Config.BallPinned;
                core.SaveConfig();
                UpdatePin();
            };
            MenuItem top = new MenuItem();
            top.Header = "始终置顶";
            top.Click += delegate
            {
                core.Config.BallTopmost = !core.Config.BallTopmost;
                ApplyTopmost();
            };
            MenuItem hide = new MenuItem();
            hide.Header = "隐藏悬浮球";
            hide.Click += delegate
            {
                core.Config.BallEnabled = false;
                core.SaveConfig();
                Hide();
                if (main != null) main.ApplyConfigLive();
            };
            MenuItem quit = new MenuItem();
            quit.Header = "退出 CampusClock";
            quit.Click += delegate { if (main != null) main.ExitApp(); };
            menu.Items.Add(open);
            menu.Items.Add(pin);
            menu.Items.Add(top);
            menu.Items.Add(new Separator());
            menu.Items.Add(hide);
            menu.Items.Add(quit);
            return menu;
        }

        private void ApplyTopmost()
        {
            core.SaveConfig();
            Topmost = core.Config.BallTopmost;
        }

        private bool IsInteractive(object source)
        {
            DependencyObject d = source as DependencyObject;
            while (d != null)
            {
                if (d is TextBox) return true;
                Border b = d as Border;
                if (b != null && (b is IconBtn || b is TextBtn || b is Chip || b.Cursor == Cursors.Hand)) return true;
                d = VisualTreeHelper.GetParent(d);
            }
            return false;
        }

        public void ApplyConfig()
        {
            Topmost = core.Config.BallTopmost;
            UpdatePin();
            if (!expanded && !animating)
            {
                Width = BallWidth();
                Height = BallHeight();
                shell.CornerRadius = new CornerRadius(BallRadius());
                PlaceCollapsed();
            }
            else
            {
                contentDirty = true;
                RefreshContent();
            }
        }

        private void PlaceCollapsed()
        {
            Rect work = WorkArea();
            double w = BallWidth();
            double h = BallHeight();
            double left = core.Config.BallLeft == int.MinValue
                ? work.Right - w - 36
                : core.Config.BallLeft;
            double top = core.Config.BallTop == int.MinValue
                ? work.Top + (work.Height - h) / 2
                : core.Config.BallTop;
            left = Math.Max(work.Left, Math.Min(work.Right - w, left));
            top = Math.Max(work.Top, Math.Min(work.Bottom - h, top));
            Left = left;
            Top = top;
            anchorLeft = left;
            anchorTop = top;
            anchorW = w;
            anchorH = h;
        }

        private Rect WorkArea()
        {
            try
            {
                return SystemParameters.WorkArea;
            }
            catch
            {
                return new Rect(0, 0, 1920, 1080);
            }
        }

        private double ExpandedSize()
        {
            Rect work = WorkArea();
            double ratio = core.Config.ExpandAreaPercent / 100.0;
            double side = Math.Sqrt(Math.Max(0.02, ratio) * work.Width * work.Height);
            double limit = Math.Min(work.Width, work.Height) - 60;
            return Math.Max(300, Math.Min(limit, side));
        }

        public void Expand(string which)
        {
            if (animating) return;
            if (disarmTimer != null) disarmTimer.Stop();
            pane = which;
            core.Config.BallPane = pane;
            collapseTimer.Stop();

            anchorLeft = Left;
            anchorTop = Top;
            anchorW = Width;
            anchorH = Height;

            Rect work = WorkArea();
            double centerX = anchorLeft + anchorW / 2;
            double centerY = anchorTop + anchorH / 2;
            expandRight = centerX <= work.Left + work.Width / 2;
            expandDown = centerY <= work.Top + work.Height / 2;

            collapsed.Visibility = Visibility.Collapsed;
            expandedLayer.Visibility = Visibility.Visible;
            shell.CornerRadius = new CornerRadius(18);
            contentDirty = true;
            bodyHost.Content = null;
            UpdateTabs();

            double side = ExpandedSize();
            StartAnim(anchorW, anchorH, side, side, true);
        }

        public void Collapse()
        {
            if (animating) return;
            if (!expanded) return;
            // Closing always disarms the widget: it stays closed until the pointer has really left the
            // widget, so a single ✕ click can never collapse-and-reopen (the reported behaviour).
            suppressHoverExpand = true;
            if (disarmTimer != null) disarmTimer.Start();
            double ballW = BallWidth();
            double ballH = BallHeight();
            // keep the ball near where the panel was
            double newLeft = expandRight ? Left : Left + Width - ballW;
            double newTop = expandDown ? Top : Top + Height - ballH;
            anchorLeft = newLeft;
            anchorTop = newTop;
            core.Config.BallLeft = (int)Math.Round(newLeft);
            core.Config.BallTop = (int)Math.Round(newTop);
            core.SaveConfig();
            StartAnim(Width, Height, ballW, ballH, false);
        }

        private void StartAnim(double fromW, double fromH, double toW, double toH, bool expanding)
        {
            animating = true;
            animExpanding = expanding;
            animFromW = fromW;
            animFromH = fromH;
            animToW = toW;
            animToH = toH;
            animStart = DateTime.Now;
            if (core.Config.AnimationMs <= 0)
            {
                ApplyAnimFrame(1.0);
                CompleteAnimation();
                return;
            }
            animTimer.Start();
        }

        private void OnAnimTick()
        {
            double total = Math.Max(1, core.Config.AnimationMs);
            double p = (DateTime.Now - animStart).TotalMilliseconds / total;
            if (p >= 1)
            {
                animTimer.Stop();
                ApplyAnimFrame(1.0);
                CompleteAnimation();
                return;
            }
            ApplyAnimFrame(p);
        }

        /// <summary>Ends the running expand / collapse animation and applies its final state.</summary>
        private void CompleteAnimation()
        {
            animating = false;
            expanded = animExpanding;
            if (animExpanding) FinishExpand();
            else FinishCollapse();
        }

        private void ApplyAnimFrame(double p)
        {
            double eased = 1 - Math.Pow(1 - p, 3);
            double w = animFromW + (animToW - animFromW) * eased;
            double h = animFromH + (animToH - animFromH) * eased;
            Width = w;
            Height = h;
            Rect work = WorkArea();
            double left = expandRight ? anchorLeft : anchorLeft + anchorW - w;
            double top = expandDown ? anchorTop : anchorTop + anchorH - h;
            if (left < work.Left) left = work.Left;
            if (top < work.Top) top = work.Top;
            if (left + w > work.Right) left = work.Right - w;
            if (top + h > work.Bottom) top = work.Bottom - h;
            Left = left;
            Top = top;
            if (w > 110 && h > 110 && bodyHost != null && bodyHost.Content == null && animExpanding)
            {
                RefreshContent();
            }
        }

        private void FinishExpand()
        {
            expanded = true;
            RefreshContent();
            opacityFix();
            if (!PointerInside && !IsKeyboardFocusWithin && !core.Config.BallPinned)
            {
                ScheduleCollapse();
            }
        }

        private void opacityFix()
        {
            Ui.Animate(shell, 1, 0);
        }

        private void FinishCollapse()
        {
            expanded = false;
            expandedLayer.Visibility = Visibility.Collapsed;
            collapsed.Visibility = Visibility.Visible;
            shell.CornerRadius = new CornerRadius(BallRadius());
            Width = BallWidth();
            Height = BallHeight();
            Left = anchorLeft;
            Top = anchorTop;
            // if the pointer ended up outside the collapsed ball, the widget is immediately usable again
            RearmIfPointerLeft();
        }

        private bool ButtonIsDown
        {
            get { return simMode ? simButtonDown : Mouse.LeftButton == MouseButtonState.Pressed; }
        }

        private bool PointerInside
        {
            get { return simMode ? simPointerInside : IsMouseOver; }
        }

        /// <summary>True when the pointer is outside the widget's own bounds (geometry based, not event based).</summary>
        private bool PointerOutsideBounds()
        {
            if (simMode) return !simPointerInside;
            try
            {
                Point p = Mouse.GetPosition(this);
                double w = ActualWidth > 1 ? ActualWidth : Width;
                double h = ActualHeight > 1 ? ActualHeight : Height;
                return p.X < -0.5 || p.Y < -0.5 || p.X > w + 0.5 || p.Y > h + 0.5;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>Re-arms the widget only once the pointer has really left it.</summary>
        private void RearmIfPointerLeft()
        {
            if (!suppressHoverExpand) return;
            if (animating) return;                 // geometry is still changing: ignore spurious events
            if (!PointerOutsideBounds()) return;   // pointer is still on the widget: keep it closed
            suppressHoverExpand = false;
            if (disarmTimer != null) disarmTimer.Stop();
        }

        private void OnDisarmTimerTick()
        {
            if (!suppressHoverExpand)
            {
                disarmTimer.Stop();
                return;
            }
            RearmIfPointerLeft();
        }

        /// <summary>Hover on one half of the ball. Refuses while the panel is busy or was just closed by the user.</summary>
        private void TryHoverExpand(bool left)
        {
            if (expanded || animating) return;
            if (dragging)
            {
                if (ButtonIsDown) return;   // a real drag is in progress
                CancelDrag();               // stale drag state: recover instead of blocking expansion forever
            }
            if (suppressHoverExpand) return;
            Expand(left ? "timetable" : "homework");
        }

        private void OnPointerLeaveWidget()
        {
            // only a real departure re-arms the widget (a resizing window fires leave/enter pairs)
            RearmIfPointerLeft();
            ScheduleCollapse();
        }

        private void OnCollapseTimerTick()
        {
            collapseTimer.Stop();
            if (dragging && !ButtonIsDown) CancelDrag();
            if (!PointerInside && !IsKeyboardFocusWithin && !core.Config.BallPinned && !dragging)
            {
                Collapse();
            }
        }

        private void ScheduleCollapse()
        {
            if (!expanded || core.Config.BallPinned) return;
            if (IsKeyboardFocusWithin) return;
            if (dragging)
            {
                if (ButtonIsDown) return;   // genuine drag: keep the panel while the user moves it
                CancelDrag();               // stale drag state must not block auto-collapse
            }
            collapseTimer.Stop();
            collapseTimer.Start();
        }

        private void UpdatePin()
        {
            if (pinBtn == null) return;
            pinBtn.SetGlyph(core.Config.BallPinned ? Glyph.Pin : Glyph.PinOff);
            pinBtn.SetForeground(core.Config.BallPinned ? Palette.Accent : Palette.TextSecondary);
        }

        private void UpdateTabs()
        {
            bool time = pane == "timetable";
            if (tabTime != null)
            {
                tabTime.Background = Palette.Br(time ? Palette.Alpha(Palette.Accent, 0.20) : Colors.Transparent);
                tabTimeText.Foreground = Palette.Br(time ? Palette.Accent : Palette.TextSecondary);
            }
            if (tabHw != null)
            {
                tabHw.Background = Palette.Br(!time ? Palette.Alpha(Palette.Accent, 0.20) : Colors.Transparent);
                tabHwText.Foreground = Palette.Br(!time ? Palette.Accent : Palette.TextSecondary);
            }
        }

        public void RefreshContent()
        {
            if (!expanded || bodyHost == null) return;
            double bodyH = Height - 42 - 26;
            double bodyW = Width - 20;
            if (pane == "timetable")
            {
                DateTime weekStart = Schedule.WeekStart(DateTime.Today);
                FrameworkElement grid = TimetableRenderer.Build(core.Schedule, core.Config, weekStart, true,
                    Math.Max(200, bodyW), Math.Max(150, bodyH));
                bodyHost.Content = grid;
                int index = core.Schedule != null ? core.Schedule.WeekIndex(DateTime.Today) : 1;
                string week = index >= 1 ? "第 " + index + " 周" : "开学前 " + (1 - index) + " 周";
                headerTitle.Text = week + "　" + Fmt.ShortDate(weekStart) + " ~ " + Fmt.ShortDate(weekStart.AddDays(6));
            }
            else
            {
                bodyHost.Content = BuildHomeworkBody(bodyW, bodyH);
            }
            contentDirty = false;
        }

        private FrameworkElement BuildHomeworkBody(double w, double h)
        {
            ScrollViewer sv = new ScrollViewer();
            sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            StackPanel list = new StackPanel();
            sv.Content = list;

            List<string> tracked = core.Config.TrackedCourses;
            if (tracked.Count == 0)
            {
                StackPanel empty = new StackPanel();
                empty.Margin = new Thickness(0, h * 0.25, 0, 0);
                empty.HorizontalAlignment = HorizontalAlignment.Center;
                empty.Children.Add(Ui.Icon(Glyph.Checklist, 22, Palette.TextMuted));
                TextBlock t = Ui.Text("还没有选择课程", 12, Palette.TextSecondary);
                t.HorizontalAlignment = HorizontalAlignment.Center;
                t.Margin = new Thickness(0, 8, 0, 0);
                empty.Children.Add(t);
                list.Children.Add(empty);
            }
            int doneCount = 0;
            for (int i = 0; i < tracked.Count; i++)
            {
                HomeworkItem item = core.Homework.Ensure(tracked[i]);
                if (item.Done) doneCount++;
                list.Children.Add(BuildCompactRow(item, w));
            }
            headerTitle.Text = "已完成 " + doneCount + " / " + tracked.Count + "　·　" + core.ClearModeText();
            return sv;
        }

        private FrameworkElement BuildCompactRow(HomeworkItem item, double w)
        {
            Border card = new Border();
            card.CornerRadius = new CornerRadius(10);
            card.Background = Palette.Br(item.Done ? Palette.Hex("#13161B") : Palette.Hex("#1B1F27"));
            card.BorderBrush = Palette.Br(Palette.Alpha(Colors.White, 0.07));
            card.BorderThickness = new Thickness(1);
            card.Padding = new Thickness(10, 8, 10, 8);
            card.Margin = new Thickness(0, 0, 0, 8);

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions[0].Width = GridLength.Auto;
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions[1].Width = new GridLength(Math.Max(84, w * 0.26));
            g.ColumnDefinitions.Add(new ColumnDefinition());

            TickCircle circle = new TickCircle();
            circle.Width = 20;
            circle.Height = 20;
            circle.CornerRadius = new CornerRadius(10);
            circle.VerticalAlignment = VerticalAlignment.Top;
            circle.Margin = new Thickness(0, 2, 9, 0);
            circle.IsDone = item.Done;
            circle.Clicked += delegate
            {
                item.Done = circle.IsDone;
                item.Updated = Fmt.DateTimeText(DateTime.Now);
                core.SaveHomeworkNow();
                RefreshContent();
                if (main != null) main.RefreshAll();
            };
            g.Children.Add(circle);

            StackPanel nameCol = new StackPanel();
            nameCol.Margin = new Thickness(0, 2, 8, 0);
            TextBlock name = Ui.Text(item.Course, 12, item.Done ? Palette.TextMuted : Palette.TextPrimary,
                FontWeights.SemiBold, true);
            if (item.Done) name.TextDecorations = TextDecorations.Strikethrough;
            nameCol.Children.Add(name);
            if (item.Method.Trim().Length > 0)
            {
                // 提交方式：独立 metadata，只在这里只读展示
                TextBlock method = Ui.Text("提交：" + item.Method.Trim(), 10.5, Palette.TextMuted,
                    FontWeights.Normal, true);
                method.Margin = new Thickness(0, 2, 0, 0);
                nameCol.Children.Add(method);
            }
            Grid.SetColumn(nameCol, 1);
            g.Children.Add(nameCol);

            StackPanel jobCol = new StackPanel();
            TextBox box = new TextBox();
            box.AcceptsReturn = true;
            box.TextWrapping = TextWrapping.Wrap;
            box.FontSize = 12;
            box.MinHeight = 44;
            box.MaxHeight = 110;
            box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            box.Text = item.Text;
            box.Foreground = Palette.Br(item.Done ? Palette.TextMuted : Palette.TextPrimary);
            box.TextChanged += delegate
            {
                item.Text = box.Text;
                item.Updated = Fmt.DateTimeText(DateTime.Now);
                core.SaveHomeworkSoon();
            };
            box.GotKeyboardFocus += delegate { collapseTimer.Stop(); };
            jobCol.Children.Add(box);
            if (item.LastText.Trim().Length > 0)
            {
                StackPanel lastRow = new StackPanel();
                lastRow.Orientation = Orientation.Horizontal;
                lastRow.Margin = new Thickness(0, 4, 0, 0);
                TextBlock hint = Ui.Text("上次作业可恢复", 10, Palette.TextMuted);
                hint.VerticalAlignment = VerticalAlignment.Center;
                lastRow.Children.Add(hint);
                TextBtn restore = new TextBtn("恢复", Colors.Transparent, Palette.Hover,
                    Palette.Accent, Palette.Alpha(Palette.Accent, 0.45));
                restore.Margin = new Thickness(8, 0, 0, 0);
                restore.VerticalAlignment = VerticalAlignment.Center;
                restore.Clicked += delegate
                {
                    if (core.RestoreLastText(item.Course)) RefreshContent();
                };
                lastRow.Children.Add(restore);
                jobCol.Children.Add(lastRow);
            }
            Grid.SetColumn(jobCol, 2);
            g.Children.Add(jobCol);
            card.Child = g;
            return card;
        }

        private void BeginDrag(Point positionInWindow)
        {
            dragging = true;
            dragMoved = false;
            dragOrigin = positionInWindow;
            if (!IsMouseCaptured)
            {
                try { CaptureMouse(); } catch { }
            }
        }

        private void OnPointerMove(object sender, MouseEventArgs e)
        {
            if (!dragging) return;
            PointerMoved(e.GetPosition(this));
        }

        private void PointerMoved(Point positionInWindow)
        {
            if (!dragging) return;
            if (!ButtonIsDown)
            {
                // the button-up was swallowed (capture lost, window switched, …): never stay stuck in dragging
                CancelDrag();
                return;
            }
            DragTo(positionInWindow);
        }

        /// <summary>
        /// Moves the window so that the pointer keeps its offset inside the window.
        /// <para>
        /// The pointer position is reported relative to the window, and the window itself moves,
        /// so the baseline must be the <b>current</b> window position. Using the position captured
        /// at mouse-down (the old code) double-counts the movement already applied: the window then
        /// advances, stalls, advances … and jitters back and forth when the pointer wiggles.
        /// </para>
        /// </summary>
        private void DragTo(Point positionInWindow)
        {
            if (!dragging) return;
            double dx = positionInWindow.X - dragOrigin.X;
            double dy = positionInWindow.Y - dragOrigin.Y;
            if (!dragMoved && (Math.Abs(dx) > 4 || Math.Abs(dy) > 4)) dragMoved = true;
            if (!dragMoved) return;
            MoveWindowTo(Left + dx, Top + dy);
        }

        private void MoveWindowTo(double left, double top)
        {
            Rect work = WorkArea();
            left = Math.Max(work.Left - 4, Math.Min(work.Right - Width + 4, left));
            top = Math.Max(work.Top - 4, Math.Min(work.Bottom - Height + 4, top));
            Left = left;
            Top = top;
        }

        private void OnPointerUp(object sender, MouseButtonEventArgs e)
        {
            if (!dragging) return;
            EndDrag();
            e.Handled = true;
        }

        private void OnLostMouseCapture(object sender, MouseEventArgs e)
        {
            if (dragging) CancelDrag();
        }

        private void EndDrag()
        {
            bool moved = dragMoved;
            ResetDragState();
            if (moved)
            {
                PersistPosition();
                // a drag is an explicit interaction: the panel may be opened again by hovering
                suppressHoverExpand = false;
                if (disarmTimer != null) disarmTimer.Stop();
            }
            else if (!expanded && !animating && !suppressHoverExpand)
            {
                // a click without movement on the ball opens the panel (only while the widget is armed)
                Expand(pane);
            }
        }

        private void CancelDrag()
        {
            bool moved = dragMoved;
            ResetDragState();
            if (moved) PersistPosition();
        }

        private void ResetDragState()
        {
            dragging = false;
            dragMoved = false;
            if (IsMouseCaptured)
            {
                try { ReleaseMouseCapture(); } catch { }
            }
        }

        private void PersistPosition()
        {
            if (expanded)
            {
                anchorLeft = Left;
                anchorTop = Top;
                anchorW = Width;
                anchorH = Height;
            }
            else
            {
                core.Config.BallLeft = (int)Math.Round(Left);
                core.Config.BallTop = (int)Math.Round(Top);
                core.SaveConfig();
                anchorLeft = Left;
                anchorTop = Top;
            }
        }

        public void AllowClose()
        {
            allowClose = true;
        }

        // ------------------------------------------------------------------
        // Simulation hooks. They drive exactly the same methods the real mouse
        // handlers use, so --selftest can verify the interaction state machine
        // without a physical mouse. Not used by the application itself.
        // ------------------------------------------------------------------

        public bool SimExpanded { get { return expanded; } }
        public bool SimAnimating { get { return animating; } }
        public bool SimDragging { get { return dragging; } }
        public bool SimDragMoved { get { return dragMoved; } }
        public bool SimSuppressHoverExpand { get { return suppressHoverExpand; } }
        public bool SimMouseCaptured { get { return IsMouseCaptured; } }
        public double SimLeft { get { return Left; } }
        public double SimTop { get { return Top; } }

        public void SimEnable() { simMode = true; }

        public void SimSetPointerInside(bool inside)
        {
            simMode = true;
            simPointerInside = inside;
        }

        public void SimSetButtonDown(bool down)
        {
            simMode = true;
            simButtonDown = down;
        }

        public void SimHover(bool left) { TryHoverExpand(left); }

        public void SimPointerDownOnBall()
        {
            BeginDrag(new Point(BallWidth() / 2, BallHeight() / 2));
        }

        public void SimPointerDownAt(double x, double y)
        {
            BeginDrag(new Point(x, y));
        }

        /// <summary>Finishes a running expand / collapse animation (the self-test has no dispatcher ticks).</summary>
        public void SimRunAnimationToEnd()
        {
            if (animating) CompleteAnimation();
        }

        public void SimDisarmTimerTick() { OnDisarmTimerTick(); }

        public void SimPointerDownOnHeader()
        {
            BeginDrag(new Point(20, 20));
        }

        public void SimPointerMove(double x, double y) { PointerMoved(new Point(x, y)); }

        public void SimPointerUp() { EndDrag(); }

        public void SimLostCapture() { CancelDrag(); }

        public void SimLeaveWidget() { OnPointerLeaveWidget(); }

        public void SimCollapseTimer() { OnCollapseTimerTick(); }

        public void SimCollapseClick() { Collapse(); }

        public void SimSetPinned(bool pinned)
        {
            core.Config.BallPinned = pinned;
            UpdatePin();
        }
    }
}
