using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CampusClock
{
    /// <summary>Headless checks used during development: parse, build UI tree, exercise logic.</summary>
    public static class Selftest
    {
        private static StringBuilder log = new StringBuilder();
        private static int failures;
        public static string ShotDir;

        private static void Say(string text)
        {
            log.AppendLine(text);
            Console.WriteLine(text);
        }

        private static void Check(string name, bool ok, string detail = null)
        {
            if (!ok) failures++;
            Say((ok ? "  PASS  " : "  FAIL  ") + name + (detail != null && detail.Length > 0 ? "   -> " + detail : ""));
        }

        public static int Run(string icsPath)
        {
            Say("CampusClock selftest " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            Say("AppDir  : " + Paths.AppDir);
            Say("DataDir : " + Paths.DataDir);
            Say("ICS     : " + (icsPath == null ? "(none)" : icsPath));

            if (Application.Current == null)
            {
                Application app = new Application();
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }
            ThemeResources.Apply();
            Say("主题样式加载完成");

            // ---- 1. parse ----
            IcsDocument doc = null;
            if (icsPath != null && File.Exists(icsPath))
            {
                doc = Ics.Load(icsPath);
                Say("解析结果：事件 " + doc.EventCount + " 个，课程规则 " + doc.Rules.Count +
                    " 条，跳过 " + doc.SkippedEvents + " 个，警告 " + doc.Warnings.Count + " 条");
                Check("解析出课程规则", doc.Rules.Count > 0, doc.Rules.Count.ToString(CultureInfo.InvariantCulture));
                Check("解析出课程名称", doc.Courses().Count > 0,
                    string.Join(" / ", doc.Courses().ToArray()));
                for (int i = 0; i < doc.Warnings.Count; i++) Say("    警告：" + doc.Warnings[i]);
                for (int i = 0; i < doc.Rules.Count && i < 8; i++)
                {
                    CourseRule r = doc.Rules[i];
                    Say("    " + Fmt.WeekDayCN(r.Day) + " " + r.TimeText + "  " + r.Summary + "  @" + r.Location +
                        "  " + r.PeriodLabel + "  首次 " + Fmt.Date(r.FirstDate) +
                        "  最后一节 " + Fmt.Date(r.LastOccurrence()) +
                        "  间隔 " + r.IntervalWeeks + " 周");
                }
                Say("学期范围：" + Fmt.Date(doc.FirstDate()) + " ~ " + Fmt.Date(doc.LastDate()));
            }
            else
            {
                Check("找到 .ics 文件", false, "未提供课表文件");
            }

            Schedule schedule = new Schedule(doc != null ? doc : new IcsDocument());

            // ---- 2. schedule queries ----
            Say("时段行数：" + schedule.Rows.Count);
            for (int i = 0; i < schedule.Rows.Count; i++)
            {
                Say("    第 " + (i + 1) + " 行  " + schedule.Rows[i].Label + "  " + schedule.Rows[i].TimeText);
            }
            DateTime weekStart = Schedule.WeekStart(DateTime.Today);
            List<Session> weekSessions = schedule.WeekSessions(weekStart);
            Say("本周（" + Fmt.Date(weekStart) + " 起）课程节次：" + weekSessions.Count);
            int weeksChecked = 0;
            DateTime probe = Schedule.WeekStart(new DateTime(2026, 9, 7));
            int total = 0;
            for (int i = 0; i < 20; i++)
            {
                int c = schedule.WeekSessions(probe.AddDays(i * 7)).Count;
                total += c;
                weeksChecked++;
            }
            Say("模拟前 20 周总课次：" + total);
            Check("学期内有课程", total > 0, total.ToString(CultureInfo.InvariantCulture));
            Check("周次计算", schedule.WeekIndex(new DateTime(2026, 9, 7)) == 1,
                "9/7 应为第 1 周，实际第 " + schedule.WeekIndex(new DateTime(2026, 9, 7)) + " 周");
            Session next = schedule.NextSession(DateTime.Now, 14);
            if (next != null)
            {
                Say("下一节课：" + Fmt.DateTimeText(next.Start) + " " + next.Course + " @" + next.Location);
            }
            else
            {
                Say("下一节课：无（可能已过学期末或课表为空）");
            }

            // ---- 3. json round trip ----
            AppConfig cfg = new AppConfig();
            cfg.FontSize = 17;
            cfg.ShowWeekend = true;
            cfg.Accent = "#4CC38A";
            cfg.TrackedCourses.Add("测试课程 \"A\"\n第二行");
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["FontSize"] = cfg.FontSize;
            d["ShowWeekend"] = cfg.ShowWeekend;
            d["Accent"] = cfg.Accent;
            d["TrackedCourses"] = cfg.TrackedCourses;
            string json = Json.Pretty(d);
            Dictionary<string, object> back = Json.AsDict(Json.Parse(json));
            Check("JSON 往返：字体", Json.GetInt(back, "FontSize", 0) == 17, json.Replace("\n", " "));
            Check("JSON 往返：开关", Json.GetBool(back, "ShowWeekend", false));
            Check("JSON 往返：列表", Json.GetStrings(back, "TrackedCourses").Count == 1);

            // ---- 4. homework logic ----
            AppCore core = new AppCore();
            core.Schedule = schedule;
            core.Doc = doc != null ? doc : new IcsDocument();
            if (core.Config.TrackedCourses.Count == 0 && core.Doc.Courses().Count > 0)
            {
                core.Config.TrackedCourses.Add(core.Doc.Courses()[0]);
            }
            if (core.Config.TrackedCourses.Count > 0)
            {
                string course = core.Config.TrackedCourses[0];
                HomeworkItem item = core.Homework.Ensure(course);
                item.Text = "第三章习题";
                item.Done = false;
                item.LastClearedKey = "";
                DateTime fake = DateTime.Today.AddHours(23);
                core.CheckAutoClear(fake);
                Check("首次运行仅建立基准，不清空", item.Text == "第三章习题", "Text=" + item.Text);
                string baseline = item.LastClearedKey;
                Say("基准键：" + baseline);
                DateTime later = WeekStartPlus(schedule, fake, course);
                core.CheckAutoClear(later);
                Check("下一次课后清空内容", item.Text == "" && !item.Done,
                    "Text='" + item.Text + "' Key=" + item.LastClearedKey);

                // 4b. 切换"清空时机"绝不能误清空（2026-09-13 17:12 用户作业被吞的根因）
                item.Text = "第五章 1-5 题";
                item.Done = false;
                item.LastClearedKey = "";
                string savedMode = core.Config.ClearMode;
                core.Config.ClearMode = "PerDay";
                core.CheckAutoClear(fake);
                Check("4b PerDay 首次仅建立基准", item.Text == "第五章 1-5 题", "Text=" + item.Text);
                Say("    PerDay 基准键：" + item.LastClearedKey);
                core.Config.ClearMode = "PerSession";
                core.CheckAutoClear(fake);
                Check("4b 切到 PerSession 不误清空", item.Text == "第五章 1-5 题",
                    "Text=" + item.Text + " Key=" + item.LastClearedKey);
                core.Config.ClearMode = "PerDay";
                core.CheckAutoClear(fake);
                Check("4b 切回 PerDay 不误清空", item.Text == "第五章 1-5 题",
                    "Text=" + item.Text + " Key=" + item.LastClearedKey);
                int before = item.Text.Length;
                core.CheckAutoClear(later);
                Check("4b 真正到了下一节课仍然清空", before > 0 && item.Text.Length == 0 && !item.Done,
                    "Text='" + item.Text + "' Key=" + item.LastClearedKey);
                core.Config.ClearMode = savedMode;
                // 4c. 旧版遗留的"只有日期"的键，在 PerSession 下不得被当成新的一节课
                item.Text = "第六章 1-3 题";
                item.Done = false;
                Session lastSession = schedule.LatestFinishedSession(course, fake, 21);
                item.LastClearedKey = lastSession != null ? Fmt.Date(lastSession.Start) : Fmt.Date(fake);
                core.Config.ClearMode = "PerSession";
                core.CheckAutoClear(fake);
                Check("4c 遗留日期键不误清空", item.Text == "第六章 1-3 题",
                    "Text=" + item.Text + " Key=" + item.LastClearedKey);
                core.Config.ClearMode = savedMode;
            }
            else
            {
                Say("跳过作业清空测试（没有课程）");
            }

            HomeworkV103Check(core, schedule);

            // ---- 5. build UI trees (no window shown) ----
            try
            {
                MainWindow win = new MainWindow(core);
                win.Width = 1220;
                win.Height = 820;
                ForceLayout(win, 1220, 820);
                Say("主窗口构建完成");
                Shot("01-timetable.png", win.Content as FrameworkElement, 1220, 820);
                win.ShowPage("homework");
                ForceLayout(win, 1220, 820);
                Shot("02-homework.png", win.Content as FrameworkElement, 1220, 820);
                win.ShowPage("settings");
                ForceLayout(win, 1220, 820);
                Shot("03-settings.png", win.Content as FrameworkElement, 1220, 820);
                Say("三个页面（课表 / 作业 / 显示设置）构建完成");
                Check("主窗口内容非空", win.Content != null);
                win.Close();
            }
            catch (Exception ex)
            {
                Check("主窗口构建", false, ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace);
            }

            try
            {
                Widget w = new Widget(core, null);
                w.ApplyConfig();
                double ballW = w.Width;
                double ballH = w.Height;
                Say("悬浮球收起尺寸：" + ballW.ToString("0", CultureInfo.InvariantCulture) + " x " +
                    ballH.ToString("0", CultureInfo.InvariantCulture) + " px");
                Check("悬浮球加宽（宽 ≥ 高 × 1.4）", ballW >= ballH * 1.4,
                    ballW.ToString("0", CultureInfo.InvariantCulture) + " x " +
                    ballH.ToString("0", CultureInfo.InvariantCulture));
                BallLayoutCheck(w, ballW, ballH);
                int savedAnim = core.Config.AnimationMs;
                core.Config.AnimationMs = 0;
                w.Expand("timetable");
                ForceLayout(w, 600, 600);
                w.RefreshContent();
                Shot("04-widget-timetable.png", w.Content as FrameworkElement, 600, 600);
                w.Expand("homework");
                w.RefreshContent();
                ForceLayout(w, 600, 600);
                Shot("05-widget-homework.png", w.Content as FrameworkElement, 600, 600);
                core.Config.AnimationMs = savedAnim;
                Say("悬浮球（课表 / 作业两种展开）构建完成");
                w.AllowClose();
                w.Close();
                Check("悬浮球构建", true, "");
            }
            catch (Exception ex)
            {
                Check("悬浮球构建", false, ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace);
            }

            WidgetInteractionCheck(core);

            try
            {
                System.Drawing.Icon ico = TrayIcon.CreateIcon(32);
                Check("托盘图标绘制", ico != null && ico.Width > 0, ico == null ? "null" : ico.Width + "px");
                ico.Dispose();
            }
            catch (Exception ex)
            {
                Check("托盘图标绘制", false, ex.Message);
            }

            // ---- 6. compact renderer at several sizes ----
            double[] sizes = new double[] { 320, 420, 520, 720 };
            for (int i = 0; i < sizes.Length; i++)
            {
                try
                {
                    TimetableRenderer.Metrics m = TimetableRenderer.ComputeMetrics(core.Config, schedule, true, sizes[i], sizes[i]);
                    FrameworkElement el = TimetableRenderer.Build(schedule, core.Config, weekStart, true, sizes[i], sizes[i]);
                    ForceLayout(el, sizes[i], sizes[i]);
                    Check("紧凑渲染 " + sizes[i] + "px", el != null,
                        "字号 " + m.FontSize.ToString("0.0", CultureInfo.InvariantCulture) +
                        " 列宽 " + m.CellWidth.ToString("0.0", CultureInfo.InvariantCulture) +
                        " 每小时 " + m.PxPerHour.ToString("0.0", CultureInfo.InvariantCulture) + "px" +
                        " 轴范围 " + Fmt.Minutes(m.RangeStartMin) + "-" + Fmt.Minutes(m.RangeEndMin));
                }
                catch (Exception ex)
                {
                    Check("紧凑渲染 " + sizes[i] + "px", false, ex.Message);
                }
            }

            // ---- 7. time axis with unusual / overlapping class times ----
            try
            {
                string weird = Path.Combine(Paths.DataDir, "selftest-weird.ics");
                File.WriteAllText(weird, WeirdIcs(), Encoding.UTF8);
                IcsDocument wdoc = Ics.Load(weird);
                Schedule wschedule = new Schedule(wdoc);
                int rs;
                int re;
                TimetableRenderer.RangeMinutes(wschedule, out rs, out re);
                Say("不规则时间轴范围：" + Fmt.Minutes(rs) + " - " + Fmt.Minutes(re));
                Check("时间轴覆盖 07:30 与 22:40 的课", rs <= 7 * 60 + 30 && re >= 22 * 60 + 40,
                    Fmt.Minutes(rs) + "-" + Fmt.Minutes(re));
                List<Session> wsess = wschedule.WeekSessions(Schedule.WeekStart(new DateTime(2026, 9, 7)));
                Session longOne = null;
                Session clash = null;
                for (int i = 0; i < wsess.Count; i++)
                {
                    if (wsess[i].Course == "超长实验课") longOne = wsess[i];
                    if (wsess[i].Course == "冲突研讨课") clash = wsess[i];
                }
                Check("找到 15:10-18:00 的超长课", longOne != null);
                Check("找到与其重叠的课", clash != null);
                if (longOne != null && clash != null)
                {
                    Check("重叠课自动分列", longOne.LaneCount == 2 && clash.LaneCount == 2 && longOne.Lane != clash.Lane,
                        "lane=" + longOne.Lane + "/" + clash.Lane + " count=" + longOne.LaneCount);
                }
                if (longOne != null)
                {
                    TimetableRenderer.Metrics wm = TimetableRenderer.ComputeMetrics(core.Config, wschedule, false, 900, 600);
                    double expected = 170 * wm.PxPerMin;   // 15:10 -> 18:00
                    double height = (longOne.Rule.EndMin - longOne.Rule.StartMin) * wm.PxPerMin;
                    Check("超长课高度按比例", Math.Abs(height - expected) < 0.01,
                        "height=" + height.ToString("0.0", CultureInfo.InvariantCulture) + "px");
                    Say("    15:10-18:00 课程高度：" + height.ToString("0.0", CultureInfo.InvariantCulture) +
                        "px（1 小时 = " + wm.PxPerHour.ToString("0", CultureInfo.InvariantCulture) + "px）");
                    Check("1 小时课更矮", (60 * wm.PxPerMin) < height);
                }
                FrameworkElement weirdEl = TimetableRenderer.Build(wschedule, core.Config,
                    Schedule.WeekStart(new DateTime(2026, 9, 7)), false, 900, 700);
                Border host = new Border();
                host.Background = Palette.Br(Palette.WindowBg);
                host.Padding = new Thickness(14);
                host.Child = weirdEl;
                ForceLayout(host, 900, 700);
                Shot("06-weird-times.png", host, 900, 700);
                Check("不规则时间课表渲染", weirdEl != null);
                File.Delete(weird);
            }
            catch (Exception ex)
            {
                Check("不规则时间测试", false, ex.GetType().Name + ": " + ex.Message);
            }

            Say("");
            Say(failures == 0 ? "全部检查通过 ✔" : ("存在 " + failures + " 项失败 ✘"));
            try
            {
                File.WriteAllText(Path.Combine(Paths.DataDir, "selftest.log"), log.ToString(), Encoding.UTF8);
            }
            catch
            {
            }
            return failures == 0 ? 0 : 1;
        }

        private static string WeirdIcs()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("BEGIN:VCALENDAR");
            sb.AppendLine("VERSION:2.0");
            sb.AppendLine("PRODID:-//CampusClock//selftest//CN");
            sb.AppendLine("BEGIN:VTIMEZONE");
            sb.AppendLine("TZID:Asia/Shanghai");
            sb.AppendLine("BEGIN:STANDARD");
            sb.AppendLine("TZOFFSETFROM:+0800");
            sb.AppendLine("TZOFFSETTO:+0800");
            sb.AppendLine("DTSTART:19700101T000000");
            sb.AppendLine("END:STANDARD");
            sb.AppendLine("END:VTIMEZONE");
            sb.Append(Event("超长实验课", "20260907T151000", "20260907T180000", "实验楼B301", "第7 - 9节\\n实验楼B301"));
            sb.Append(Event("冲突研讨课", "20260907T160000", "20260907T170000", "研讨室1", "\\n研讨室1"));
            sb.Append(Event("清晨早课", "20260907T073000", "20260907T084500", "一教101", "第0 - 1节\\n一教101"));
            sb.Append(Event("深夜选修", "20260907T201000", "20260907T224000", "线上", "第12 - 14节\\n线上"));
            sb.AppendLine("END:VCALENDAR");
            return sb.ToString();
        }

        private static string Event(string summary, string start, string end, string location, string description)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine("UID:selftest-" + summary);
            sb.AppendLine("SUMMARY:" + summary);
            sb.AppendLine("DTSTART;TZID=Asia/Shanghai:" + start);
            sb.AppendLine("DTEND;TZID=Asia/Shanghai:" + end);
            sb.AppendLine("LOCATION:" + location);
            sb.AppendLine("DESCRIPTION:" + description);
            sb.AppendLine("RRULE:FREQ=WEEKLY;UNTIL=20270104T160000Z;INTERVAL=1");
            sb.AppendLine("END:VEVENT");
            return sb.ToString();
        }

        private static DateTime WeekStartPlus(Schedule schedule, DateTime baseTime, string course)
        {
            // find a moment after the next occurrence of the course so the clear triggers
            Session next = null;
            for (int i = 1; i <= 28; i++)
            {
                List<Session> list = schedule.WeekSessions(Schedule.WeekStart(baseTime).AddDays(-7 * i));
                for (int j = 0; j < list.Count; j++)
                {
                    if (list[j].Course == course && list[j].End < baseTime)
                    {
                        next = list[j];
                        break;
                    }
                }
                if (next != null) break;
            }
            if (next == null) return baseTime.AddDays(7);
            return next.End.AddMinutes(5);
        }

        public static void ForceLayout(FrameworkElement el, double w, double h)
        {
            el.Width = w;
            el.Height = h;
            el.Measure(new Size(w, h));
            el.Arrange(new Rect(0, 0, w, h));
            el.UpdateLayout();
        }

        /// <summary>Render a visual tree to a PNG (used to eyeball the layout without a screen).</summary>
        private static void Shot(string fileName, FrameworkElement el, int w, int h, int minColors = 60)
        {
            if (ShotDir == null || el == null) return;
            try
            {
                if (!Directory.Exists(ShotDir)) Directory.CreateDirectory(ShotDir);
                ForceLayout(el, w, h);
                RenderTargetBitmap bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                bmp.Render(el);
                PngBitmapEncoder enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bmp));
                using (FileStream fs = new FileStream(Path.Combine(ShotDir, fileName), FileMode.Create))
                {
                    enc.Save(fs);
                }
                int colors;
                double mean;
                Analyze(bmp, out colors, out mean);
                Say("    截图 " + fileName + "  颜色数 " + colors + "  平均亮度 " +
                    mean.ToString("0.0", CultureInfo.InvariantCulture));
                Check("截图非空白 " + fileName, colors > minColors && (mean > 8 || colors > minColors),
                    "colors=" + colors);
            }
            catch (Exception ex)
            {
                Check("截图 " + fileName, false, ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void Analyze(RenderTargetBitmap bmp, out int colors, out double mean)
        {
            int stride = bmp.PixelWidth * 4;
            byte[] pixels = new byte[stride * bmp.PixelHeight];
            bmp.CopyPixels(pixels, stride, 0);
            HashSet<int> set = new HashSet<int>();
            long sum = 0;
            int count = 0;
            for (int y = 0; y < bmp.PixelHeight; y += 2)
            {
                for (int x = 0; x < bmp.PixelWidth; x += 2)
                {
                    int i = y * stride + x * 4;
                    int b = pixels[i];
                    int g = pixels[i + 1];
                    int r = pixels[i + 2];
                    set.Add((r >> 3) << 10 | (g >> 3) << 5 | (b >> 3));
                    sum += (r + g + b) / 3;
                    count++;
                }
            }
            colors = set.Count;
            mean = count == 0 ? 0 : (double)sum / count;
        }

        /// <summary>Checks that icon + caption are centred inside each half of the collapsed ball.</summary>
        private static void BallLayoutCheck(Widget w, double ballW, double ballH)
        {
            try
            {
                int iw = (int)Math.Round(ballW);
                int ih = (int)Math.Round(ballH);
                FrameworkElement el = w.Content as FrameworkElement;
                if (el == null || iw < 40 || ih < 30)
                {
                    Check("悬浮球布局", false, "尺寸异常 " + iw + "x" + ih);
                    return;
                }
                ForceLayout(el, iw, ih);
                RenderTargetBitmap bmp = new RenderTargetBitmap(iw, ih, 96, 96, PixelFormats.Pbgra32);
                bmp.Render(el);
                if (ShotDir != null)
                {
                    Image img = new Image();
                    img.Source = bmp;
                    img.Width = iw;
                    img.Height = ih;
                    Border host = new Border();
                    host.Background = Palette.Br(Palette.WindowBg);
                    host.Padding = new Thickness(12);
                    host.Child = img;
                    ForceLayout(host, iw + 24, ih + 24);
                    Shot("07-widget-ball.png", host, iw + 24, ih + 24, 20);
                }

                int pad = 5;
                double lx, ly, rx, ry;
                bool leftInk = Centroid(bmp, pad, iw / 2, pad, ih - pad, out lx, out ly);
                bool rightInk = Centroid(bmp, iw / 2, iw - pad, pad, ih - pad, out rx, out ry);
                Check("悬浮球左右两半都有图标文字", leftInk && rightInk,
                    "left=" + leftInk + " right=" + rightInk);
                double halfW = iw / 2.0;
                double tolX = halfW * 0.15;
                double tolY = ih * 0.18;
                Say("    左半重心 (" + lx.ToString("0.0", CultureInfo.InvariantCulture) + ", " +
                    ly.ToString("0.0", CultureInfo.InvariantCulture) + ")  期望 x=" +
                    (halfW / 2).ToString("0.0", CultureInfo.InvariantCulture) + " y=" +
                    (ih / 2.0).ToString("0.0", CultureInfo.InvariantCulture));
                Say("    右半重心 (" + rx.ToString("0.0", CultureInfo.InvariantCulture) + ", " +
                    ry.ToString("0.0", CultureInfo.InvariantCulture) + ")  期望 x=" +
                    (halfW * 1.5).ToString("0.0", CultureInfo.InvariantCulture) + " y=" +
                    (ih / 2.0).ToString("0.0", CultureInfo.InvariantCulture));
                Check("左半内容水平居中", Math.Abs(lx - halfW / 2) <= tolX,
                    "dx=" + (lx - halfW / 2).ToString("0.0", CultureInfo.InvariantCulture));
                Check("右半内容水平居中", Math.Abs(rx - halfW * 1.5) <= tolX,
                    "dx=" + (rx - halfW * 1.5).ToString("0.0", CultureInfo.InvariantCulture));
                Check("左右内容垂直居中", Math.Abs(ly - ih / 2.0) <= tolY && Math.Abs(ry - ih / 2.0) <= tolY,
                    "dy=" + (ly - ih / 2.0).ToString("0.0", CultureInfo.InvariantCulture) + "/" +
                    (ry - ih / 2.0).ToString("0.0", CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                Check("悬浮球布局", false, ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Drives the floating widget through the interaction scenarios that used to break
        /// (expand / close / hover again / click while expanded / drag / lost mouse-up).
        /// </summary>
        /// <summary>
        /// v1.0.3 作业专项：提交方式、更新按钮、自动清空可恢复、取消跟踪不丢数据、旧数据兼容、损坏文件保护。
        /// 全部走真实代码路径（Storage / AppCore / HomeworkPage / HomeworkRow）。
        /// </summary>
        private static void HomeworkV103Check(AppCore core, Schedule schedule)
        {
            Say("");
            Say("—— v1.0.3 作业专项 ——");
            if (core.Doc == null || core.Doc.Courses().Count == 0)
            {
                Say("跳过（没有课表）");
                return;
            }
            if (core.Config.TrackedCourses.Count == 0) core.TrackCourse(core.Doc.Courses()[0], true);
            string course = core.Config.TrackedCourses[0];
            if (core.Config.TrackedCourses.Count < 2 && core.Doc.Courses().Count >= 2)
            {
                for (int i = 0; i < core.Doc.Courses().Count && core.Config.TrackedCourses.Count < 2; i++)
                {
                    if (core.Doc.Courses()[i] != course) core.TrackCourse(core.Doc.Courses()[i], true);
                }
            }
            string other = core.Config.TrackedCourses.Count > 1 ? core.Config.TrackedCourses[1] : null;
            int rulesBefore = core.Doc.Rules.Count;
            DateTime now = DateTime.Today.AddHours(23);

            HomeworkItem item = core.Homework.Ensure(course);
            HomeworkPage page = new HomeworkPage(core);
            HomeworkRow row = page.SimRow(course);
            Check("找到该课程的作业行", row != null, "rows=" + page.SimRowCount);
            if (row == null) return;

            // ---- T1 基本持久化 ----
            item.Text = "A";
            item.Method = "教学网";
            item.Done = false;
            core.SaveHomeworkNow();
            HomeworkItem r1 = Storage.LoadHomework().Find(course);
            Check("T1 正文与提交方式都能持久化", r1 != null && r1.Text == "A" && r1.Method == "教学网",
                r1 == null ? "没有该课程" : "Text=" + r1.Text + " Method=" + r1.Method);

            // ---- T2 修改正文不改提交方式（走 UI 真实处理器）----
            row.SimSetJobText("C");
            Check("T2 修改正文后提交方式仍为 B", item.Text == "C" && item.Method == "教学网",
                "Text=" + item.Text + " Method=" + item.Method);

            // ---- T3 「更新」按钮 ----
            item.Text = "A"; item.Method = "教学网"; item.Done = false;
            row.SimClickUpdate();
            Check("T3a 未完成→更新：正文清空 / 提交方式保留 / 仍未完成",
                item.Text == "" && item.Method == "教学网" && !item.Done,
                "Text='" + item.Text + "' Method=" + item.Method + " Done=" + item.Done);
            item.Text = "A"; item.Done = true;
            row.SimClickUpdate();
            Check("T3b 已完成→更新：正文清空 / 提交方式保留 / 状态变未完成",
                item.Text == "" && item.Method == "教学网" && !item.Done,
                "Text='" + item.Text + "' Method=" + item.Method + " Done=" + item.Done);

            // ---- T4 自动课后清空：正文清空但留下可恢复内容，提交方式不变 ----
            item.Text = "A"; item.Method = "Class网"; item.Done = false;
            item.LastClearedKey = ""; item.LastText = ""; item.LastClearedAt = "";
            core.Config.ClearMode = "PerSession";
            core.CheckAutoClear(now);
            Check("T4a 首次仅建立基准，不清空", item.Text == "A" && item.LastText == "",
                "Text=" + item.Text + " Last=" + item.LastText);
            DateTime later = NextCourseMoment(schedule, now, course);
            core.CheckAutoClear(later);
            Check("T4b 自动清空：正文清空 / LastText=A / 提交方式不变 / 记录时间",
                item.Text == "" && item.LastText == "A" && item.LastClearedAt.Length > 0 && item.Method == "Class网",
                "Text='" + item.Text + "' Last='" + item.LastText + "' Method=" + item.Method);
            row.SyncFromModel();
            Check("T4c 界面显示「上次作业」提示", row.SimLastPanelVisible && row.SimLastLabel.Contains("上次作业"),
                row.SimLastLabel);

            // ---- T5 恢复 ----
            row.SimClickRestore();
            Check("T5a 恢复：正文回到 A / 提交方式不变 / 提示消失",
                item.Text == "A" && item.Method == "Class网" && item.LastText.Length == 0 && !row.SimLastPanelVisible,
                "Text='" + item.Text + "' Method=" + item.Method);
            core.CheckAutoClear(later);
            Check("T5b 恢复后不会立即再次被自动清空", item.Text == "A", "Text=" + item.Text);

            // ---- T6 切换清空模式 ----
            item.Text = "B2"; item.Method = "邮件";
            string[] modes = new string[] { "PerDay", "PerSession", "PerDay", "Manual", "PerSession" };
            for (int i = 0; i < modes.Length; i++)
            {
                core.Config.ClearMode = modes[i];
                core.CheckAutoClear(now);
            }
            Check("T6 切换清空模式：不清空正文、不改提交方式",
                item.Text == "B2" && item.Method == "邮件", "Text=" + item.Text + " Method=" + item.Method);

            // ---- T7 取消跟踪不删除作业 ----
            core.TrackCourse(course, false);
            Check("T7a 取消跟踪后内存数据仍在", core.Homework.Find(course) != null &&
                core.Homework.Find(course).Text == "B2", "Text=" + (core.Homework.Find(course) == null ? "-" : core.Homework.Find(course).Text));
            core.SyncTrackedCourses();
            HomeworkItem afterSync = core.Homework.Find(course);
            Check("T7b 课程同步后数据仍在（不会被 RetainOnly 删掉）",
                afterSync != null && afterSync.Text == "B2" && afterSync.Method == "邮件");
            core.SaveHomeworkNow();
            HomeworkItem r7 = Storage.LoadHomework().Find(course);
            Check("T7c 重启（重新读取文件）后数据仍在",
                r7 != null && r7.Text == "B2" && r7.Method == "邮件",
                r7 == null ? "没有该课程" : "Text=" + r7.Text + " Method=" + r7.Method);
            core.TrackCourse(course, true);
            HomeworkItem back2 = core.Homework.Find(course);
            Check("T7d 重新跟踪后原数据仍在",
                back2 != null && back2.Text == "B2" && back2.Method == "邮件");

            // ---- T8 旧数据兼容（没有 Method / LastText 字段）----
            string good = File.ReadAllText(Paths.HomeworkFile, Encoding.UTF8);
            File.WriteAllText(Paths.HomeworkFile,
                "{ \"Items\": [ { \"Course\": \"旧数据课程\", \"Text\": \"旧作业\", \"Done\": false, " +
                "\"LastClearedKey\": \"2026-09-08\", \"Updated\": \"2026-09-08 10:00\" } ], \"Log\": [] }",
                Encoding.UTF8);
            HomeworkState legacy = Storage.LoadHomework();
            Check("T8a 旧数据读取不报错且内容完整",
                legacy.Items.Count == 1 && legacy.Items[0].Text == "旧作业" &&
                legacy.Items[0].Method == "" && legacy.Items[0].LastText == "",
                legacy.Items.Count == 0 ? "没有条目" : "Text=" + legacy.Items[0].Text + " Method='" + legacy.Items[0].Method + "'");
            Storage.SaveHomework(legacy);
            string upgraded = File.ReadAllText(Paths.HomeworkFile, Encoding.UTF8);
            Check("T8b 保存后升级为新格式（含 Method / LastText 字段）",
                upgraded.Contains("\"Method\"") && upgraded.Contains("\"LastText\"") && upgraded.Contains("旧作业"));

            // ---- T9 损坏文件保护（本次新增的数据安全网）----
            File.WriteAllText(Paths.HomeworkFile,
                "{ \"Items\": [ { \"Course\": \"坏数据\", \"Text\": \"重要内容\" ", Encoding.UTF8);
            HomeworkState broken = Storage.LoadHomework();
            string bad = broken.LoadBackupPath;
            bool backupOk = bad.Length > 0 && bad != Paths.HomeworkFile && File.Exists(bad) &&
                File.ReadAllText(bad, Encoding.UTF8).Contains("重要内容");
            Check("T9a 解析失败不抛异常并标记 LoadFailed", broken.LoadFailed);
            Check("T9b 已保留损坏文件备份（内容完整）", backupOk, bad);
            File.WriteAllText(Paths.HomeworkFile, good, Encoding.UTF8);
            try { if (backupOk) File.Delete(bad); } catch { }

            // ---- T10 原子写入 ----
            Storage.SaveHomework(core.Homework);
            int tmpLeft = Directory.GetFiles(Paths.DataDir, "*.tmp-*").Length;
            Check("T10 原子写入不留下临时文件", tmpLeft == 0, "left=" + tmpLeft);

            // ---- T11 「更新」与自动清空连续发生 ----
            item = core.Homework.Ensure(course);
            item.Text = "A2"; item.Method = "微信"; item.Done = true;
            item.LastText = ""; item.LastClearedAt = ""; item.LastClearedKey = "";
            core.Config.ClearMode = "PerSession";
            core.CheckAutoClear(now);
            Check("T11a 建立基准时正文保留", item.Text == "A2", "Text=" + item.Text);
            row.SimClickUpdate();
            Check("T11b 更新后：正文空 / 未完成 / 提交方式=微信",
                item.Text == "" && !item.Done && item.Method == "微信",
                "Text='" + item.Text + "' Done=" + item.Done + " Method=" + item.Method);
            core.CheckAutoClear(now);
            Check("T11c 同一节课不会因为更新而触发自动清空",
                item.Text == "" && item.LastText == "" && item.Method == "微信");
            DateTime later2 = NextCourseMoment(schedule, now, course);
            core.CheckAutoClear(later2);
            Check("T11d 下一节课且正文为空：不产生 LastText、提交方式不变",
                item.LastText == "" && item.Method == "微信", "Last='" + item.LastText + "' Method=" + item.Method);
            item.Text = "B3";
            core.CheckAutoClear(NextCourseMoment(schedule, later2, course));
            Check("T11e 之后写的作业被正确保存为可恢复内容",
                item.Text == "" && item.LastText == "B3" && item.Method == "微信",
                "Text='" + item.Text + "' Last='" + item.LastText + "'");

            // ---- T12 更新只影响当前课程 ----
            string modeBeforeUpdate = core.Config.ClearMode;
            string otherText = null;
            string otherMethod = null;
            if (other != null)
            {
                HomeworkItem oi = core.Homework.Ensure(other);
                oi.Text = "别的作业"; oi.Method = "课堂提交";
                otherText = oi.Text; otherMethod = oi.Method;
            }
            item.Text = "C2"; item.Method = "教师指定平台"; item.Done = false;
            row.SimClickUpdate();
            bool otherOk = other == null || (core.Homework.Find(other).Text == otherText &&
                core.Homework.Find(other).Method == otherMethod);
            Check("T12a 更新不影响其他课程的作业与提交方式", otherOk,
                other == null ? "（只有一门课）" : "other=" + core.Homework.Find(other).Text + "/" + core.Homework.Find(other).Method);
            Check("T12b 更新不改变清空模式", core.Config.ClearMode == modeBeforeUpdate, core.Config.ClearMode);
            Check("T12c 更新不改变课程/课表信息",
                core.Doc.Rules.Count == rulesBefore && core.Config.TrackedCourses.Contains(course),
                "rules=" + core.Doc.Rules.Count);
            Check("T12d 更新后提交方式仍保留", item.Method == "教师指定平台", item.Method);
            core.SaveHomeworkNow();
            HomeworkItem r12 = Storage.LoadHomework().Find(course);
            Check("T13 更新后重启（重新读取）提交方式仍存在",
                r12 != null && r12.Method == "教师指定平台", r12 == null ? "没有该课程" : r12.Method);

            core.SaveConfig();
        }

        /// <summary>该课程在 after 之后的第一次课「结束时间 + 5 分钟」。</summary>
        private static DateTime NextCourseMoment(Schedule schedule, DateTime after, string course)
        {
            DateTime probe = after;
            for (int i = 0; i < 80; i++)
            {
                Session s = schedule.NextSession(probe, 21);
                if (s == null) return after.AddDays(7);
                if (s.Course == course) return s.End.AddMinutes(5);
                probe = s.Start.AddMinutes(1);
            }
            return after.AddDays(7);
        }

        /// <summary>
        /// Drives the floating widget through the interaction scenarios that used to break
        /// (expand / close / hover again / click while expanded / drag / lost mouse-up).
        /// </summary>
        private static void WidgetInteractionCheck(AppCore core)
        {
            Say("");
            Say("—— 悬浮球交互状态机 ——");
            int savedAnim = core.Config.AnimationMs;
            bool savedPinned = core.Config.BallPinned;
            int savedLeft = core.Config.BallLeft;
            int savedTop = core.Config.BallTop;
            core.Config.AnimationMs = 0;      // expand/collapse complete synchronously in the test
            core.Config.BallPinned = false;
            Rect work = SystemParameters.WorkArea;
            try
            {
                Widget w = new Widget(core, null);
                w.SimEnable();
                PlaceBall(core, w, work.Left + 60, work.Top + 60);

                // 1. 悬停展开
                w.SimSetPointerInside(true);
                w.SimHover(true);
                Check("① 悬停展开", w.SimExpanded && !w.SimDragging,
                    "expanded=" + w.SimExpanded + " dragging=" + w.SimDragging);

                // 2. 展开后点击关闭按钮
                w.SimCollapseClick();
                Check("② 点击关闭按钮后收起", !w.SimExpanded, "expanded=" + w.SimExpanded);

                // 3. 关闭后指针停留在原处，不应被同一个 hover 再次展开
                Check("③ 关闭时设置了悬停抑制", w.SimSuppressHoverExpand,
                    "suppress=" + w.SimSuppressHoverExpand);
                w.SimHover(true);
                w.SimHover(false);
                Check("③ 指针未离开时不会重新展开", !w.SimExpanded, "expanded=" + w.SimExpanded);

                // 4. 指针离开后再悬停，应能正常展开
                w.SimSetPointerInside(false);   // 真正离开控件范围
                w.SimLeaveWidget();             // 离开事件
                w.SimSetPointerInside(true);
                w.SimHover(false);
                Check("④ 离开后再次悬停可展开", w.SimExpanded, "expanded=" + w.SimExpanded);

                // 5. 展开状态下点击标题栏（无位移）：不得留下拖拽/捕获状态
                w.SimSetButtonDown(true);
                w.SimPointerDownOnHeader();
                w.SimPointerUp();
                w.SimSetButtonDown(false);
                Check("⑤ 展开态点击后无残留拖拽状态",
                    !w.SimDragging && !w.SimMouseCaptured && w.SimExpanded,
                    "dragging=" + w.SimDragging + " captured=" + w.SimMouseCaptured);

                // 6. 鼠标快速移出 → 定时器触发自动收起
                w.SimSetPointerInside(false);
                w.SimCollapseTimer();
                Check("⑥ 指针移出后自动收起", !w.SimExpanded, "expanded=" + w.SimExpanded);

                // 根因演示：同一串光标移动，旧公式（基准 = 按下时的 Left）会“走一步停一步”
                Say("    旧公式 8 步 位置：" + DragTrack(1000, false));
                Say("    新公式 8 步 位置：" + DragTrack(1000, true));

                // 7/8/9. 拖拽：光标在屏幕坐标里每步走 (5,2)，窗口相对坐标按窗口当前位置换算
                PlaceBall(core, w, work.Left + 60, work.Top + 60);
                double grabX = 51;
                double grabY = 32;
                double cursorX = w.SimLeft + grabX;
                double cursorY = w.SimTop + grabY;
                double x0 = w.SimLeft;
                double y0 = w.SimTop;
                w.SimSetButtonDown(true);
                w.SimPointerDownAt(grabX, grabY);
                bool monotonic = true;
                double prev = x0;
                for (int i = 1; i <= 20; i++)
                {
                    cursorX += 5;
                    cursorY += 2;
                    w.SimPointerMove(cursorX - w.SimLeft, cursorY - w.SimTop);
                    if (w.SimLeft < prev - 0.001) monotonic = false;
                    prev = w.SimLeft;
                }
                w.SimPointerUp();
                w.SimSetButtonDown(false);
                double dx = w.SimLeft - x0;
                double dy = w.SimTop - y0;
                Check("⑦ 多步拖拽位移精确 (100, 40)",
                    Math.Abs(dx - 100) < 0.6 && Math.Abs(dy - 40) < 0.6,
                    "dx=" + dx.ToString("0.0", CultureInfo.InvariantCulture) +
                    " dy=" + dy.ToString("0.0", CultureInfo.InvariantCulture));
                Check("⑧ 拖拽过程单调无抖动（无往复）", monotonic);
                Check("⑨ 拖拽结束后状态干净",
                    !w.SimDragging && !w.SimMouseCaptured && w.SimDragMoved == false,
                    "dragging=" + w.SimDragging + " captured=" + w.SimMouseCaptured);

                // 10. 长距离拖拽
                PlaceBall(core, w, work.Left + 60, work.Top + 60);
                cursorX = w.SimLeft + grabX;
                cursorY = w.SimTop + grabY;
                w.SimSetButtonDown(true);
                w.SimPointerDownAt(grabX, grabY);
                x0 = w.SimLeft;
                y0 = w.SimTop;
                for (int i = 1; i <= 40; i++)
                {
                    cursorX += 10;
                    cursorY += 5;
                    w.SimPointerMove(cursorX - w.SimLeft, cursorY - w.SimTop);
                }
                w.SimPointerUp();
                w.SimSetButtonDown(false);
                Check("⑩ 长距离拖拽位移精确 (400, 200)",
                    Math.Abs((w.SimLeft - x0) - 400) < 0.6 && Math.Abs((w.SimTop - y0) - 200) < 0.6,
                    "dx=" + (w.SimLeft - x0).ToString("0.0", CultureInfo.InvariantCulture) +
                    " dy=" + (w.SimTop - y0).ToString("0.0", CultureInfo.InvariantCulture));

                // 11. 拖拽中丢失 mouse-up（按钮已松开但 up 事件没到）→ 下一次移动必须自愈
                PlaceBall(core, w, work.Left + 60, work.Top + 60);
                cursorX = w.SimLeft + grabX;
                cursorY = w.SimTop + grabY;
                w.SimSetButtonDown(true);
                w.SimPointerDownAt(grabX, grabY);
                cursorX += 60;
                cursorY += 30;
                w.SimPointerMove(cursorX - w.SimLeft, cursorY - w.SimTop);
                w.SimSetButtonDown(false);
                cursorX += 20;
                cursorY += 10;
                w.SimPointerMove(cursorX - w.SimLeft, cursorY - w.SimTop);
                Check("⑪ 丢失 mouse-up 后自动解除拖拽状态",
                    !w.SimDragging && !w.SimMouseCaptured,
                    "dragging=" + w.SimDragging + " captured=" + w.SimMouseCaptured);
                w.SimSetPointerInside(true);
                w.SimHover(true);
                w.SimSetPointerInside(false);
                w.SimCollapseTimer();
                Check("⑪ 自愈后自动收起仍然生效", !w.SimExpanded, "expanded=" + w.SimExpanded);

                // 12. 拖拽之后继续点击悬浮球，应能正常展开
                w.SimSetButtonDown(true);
                w.SimPointerDownOnBall();
                w.SimPointerUp();
                w.SimSetButtonDown(false);
                Check("⑫ 拖拽后点击悬浮球可展开", w.SimExpanded && !w.SimDragging,
                    "expanded=" + w.SimExpanded + " dragging=" + w.SimDragging);

                // 13. 连续多轮 展开 → 收起 → 拖拽，状态保持一致
                bool loopOk = true;
                for (int i = 0; i < 5 && loopOk; i++)
                {
                    w.SimSetPointerInside(true);
                    w.SimHover(true);
                    loopOk = loopOk && w.SimExpanded;
                    w.SimSetPointerInside(false);
                    w.SimCollapseTimer();
                    loopOk = loopOk && !w.SimExpanded;
                    cursorX = w.SimLeft + grabX;
                    cursorY = w.SimTop + grabY;
                    w.SimSetButtonDown(true);
                    w.SimPointerDownAt(grabX, grabY);
                    cursorX += 10;
                    cursorY += 5;
                    w.SimPointerMove(cursorX - w.SimLeft, cursorY - w.SimTop);
                    cursorX += 10;
                    cursorY += 5;
                    w.SimPointerMove(cursorX - w.SimLeft, cursorY - w.SimTop);
                    w.SimPointerUp();
                    w.SimSetButtonDown(false);
                    loopOk = loopOk && !w.SimDragging && !w.SimMouseCaptured;
                }
                Check("⑬ 连续 5 轮 展开/收起/拖拽 状态一致", loopOk,
                    "dragging=" + w.SimDragging + " captured=" + w.SimMouseCaptured);

                // ⑭ 回归：收起动画期间窗口几何变化会放出"假的 MouseLeave"，
                //     上一版就是被它解除了抑制，导致 ✕ 之后立刻又展开。现在必须只在指针真的离开后才解除。
                core.Config.AnimationMs = 200;      // 让收起处于"动画进行中"状态
                PlaceBall(core, w, work.Left + 60, work.Top + 60);
                w.SimSetPointerInside(true);
                w.SimHover(true);
                w.SimRunAnimationToEnd();
                Check("⑭ 准备：已展开", w.SimExpanded && !w.SimAnimating,
                    "expanded=" + w.SimExpanded + " animating=" + w.SimAnimating);
                w.SimCollapseClick();
                Check("⑭ 点击 ✕ 后进入收起动画并进入收兵状态",
                    w.SimAnimating && w.SimSuppressHoverExpand,
                    "animating=" + w.SimAnimating + " disarmed=" + w.SimSuppressHoverExpand);
                w.SimSetPointerInside(false);
                w.SimLeaveWidget();                              // 动画中的假离开
                Check("⑭ 动画中的假离开不解除收兵", w.SimSuppressHoverExpand,
                    "disarmed=" + w.SimSuppressHoverExpand);
                w.SimSetPointerInside(true);                     // 指针其实一直停在胶囊上
                w.SimRunAnimationToEnd();
                Check("⑭ 收起完成后仍保持收兵且未展开",
                    !w.SimExpanded && w.SimSuppressHoverExpand,
                    "expanded=" + w.SimExpanded + " disarmed=" + w.SimSuppressHoverExpand);
                w.SimHover(true);
                w.SimHover(false);
                Check("⑭ 指针未真正离开时不会被重新展开", !w.SimExpanded, "expanded=" + w.SimExpanded);

                // ⑮ 收兵状态下点击胶囊（等同于"连点两次 ✕"的第二下）也不应立刻展开
                w.SimSetButtonDown(true);
                w.SimPointerDownOnBall();
                w.SimPointerUp();
                w.SimSetButtonDown(false);
                Check("⑮ 收兵状态下点击胶囊不会立刻展开", !w.SimExpanded, "expanded=" + w.SimExpanded);

                // 指针真正离开 → 解除收兵 → 下次悬停正常展开
                w.SimSetPointerInside(false);
                w.SimDisarmTimerTick();
                Check("⑭ 指针真正离开后解除收兵", !w.SimSuppressHoverExpand,
                    "disarmed=" + w.SimSuppressHoverExpand);
                w.SimSetPointerInside(true);
                w.SimHover(true);
                w.SimRunAnimationToEnd();
                Check("⑭ 离开后再次悬停可展开", w.SimExpanded, "expanded=" + w.SimExpanded);
                core.Config.AnimationMs = 0;
                w.SimSetPointerInside(false);
                w.SimCollapseTimer();

                // ⑯ 指针本来就在窗口外时收起 → 立刻解除收兵，不影响后续悬停
                Check("⑯ 指针在外时收起后立即解除收兵",
                    !w.SimExpanded && !w.SimSuppressHoverExpand,
                    "expanded=" + w.SimExpanded + " disarmed=" + w.SimSuppressHoverExpand);
                w.SimSetPointerInside(true);
                w.SimHover(false);
                w.SimRunAnimationToEnd();
                Check("⑯ 随后悬停可正常展开", w.SimExpanded, "expanded=" + w.SimExpanded);

                w.AllowClose();
                w.Close();
            }
            catch (Exception ex)
            {
                Check("悬浮球交互状态机", false, ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace);
            }
            finally
            {
                core.Config.AnimationMs = savedAnim;
                core.Config.BallPinned = savedPinned;
                core.Config.BallLeft = savedLeft;
                core.Config.BallTop = savedTop;
            }
        }

        private static void PlaceBall(AppCore core, Widget w, double left, double top)
        {
            core.Config.BallLeft = (int)Math.Round(left);
            core.Config.BallTop = (int)Math.Round(top);
            w.ApplyConfig();
        }

        /// <summary>
        /// Pure numeric demo of the drag formula. The cursor moves 5px per step on the screen while
        /// the reported pointer position is window relative, exactly like the real events.
        /// Old formula (baseline = window position at mouse-down) advances only every other step;
        /// the fixed formula (baseline = current window position) follows exactly.
        /// </summary>
        private static string DragTrack(double startLeft, bool useCurrentBaseline)
        {
            double origin = 51;                  // pointer offset inside the window at mouse-down
            double left = startLeft;
            double cursor = startLeft + origin;  // screen coordinates
            StringBuilder sb = new StringBuilder();
            for (int i = 1; i <= 8; i++)
            {
                cursor += 5;
                double rel = cursor - left;
                double dx = rel - origin;
                left = useCurrentBaseline ? left + dx : startLeft + dx;
                sb.Append(left.ToString("0", CultureInfo.InvariantCulture)).Append(' ');
            }
            return sb.ToString().Trim();
        }

        /// <summary>Ink centroid (alpha &gt; 100, bright pixels) inside a rectangle of the bitmap.</summary>
        private static bool Centroid(RenderTargetBitmap bmp, int x0, int x1, int y0, int y1,
            out double cx, out double cy)
        {
            int stride = bmp.PixelWidth * 4;
            byte[] pixels = new byte[stride * bmp.PixelHeight];
            bmp.CopyPixels(pixels, stride, 0);
            double sumX = 0;
            double sumY = 0;
            int count = 0;
            for (int y = Math.Max(0, y0); y < Math.Min(bmp.PixelHeight, y1); y++)
            {
                for (int x = Math.Max(0, x0); x < Math.Min(bmp.PixelWidth, x1); x++)
                {
                    int i = y * stride + x * 4;
                    int b = pixels[i];
                    int g = pixels[i + 1];
                    int r = pixels[i + 2];
                    int a = pixels[i + 3];
                    if (a < 100) continue;
                    if ((r + g + b) / 3 < 85) continue;
                    sumX += x;
                    sumY += y;
                    count++;
                }
            }
            cx = count == 0 ? -1 : sumX / count;
            cy = count == 0 ? -1 : sumY / count;
            return count > 8;
        }
    }
}
