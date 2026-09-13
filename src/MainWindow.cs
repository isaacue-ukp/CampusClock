using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using Microsoft.Win32;

namespace CampusClock
{
    public class MainWindow : Window
    {
        public AppCore Core;

        private ContentControl pageHost;
        private StackPanel navPanel;
        private TextBlock headerTitle;
        private TextBlock headerSub;
        private Grid toastHost;
        private Border toastCard;
        private TimetablePage timetablePage;
        private HomeworkPage homeworkPage;
        private SettingsPage settingsPage;
        private System.Windows.Threading.DispatcherTimer tick;
        private string currentPage = "timetable";
        private List<Border> navItems = new List<Border>();
        private TextBtn ballToggle;
        private TrayIcon tray;
        private Widget widget;
        private bool reallyClose;
        private bool trayHintShown;
        private bool loadWarningShown;
        private const string TextInfoSeparator = "　·　";

        public MainWindow(AppCore core)
        {
            Core = core;
            Title = "CampusClock";
            Width = 1220;
            Height = 820;
            MinWidth = 940;
            MinHeight = 620;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Palette.Br(Palette.WindowBg);
            WindowStyle = WindowStyle.None;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

            WindowChrome chrome = new WindowChrome();
            chrome.CaptionHeight = 0;
            chrome.ResizeBorderThickness = new Thickness(6);
            chrome.GlassFrameThickness = new Thickness(0);
            chrome.CornerRadius = new CornerRadius(0);
            chrome.UseAeroCaptionButtons = false;
            WindowChrome.SetWindowChrome(this, chrome);

            SourceInitialized += OnSourceInitialized;
            BuildShell();
            CreateTray();
            CreateWidget();
            // keep the homework board in sync when data changes elsewhere (e.g. from the floating panel)
            Core.HomeworkChanged += delegate
            {
                if (homeworkPage != null) homeworkPage.SyncFromModel();
            };

            tick = new System.Windows.Threading.DispatcherTimer();
            tick.Interval = TimeSpan.FromSeconds(20);
            tick.Tick += delegate { OnTick(); };
            tick.Start();

            Loaded += delegate
            {
                OnTick();
                if (Core.Homework.LoadFailed && !loadWarningShown)
                {
                    loadWarningShown = true;
                    string where = Core.Homework.LoadBackupPath.Length > 0
                        ? "（原文件已保留：" + Core.Homework.LoadBackupPath + "）"
                        : "";
                    ShowToast("作业数据读取失败，为避免覆盖，CC 没有丢弃原文件" + where, Palette.Warn);
                }
            };
            Closing += OnClosing;
            StateChanged += delegate
            {
                if (WindowState == WindowState.Maximized) Padding = new Thickness(0);
                else Padding = new Thickness(0);
            };
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                int round = 2; // DWMWCP_ROUND
                DwmSetWindowAttribute(hwnd, 33, ref round, 4);
                HwndSource src = HwndSource.FromHwnd(hwnd);
                if (src != null) src.AddHook(WndProc);
            }
            catch (Exception ex)
            {
                Log.Warn("窗口圆角或边框处理失败：" + ex.Message);
            }
        }

        private void BuildShell()
        {
            Grid root = new Grid();
            root.Background = Palette.Br(Palette.WindowBg);
            root.RowDefinitions.Add(new RowDefinition());
            root.RowDefinitions[0].Height = GridLength.Auto;
            root.RowDefinitions.Add(new RowDefinition());
            root.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);

            root.Children.Add(BuildHeader());

