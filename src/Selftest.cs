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
            }
            else
            {
                Say("跳过作业清空测试（没有课程）");
            }

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