            Grid body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition());
            body.ColumnDefinitions[0].Width = new GridLength(196);
            body.ColumnDefinitions.Add(new ColumnDefinition());
            body.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            body.Children.Add(BuildNav());

            pageHost = new ContentControl();
            pageHost.Margin = new Thickness(22, 18, 22, 18);
            pageHost.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            pageHost.VerticalContentAlignment = VerticalAlignment.Stretch;
            Grid.SetColumn(pageHost, 1);
            body.Children.Add(pageHost);

            Grid.SetRow(body, 1);
            root.Children.Add(body);

            toastHost = new Grid();
            toastHost.VerticalAlignment = VerticalAlignment.Bottom;
            toastHost.HorizontalAlignment = HorizontalAlignment.Center;
            toastHost.Margin = new Thickness(0, 0, 0, 26);
            toastHost.IsHitTestVisible = false;
            root.Children.Add(toastHost);

            Content = root;
            ShowPage("timetable");
        }

        private FrameworkElement BuildHeader()
        {
            Border header = new Border();
            header.Background = Palette.Br(Palette.Surface);
            header.BorderBrush = Palette.Br(Palette.BorderSoft);
            header.BorderThickness = new Thickness(0, 0, 0, 1);
            header.Height = 62;
            header.MouseLeftButtonDown += delegate (object s, MouseButtonEventArgs e)
            {
                if (e.ClickCount == 2)
                {
                    WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                    return;
                }
                if (IsInteractive(e.OriginalSource)) return;
                try { DragMove(); }
                catch { }
            };

            Grid g = new Grid();
            g.Margin = new Thickness(18, 0, 12, 0);
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions[1].Width = GridLength.Auto;

            StackPanel left = new StackPanel();
            left.Orientation = Orientation.Horizontal;
            left.VerticalAlignment = VerticalAlignment.Center;
            Border mark = new Border();
            mark.Width = 34;
            mark.Height = 34;
            mark.CornerRadius = new CornerRadius(10);
            mark.Background = Palette.Br(Palette.Alpha(Palette.Accent, 0.20));
            mark.BorderBrush = Palette.Br(Palette.Alpha(Palette.Accent, 0.55));
            mark.BorderThickness = new Thickness(1);
            mark.Child = Ui.Icon(Glyph.Calendar, 17, Palette.Accent);
            mark.Margin = new Thickness(0, 0, 12, 0);
            left.Children.Add(mark);

            StackPanel titles = new StackPanel();
            titles.VerticalAlignment = VerticalAlignment.Center;
            headerTitle = Ui.Text("CampusClock", 15.5, Palette.TextPrimary, FontWeights.SemiBold);
            headerSub = Ui.Text("", 11.5, Palette.TextMuted);
            headerSub.Margin = new Thickness(0, 2, 0, 0);
            titles.Children.Add(headerTitle);
            titles.Children.Add(headerSub);
            left.Children.Add(titles);
            g.Children.Add(left);

            StackPanel right = new StackPanel();
            right.Orientation = Orientation.Horizontal;
            right.VerticalAlignment = VerticalAlignment.Center;

            TextBtn import = TextBtn.Primary("导入课表", Palette.Accent);
            import.Margin = new Thickness(0, 0, 8, 0);
            import.Clicked += delegate { PromptImport(); };
            right.Children.Add(import);

            ballToggle = TextBtn.Ghost(Core.Config.BallEnabled ? "悬浮球：开" : "悬浮球：关");
            ballToggle.Margin = new Thickness(0, 0, 12, 0);
            ballToggle.Clicked += delegate
            {
                Core.Config.BallEnabled = !Core.Config.BallEnabled;
                Core.SaveConfig();
                UpdateBallToggle();
                ApplyConfigLive();
            };
            right.Children.Add(ballToggle);

            IconBtn min = new IconBtn(Glyph.Minimize, 11, Palette.TextSecondary, 38);
            min.ToolTip = "最小化";
            min.Clicked += delegate { WindowState = WindowState.Minimized; };
            IconBtn max = new IconBtn(Glyph.Maximize, 11, Palette.TextSecondary, 38);
            max.ToolTip = "最大化 / 还原";
            max.Clicked += delegate
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            };
            IconBtn close = new IconBtn(Glyph.Close, 11, Palette.TextSecondary, 38);
            close.ToolTip = Core.Config.TrayEnabled ? "关闭（最小化到托盘）" : "退出";
            close.Clicked += delegate { Close(); };
            right.Children.Add(min);
            right.Children.Add(max);
            right.Children.Add(close);
            Grid.SetColumn(right, 1);
            g.Children.Add(right);

            header.Child = g;
            return header;
        }

        private bool IsInteractive(object source)
        {
            DependencyObject d = source as DependencyObject;
            while (d != null)
            {
                if (d is TextBox) return true;
                if (d is Border)
                {
                    Border b = (Border)d;
                    if (b.Cursor == Cursors.Hand) return true;
                    if (b is IconBtn || b is TextBtn || b is Chip) return true;
                }
                d = VisualTreeHelper.GetParent(d);
            }
            return false;
        }

        private FrameworkElement BuildNav()
        {
            Border panel = new Border();
            panel.Background = Palette.Br(Palette.Surface);
            panel.BorderBrush = Palette.Br(Palette.BorderSoft);
            panel.BorderThickness = new Thickness(0, 0, 1, 0);
            Grid g = new Grid();
            g.Margin = new Thickness(12, 16, 12, 14);
            g.RowDefinitions.Add(new RowDefinition());
            g.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
            g.RowDefinitions.Add(new RowDefinition());
            g.RowDefinitions[1].Height = GridLength.Auto;

            navPanel = new StackPanel();
            navItems.Add(NavItem(Glyph.Calendar, "课表", "timetable"));
            navItems.Add(NavItem(Glyph.Checklist, "作业记录", "homework"));
            navItems.Add(NavItem(Glyph.Settings, "显示设置", "settings"));
            for (int i = 0; i < navItems.Count; i++) navPanel.Children.Add(navItems[i]);
            g.Children.Add(navPanel);

            StackPanel bottom = new StackPanel();
            IconBtn folder = new IconBtn(Glyph.Folder, 13, Palette.TextSecondary, 34);
            folder.HorizontalAlignment = HorizontalAlignment.Left;
            folder.ToolTip = "打开数据目录";
            folder.Clicked += delegate { SettingsPage.OpenPath(Paths.DataDir); };
            TextBlock ver = Ui.Text("本地运行 · 无联网", 10.5, Palette.TextMuted);
            ver.Margin = new Thickness(2, 10, 0, 0);
            bottom.Children.Add(folder);
            bottom.Children.Add(ver);
            Grid.SetRow(bottom, 1);
            g.Children.Add(bottom);

            panel.Child = g;
            return panel;
        }

        private Border NavItem(string glyph, string label, string page)
        {
            Border item = new Border();
            item.CornerRadius = new CornerRadius(10);
            item.Padding = new Thickness(12, 10, 12, 10);
            item.Margin = new Thickness(0, 0, 0, 6);
            item.Cursor = Cursors.Hand;
            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            TextBlock icon = Ui.Icon(glyph, 15, Palette.TextSecondary);
            icon.Margin = new Thickness(0, 0, 10, 0);
            TextBlock text = Ui.Text(label, 13.5, Palette.TextSecondary);
            sp.Children.Add(icon);
            sp.Children.Add(text);
            item.Child = sp;
            item.Tag = page;
            item.MouseEnter += delegate
            {
                if ((string)item.Tag != currentPage) item.Background = Palette.Br(Palette.Hover);
            };
            item.MouseLeave += delegate
            {
                if ((string)item.Tag != currentPage) item.Background = Palette.Br(Colors.Transparent);
            };
            item.MouseLeftButtonUp += delegate { ShowPage(page); };
            return item;
        }

        private void HighlightNav()
        {
            for (int i = 0; i < navItems.Count; i++)
            {
                Border item = navItems[i];
                bool active = (string)item.Tag == currentPage;
                StackPanel sp = (StackPanel)item.Child;
                TextBlock icon = (TextBlock)sp.Children[0];
                TextBlock text = (TextBlock)sp.Children[1];
                item.Background = Palette.Br(active ? Palette.Alpha(Palette.Accent, 0.15) : Colors.Transparent);
                icon.Foreground = Palette.Br(active ? Palette.Accent : Palette.TextSecondary);
                text.Foreground = Palette.Br(active ? Palette.Accent : Palette.TextSecondary);
                text.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
            }
        }

        public void ShowPage(string page)
        {
            currentPage = page;
            if (timetablePage == null)
            {
                timetablePage = new TimetablePage(Core);
                timetablePage.Rebuild();
            }
            if (homeworkPage == null) homeworkPage = new HomeworkPage(Core);
            if (settingsPage == null)
            {
                settingsPage = new SettingsPage(Core);
                settingsPage.ImportRequested += delegate { PromptImport(); };
            }
            if (page == "timetable") pageHost.Content = timetablePage;
            else if (page == "homework") pageHost.Content = homeworkPage;
            else pageHost.Content = settingsPage;
            HighlightNav();
            if (pageHost.Content is FrameworkElement)
            {
                Ui.Animate((FrameworkElement)pageHost.Content, 0, 0);
                Ui.Animate((FrameworkElement)pageHost.Content, 1, 160);
            }
            if (page == "timetable") timetablePage.Rebuild();
            if (page == "homework") homeworkPage.Build();
            UpdateHeader();
        }

        public void UpdateHeader()
        {
            try
            {
                DateTime today = DateTime.Today;
                int index = Core.Schedule != null ? Core.Schedule.WeekIndex(today) : 1;
                string week = index >= 1 ? "第 " + index + " 周" : "开学前 " + (1 - index) + " 周";
                string text = week + TextInfoSeparator + Fmt.ShortDate(Schedule.WeekStart(today)) + " ~ " +
                    Fmt.ShortDate(Schedule.WeekStart(today).AddDays(6));
                if (Core.Schedule != null && Core.Schedule.Doc != null && Core.Schedule.Doc.Rules.Count > 0)
                {
                    List<Session> todaySessions = new List<Session>();
                    List<Session> weekSessions = Core.Schedule.WeekSessions(Schedule.WeekStart(today));
                    for (int i = 0; i < weekSessions.Count; i++)
                    {
                        if (weekSessions[i].Start.Date == today) todaySessions.Add(weekSessions[i]);
                    }
                    text += TextInfoSeparator + "今天 " + todaySessions.Count + " 节课";
                    Session next = Core.Schedule.NextSession(DateTime.Now, 14);
                    if (next != null)
                    {
                        text += TextInfoSeparator + "下一节 " + Fmt.WeekDayCN(next.Start.DayOfWeek) + " " +
                            next.Start.ToString("HH:mm", CultureInfo.InvariantCulture) + " " + next.Course;
                    }
                }
                headerSub.Text = text;
            }
            catch (Exception ex)
            {
                Log.Warn("更新标题栏信息失败：" + ex.Message);
            }
        }

        private void UpdateBallToggle()
        {
            if (ballToggle != null) ballToggle.SetText(Core.Config.BallEnabled ? "悬浮球：开" : "悬浮球：关");
        }

        private void OnTick()
        {
            try
            {
                UpdateHeader();
                if (timetablePage != null && currentPage == "timetable") timetablePage.RefreshMetrics();
                List<string> cleared = Core.CheckAutoClear(DateTime.Now);
                if (cleared.Count > 0)
                {
                    string names = "";
                    for (int i = 0; i < cleared.Count; i++)
                    {
                        if (i > 0) names += "、";
                        names += cleared[i];
                    }
                    ShowToast("《" + names + "》上完课，作业已清空，请填写新作业", Palette.Accent);
                    if (homeworkPage != null) homeworkPage.SyncFromModel();
                    if (widget != null) widget.RefreshContent();
                }
            }
            catch (Exception ex)
            {
                Log.Error("定时刷新失败：" + ex.Message);
            }
        }

        public void ShowToast(string message, Color accent)
        {
            try
            {
                toastHost.Children.Clear();
                Grid g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions[0].Width = new GridLength(3);
                g.ColumnDefinitions.Add(new ColumnDefinition());
                Border bar = new Border();
                bar.Background = Palette.Br(accent);
                bar.CornerRadius = new CornerRadius(2);
                bar.Margin = new Thickness(0, 6, 0, 6);
                TextBlock text = Ui.Text(message, 12.5, Palette.TextPrimary);
                text.Margin = new Thickness(12, 10, 18, 10);
                text.MaxWidth = 620;
                text.TextWrapping = TextWrapping.Wrap;
                Grid.SetColumn(text, 1);
                g.Children.Add(bar);
                g.Children.Add(text);
                toastCard = Ui.Card(g, 12, Palette.Elevated, Palette.Border, new Thickness(0));
                toastCard.Effect = Ui.Shadow(20, 0.45);
                toastHost.Children.Add(toastCard);
                Ui.Animate(toastCard, 0, 0);
                Ui.Animate(toastCard, 1, 200);
                System.Windows.Threading.DispatcherTimer hide = new System.Windows.Threading.DispatcherTimer();
                hide.Interval = TimeSpan.FromSeconds(9);
                hide.Tick += delegate
                {
                    hide.Stop();
                    Border card = toastCard;
                    if (card == null) return;
                    DoubleAnimation fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(300));
                    fade.Completed += delegate
                    {
                        if (toastHost.Children.Contains(card)) toastHost.Children.Remove(card);
                    };
                    card.BeginAnimation(UIElement.OpacityProperty, fade);
                };
                hide.Start();
            }
            catch (Exception ex)
            {
                Log.Warn("显示提示失败：" + ex.Message);
            }
        }

        public void PromptImport()
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Title = "选择课表文件";
            dlg.Filter = "日历文件 (*.ics)|*.ics|所有文件 (*.*)|*.*";
            dlg.CheckFileExists = true;
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (!string.IsNullOrEmpty(desktop)) dlg.InitialDirectory = desktop;
            }
            catch
            {
            }
            bool? ok = dlg.ShowDialog(this);
            if (ok != true) return;
            string error;
            if (Core.ImportIcs(dlg.FileName, out error))
            {
                int count = Core.Doc != null ? Core.Doc.Courses().Count : 0;
                ShowToast("课表导入成功，共 " + count + " 门课程", Palette.Good);
                RefreshAll();
            }
            else
            {
                ShowToast("导入失败：" + error, Palette.Danger);
            }
        }

        public void RefreshAll()
        {
            if (timetablePage != null) timetablePage.Rebuild();
            if (homeworkPage != null) homeworkPage.Build();
            if (settingsPage != null) { settingsPage.Build(); }
            UpdateHeader();
            if (widget != null) widget.RefreshContent();
        }

        public void ApplyConfigLive()
        {
            UpdateBallToggle();
            if (timetablePage != null && currentPage == "timetable") timetablePage.Rebuild();
            if (settingsPage != null) settingsPage.RefreshPreview();
            if (widget != null) widget.ApplyConfig();
            if (Core.Config.BallEnabled)
            {
                if (widget == null) CreateWidget();
                else widget.Show();
            }
            else if (widget != null)
            {
                widget.Hide();
            }
        }

        private void CreateTray()
        {
            if (!Core.Config.TrayEnabled) return;
            if (tray != null) return;
            tray = new TrayIcon("CampusClock");
            tray.OpenRequested += delegate { RestoreFromTray(); };
            tray.ImportRequested += delegate { RestoreFromTray(); PromptImport(); };
            tray.BallToggleRequested += delegate
            {
                Core.Config.BallEnabled = !Core.Config.BallEnabled;
                Core.SaveConfig();
                UpdateBallToggle();
                ApplyConfigLive();
            };
            tray.ExitRequested += delegate { ExitApp(); };
            tray.SetBallEnabled(Core.Config.BallEnabled);
        }

        private void CreateWidget()
        {
            if (widget != null) return;
            widget = new Widget(Core, this);
            widget.Show();
            Log.Info("悬浮球已创建");
        }

        public void RestoreFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
        }

        public void ExitApp()
        {
            reallyClose = true;
            try
            {
                Core.SaveHomeworkNow();
                Core.SaveConfig();
            }
            catch
            {
            }
            if (tray != null) tray.Dispose();
            if (widget != null) widget.AllowClose();
            Application.Current.Shutdown();
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (reallyClose) return;
            if (Core.Config.TrayEnabled)
            {
                e.Cancel = true;
                Hide();
                if (!trayHintShown)
                {
                    trayHintShown = true;
                    if (tray != null) tray.ShowBalloon("CampusClock 仍在后台运行", "双击托盘图标可重新打开窗口，右键可退出。");
                }
            }
            else
            {
                reallyClose = true;
                ExitApp();
            }
        }

        // ---- interop: keep a maximized borderless window inside the work area ----

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT Reserved;
            public POINT MaxSize;
            public POINT MaxPosition;
            public POINT MinTrackSize;
            public POINT MaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0024) // WM_GETMINMAXINFO
            {
                try
                {
                    MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));
                    IntPtr monitor = MonitorFromWindow(hwnd, 2);
                    if (monitor != IntPtr.Zero)
                    {
                        MONITORINFO mi = new MONITORINFO();
                        mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                        if (GetMonitorInfo(monitor, ref mi))
                        {
                            mmi.MaxPosition.X = mi.rcWork.Left - mi.rcMonitor.Left;
                            mmi.MaxPosition.Y = mi.rcWork.Top - mi.rcMonitor.Top;
                            mmi.MaxSize.X = mi.rcWork.Right - mi.rcWork.Left;
                            mmi.MaxSize.Y = mi.rcWork.Bottom - mi.rcWork.Top;
                            mmi.MaxTrackSize.X = mmi.MaxSize.X;
                            mmi.MaxTrackSize.Y = mmi.MaxSize.Y;
                            Marshal.StructureToPtr(mmi, lParam, true);
                        }
                    }
                }
                catch
                {
                }
            }
            return IntPtr.Zero;
        }
    }
}
