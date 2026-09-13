using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;

namespace CampusClock
{
    /// <summary>One VEVENT from the .ics file, reduced to a weekly rule.</summary>
    public class CourseRule
    {
        public string Uid = "";
        public string Summary = "";
        public string Location = "";
        public string PeriodLabel = "";          // e.g. "第1 - 2节"
        public DayOfWeek Day = DayOfWeek.Monday;
        public int StartMin;                     // minutes from midnight, local time
        public int EndMin;
        public DateTime FirstDate = DateTime.MinValue;
        public DateTime UntilLocal = DateTime.MaxValue;   // inclusive
        public int IntervalWeeks = 1;
        public List<DateTime> ExDates = new List<DateTime>();
        public int ColorIndex;

        public string TimeText
        {
            get { return Fmt.Minutes(StartMin) + "-" + Fmt.Minutes(EndMin); }
        }

        public bool OccursOn(DateTime date)
        {
            DateTime d = date.Date;
            if (d < FirstDate.Date) return false;
            if (d > UntilLocal.Date) return false;
            if (d.DayOfWeek != Day) return false;
            int weeks = (int)Math.Round((d - FirstDate.Date).TotalDays / 7.0);
            if (weeks < 0) return false;
            if (IntervalWeeks > 1 && (weeks % IntervalWeeks) != 0) return false;
            DateTime start = d.AddMinutes(StartMin);
            if (start > UntilLocal) return false;
            foreach (DateTime ex in ExDates)
            {
                if (ex.Date == d) return false;
            }
            return true;
        }

        /// <summary>The last date this rule really occurs on.</summary>
        public DateTime LastOccurrence()
        {
            DateTime last = UntilLocal == DateTime.MaxValue ? FirstDate.AddYears(1) : UntilLocal.Date;
            while (last.DayOfWeek != Day) last = last.AddDays(-1);
            if (IntervalWeeks > 1)
            {
                int weeks = (int)Math.Round((last - FirstDate.Date).TotalDays / 7.0);
                if (weeks > 0) weeks -= weeks % IntervalWeeks;
                last = FirstDate.Date.AddDays(weeks * 7);
            }
            if (last.AddMinutes(StartMin) > UntilLocal) last = last.AddDays(-7 * IntervalWeeks);
            return last;
        }
    }

    /// <summary>A concrete class meeting on a concrete date.</summary>
    public class Session
    {
        public CourseRule Rule;
        public DateTime Start;
        public DateTime End;
        public int Lane;          // horizontal slot when two classes overlap in time
        public int LaneCount = 1;

        public string Course { get { return Rule.Summary; } }
        public string Location { get { return Rule.Location; } }
        public string PeriodLabel { get { return Rule.PeriodLabel; } }
    }

    /// <summary>A row of the timetable (a period, e.g. "第1-2节").</summary>
    public class PeriodRow
    {
        public string Label = "";
        public string TimeText = "";
        public int StartMin;
        public int EndMin;
    }

    public class Fmt
    {
        public static string Minutes(int m)
        {
            if (m < 0) m = 0;
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", m / 60, m % 60);
        }

        public static string Date(DateTime d)
        {
            return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        public static string DateTimeText(DateTime d)
        {
            return d.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        public static string ShortDate(DateTime d)
        {
            return d.ToString("MM-dd", CultureInfo.InvariantCulture);
        }

        public static string WeekDayCN(DayOfWeek d)
        {
            string[] names = new string[] { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
            int i = (int)d;
            if (i < 0 || i > 6) return "";
            return names[i];
        }
    }

    /// <summary>All persisted user settings (data/config.json).</summary>
    public class AppConfig
    {
        // 外观 / 显示设置
        public int FontSize = 14;
        public int CellWidth = 116;
        public int CellHeight = 80;
        public int PxPerHour = 64;
        public bool ShowWeekend = false;
        public bool ShowNowMarker = true;
        public double CardOpacity = 0.30;
        public string Accent = "#6D8DFF";

        // 悬浮球
        public bool BallEnabled = true;
        public int BallSize = 64;
        public int ExpandAreaPercent = 20;      // expanded square covers this % of the desktop area
        public int AnimationMs = 200;
        public bool BallTopmost = true;
        public bool BallPinned = false;
        public int BallLeft = int.MinValue;
        public int BallTop = int.MinValue;
        public string BallPane = "timetable";    // timetable | homework

        // 作业
        public string ClearMode = "PerSession";  // PerSession | PerDay | Manual
        public List<string> TrackedCourses = new List<string>();

        // 其它
        public bool TrayEnabled = true;
        public bool StartMinimized = false;

        public void Normalize()
        {
            FontSize = Clamp(FontSize, 9, 28);
            CellWidth = Clamp(CellWidth, 60, 240);
            CellHeight = Clamp(CellHeight, 36, 200);
            PxPerHour = Clamp(PxPerHour, 28, 220);
            CardOpacity = Clamp(CardOpacity, 0.08, 0.60);
            BallSize = Clamp(BallSize, 40, 120);
            ExpandAreaPercent = Clamp(ExpandAreaPercent, 8, 60);
            AnimationMs = Clamp(AnimationMs, 0, 600);
            if (Accent == null || Accent.Length != 7 || Accent[0] != '#') Accent = "#6D8DFF";
            if (BallPane != "homework") BallPane = "timetable";
            if (ClearMode != "PerSession" && ClearMode != "PerDay" && ClearMode != "Manual") ClearMode = "PerSession";
            if (TrackedCourses == null) TrackedCourses = new List<string>();
        }

        public static int Clamp(int v, int lo, int hi)
        {
            if (v < lo) return lo;
            if (v > hi) return hi;
            return v;
        }

        public static double Clamp(double v, double lo, double hi)
        {
            if (v < lo) return lo;
            if (v > hi) return hi;
            return v;
        }
    }

    /// <summary>One row of the homework board.</summary>
    public class HomeworkItem
    {
        public string Course = "";
        public string Text = "";
        public string Method = "";              // 提交方式：自由文本，独立于作业内容生命周期
        public bool Done = false;
        public string LastClearedKey = "";      // e.g. "2026-10-09 15:10"
        public string LastText = "";            // 上一次被【自动课后清空】保留下来、可恢复的内容
        public string LastClearedAt = "";       // 那次自动清空发生的时间（用于界面提示）
        public string Updated = "";
    }

    /// <summary>Persisted homework state (data/homework.json).</summary>
    public class HomeworkState
    {
        public List<HomeworkItem> Items = new List<HomeworkItem>();
        public List<string> Log = new List<string>();
        public bool LoadFailed;                 // 文件存在但读取/解析失败（此时不可静默覆盖）
        public string LoadBackupPath = "";      // 失败时保留的备份文件路径（如果有）

        public HomeworkItem Find(string course)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i].Course == course) return Items[i];
            }
            return null;
        }

        public HomeworkItem Ensure(string course)
        {
            HomeworkItem it = Find(course);
            if (it == null)
            {
                it = new HomeworkItem();
                it.Course = course;
                Items.Add(it);
            }
            return it;
        }

        // NOTE: there is deliberately no "drop items that are not tracked" helper any more.
        // Whether a course is tracked is a UI/config concern; homework data must survive untracking.
    }

    public static class Paths
    {
        public static string AppDir;
        public static string DataDir;
        public static string ConfigFile;
        public static string HomeworkFile;
        public static string IcsFile;
        public static string LogFile;
        public static bool Portable;

        public static void Init()
        {
            AppDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string candidate = Path.Combine(AppDir, "data");
            Portable = true;
            try
            {
                if (!Directory.Exists(candidate)) Directory.CreateDirectory(candidate);
                string probe = Path.Combine(candidate, ".write-test");
                File.WriteAllText(probe, "ok", Encoding.UTF8);
                File.Delete(probe);
            }
            catch
            {
                Portable = false;
                string fallback = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CampusClock");
                candidate = fallback;
                if (!Directory.Exists(candidate)) Directory.CreateDirectory(candidate);
            }
            DataDir = candidate;
            ConfigFile = Path.Combine(DataDir, "config.json");
            HomeworkFile = Path.Combine(DataDir, "homework.json");
            IcsFile = Path.Combine(DataDir, "timetable.ics");
            LogFile = Path.Combine(DataDir, "app.log");
        }

        /// <summary>Redirect all paths to a scratch folder (used by --selftest so real data is never touched).</summary>
        public static void UseTempForTest()
        {
            string dir = Path.Combine(Path.GetTempPath(), "CampusClock-selftest");
            try
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            }
            catch
            {
            }
            DataDir = dir;
            ConfigFile = Path.Combine(dir, "config.json");
            HomeworkFile = Path.Combine(dir, "homework.json");
            IcsFile = Path.Combine(dir, "timetable.ics");
            LogFile = Path.Combine(dir, "app.log");
        }
    }

    public static class Log
    {
        private static readonly object Gate = new object();

        public static void Info(string message)
        {
            Write("INFO ", message);
        }

        public static void Warn(string message)
        {
            Write("WARN ", message);
        }

        public static void Error(string message)
        {
            Write("ERROR", message);
        }

        private static void Write(string level, string message)
        {
            try
            {
                lock (Gate)
                {
                    if (Paths.LogFile == null) return;
                    string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                        + " [" + level + "] " + message + Environment.NewLine;
                    File.AppendAllText(Paths.LogFile, line, Encoding.UTF8);
                    FileInfo fi = new FileInfo(Paths.LogFile);
                    if (fi.Exists && fi.Length > 512 * 1024)
                    {
                        string[] lines = File.ReadAllLines(Paths.LogFile, Encoding.UTF8);
                        int keep = lines.Length / 2;
                        StringBuilder sb = new StringBuilder();
                        for (int i = lines.Length - keep; i < lines.Length; i++)
                        {
                            if (i >= 0) sb.AppendLine(lines[i]);
                        }
                        File.WriteAllText(Paths.LogFile, sb.ToString(), Encoding.UTF8);
                    }
                }
            }
            catch
            {
                // logging must never break the app
            }
        }
    }

    public static class Json
    {
        public static object Parse(string text)
        {
            JavaScriptSerializer ser = new JavaScriptSerializer();
            ser.MaxJsonLength = 16 * 1024 * 1024;
            return ser.DeserializeObject(text);
        }

        public static Dictionary<string, object> AsDict(object o)
        {
            Dictionary<string, object> d = o as Dictionary<string, object>;
            return d;
        }

        public static string Pretty(object value)
        {
            StringBuilder sb = new StringBuilder();
            Write(sb, value, 0);
            sb.AppendLine();
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, object value, int indent)
        {
            string pad = new string(' ', indent * 2);
            string pad2 = new string(' ', (indent + 1) * 2);
            if (value == null)
            {
                sb.Append("null");
                return;
            }
            if (value is string)
            {
                sb.Append(Escape((string)value));
                return;
            }
            if (value is bool)
            {
                sb.Append(((bool)value) ? "true" : "false");
                return;
            }
            if (value is int || value is long)
            {
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }
            if (value is double || value is float || value is decimal)
            {
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }
            Dictionary<string, object> dict = value as Dictionary<string, object>;
            if (dict != null)
            {
                if (dict.Count == 0)
                {
                    sb.Append("{}");
                    return;
                }
                sb.AppendLine("{");
                int i = 0;
                foreach (KeyValuePair<string, object> kv in dict)
                {
                    sb.Append(pad2).Append(Escape(kv.Key)).Append(": ");
                    Write(sb, kv.Value, indent + 1);
                    i++;
                    if (i < dict.Count) sb.Append(',');
                    sb.AppendLine();
                }
                sb.Append(pad).Append("}");
                return;
            }
            IEnumerable list = value as IEnumerable;
            if (list != null)
            {
                List<object> items = new List<object>();
                foreach (object o in list) items.Add(o);
                if (items.Count == 0)
                {
                    sb.Append("[]");
                    return;
                }
                sb.AppendLine("[");
                for (int i = 0; i < items.Count; i++)
                {
                    sb.Append(pad2);
                    Write(sb, items[i], indent + 1);
                    if (i < items.Count - 1) sb.Append(',');
                    sb.AppendLine();
                }
                sb.Append(pad).Append("]");
                return;
            }
            sb.Append(Escape(Convert.ToString(value, CultureInfo.InvariantCulture)));
        }

        private static string Escape(string s)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        public static string GetString(Dictionary<string, object> d, string key, string def)
        {
            if (d == null || !d.ContainsKey(key)) return def;
            object v = d[key];
            if (v == null) return def;
            string s = v as string;
            if (s != null) return s;
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public static int GetInt(Dictionary<string, object> d, string key, int def)
        {
            if (d == null || !d.ContainsKey(key)) return def;
            object v = d[key];
            if (v == null) return def;
            try
            {
                if (v is int) return (int)v;
                if (v is long) return (int)(long)v;
                if (v is double) return (int)Math.Round((double)v);
                if (v is bool) return ((bool)v) ? 1 : 0;
                return int.Parse(Convert.ToString(v, CultureInfo.InvariantCulture),
                    NumberStyles.Any, CultureInfo.InvariantCulture);
            }
            catch
            {
                return def;
            }
        }

        public static double GetDouble(Dictionary<string, object> d, string key, double def)
        {
            if (d == null || !d.ContainsKey(key)) return def;
            object v = d[key];
            if (v == null) return def;
            try
            {
                if (v is double) return (double)v;
                if (v is int) return (int)v;
                if (v is long) return (long)v;
                return double.Parse(Convert.ToString(v, CultureInfo.InvariantCulture),
                    NumberStyles.Any, CultureInfo.InvariantCulture);
            }
            catch
            {
                return def;
            }
        }

        public static bool GetBool(Dictionary<string, object> d, string key, bool def)
        {
            if (d == null || !d.ContainsKey(key)) return def;
            object v = d[key];
            if (v == null) return def;
            if (v is bool) return (bool)v;
            try
            {
                return GetInt(d, key, def ? 1 : 0) != 0;
            }
            catch
            {
                return def;
            }
        }

        public static List<string> GetStrings(Dictionary<string, object> d, string key)
        {
            List<string> result = new List<string>();
            if (d == null || !d.ContainsKey(key)) return result;
            IEnumerable list = d[key] as IEnumerable;
            if (list == null) return result;
            foreach (object o in list)
            {
                if (o == null) continue;
                string s = o as string;
                result.Add(s != null ? s : Convert.ToString(o, CultureInfo.InvariantCulture));
            }
            return result;
        }
    }

    public static class Storage
    {
        public static AppConfig LoadConfig()
        {
            AppConfig cfg = new AppConfig();
            try
            {
                if (File.Exists(Paths.ConfigFile))
                {
                    Dictionary<string, object> d = Json.AsDict(Json.Parse(File.ReadAllText(Paths.ConfigFile, Encoding.UTF8)));
                    cfg.FontSize = Json.GetInt(d, "FontSize", cfg.FontSize);
                    cfg.CellWidth = Json.GetInt(d, "CellWidth", cfg.CellWidth);
                    cfg.CellHeight = Json.GetInt(d, "CellHeight", cfg.CellHeight);
                    cfg.PxPerHour = Json.GetInt(d, "PxPerHour", 0);
                    if (cfg.PxPerHour <= 0)
                    {
                        // migrate the old "row height" value into the new time axis scale
                        cfg.PxPerHour = (int)Math.Round(cfg.CellHeight * 0.85);
                    }
                    cfg.ShowWeekend = Json.GetBool(d, "ShowWeekend", cfg.ShowWeekend);
                    cfg.ShowNowMarker = Json.GetBool(d, "ShowNowMarker", cfg.ShowNowMarker);
                    cfg.CardOpacity = Json.GetDouble(d, "CardOpacity", cfg.CardOpacity);
                    cfg.Accent = Json.GetString(d, "Accent", cfg.Accent);
                    cfg.BallEnabled = Json.GetBool(d, "BallEnabled", cfg.BallEnabled);
                    cfg.BallSize = Json.GetInt(d, "BallSize", cfg.BallSize);
                    cfg.ExpandAreaPercent = Json.GetInt(d, "ExpandAreaPercent", cfg.ExpandAreaPercent);
                    cfg.AnimationMs = Json.GetInt(d, "AnimationMs", cfg.AnimationMs);
                    cfg.BallTopmost = Json.GetBool(d, "BallTopmost", cfg.BallTopmost);
                    cfg.BallPinned = Json.GetBool(d, "BallPinned", cfg.BallPinned);
                    cfg.BallLeft = Json.GetInt(d, "BallLeft", cfg.BallLeft);
                    cfg.BallTop = Json.GetInt(d, "BallTop", cfg.BallTop);
                    cfg.BallPane = Json.GetString(d, "BallPane", cfg.BallPane);
                    cfg.ClearMode = Json.GetString(d, "ClearMode", cfg.ClearMode);
                    cfg.TrackedCourses = Json.GetStrings(d, "TrackedCourses");
                    cfg.TrayEnabled = Json.GetBool(d, "TrayEnabled", cfg.TrayEnabled);
                    cfg.StartMinimized = Json.GetBool(d, "StartMinimized", cfg.StartMinimized);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("读取 config.json 失败，使用默认设置：" + ex.Message);
            }
            cfg.Normalize();
            return cfg;
        }

        public static void SaveConfig(AppConfig cfg)
        {
            try
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["FontSize"] = cfg.FontSize;
                d["CellWidth"] = cfg.CellWidth;
                d["CellHeight"] = cfg.CellHeight;
                d["PxPerHour"] = cfg.PxPerHour;
                d["ShowWeekend"] = cfg.ShowWeekend;
                d["ShowNowMarker"] = cfg.ShowNowMarker;
                d["CardOpacity"] = cfg.CardOpacity;
                d["Accent"] = cfg.Accent;
                d["BallEnabled"] = cfg.BallEnabled;
                d["BallSize"] = cfg.BallSize;
                d["ExpandAreaPercent"] = cfg.ExpandAreaPercent;
                d["AnimationMs"] = cfg.AnimationMs;
                d["BallTopmost"] = cfg.BallTopmost;
                d["BallPinned"] = cfg.BallPinned;
                d["BallLeft"] = cfg.BallLeft;
                d["BallTop"] = cfg.BallTop;
                d["BallPane"] = cfg.BallPane;
                d["ClearMode"] = cfg.ClearMode;
                d["TrackedCourses"] = cfg.TrackedCourses;
                d["TrayEnabled"] = cfg.TrayEnabled;
                d["StartMinimized"] = cfg.StartMinimized;
                WriteAtomic(Paths.ConfigFile, Json.Pretty(d));
            }
            catch (Exception ex)
            {
                Log.Error("保存 config.json 失败：" + ex.Message);
            }
        }

        public static HomeworkState LoadHomework()
        {
            HomeworkState st = new HomeworkState();
            if (!File.Exists(Paths.HomeworkFile)) return st;   // fresh install: nothing to protect
            string raw;
            try
            {
                raw = File.ReadAllText(Paths.HomeworkFile, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                // the file is there but unreadable (locked / permission): keep it untouched
                st.LoadFailed = true;
                st.LoadBackupPath = Paths.HomeworkFile;
                Log.Error("读取 homework.json 失败，已保持原文件不变，本次不覆盖：" + ex.Message);
                return st;
            }
            try
            {
                Dictionary<string, object> d = Json.AsDict(Json.Parse(raw));
                if (d != null && d.ContainsKey("Items"))
                {
                    IEnumerable list = d["Items"] as IEnumerable;
                    if (list != null)
                    {
                        foreach (object o in list)
                        {
                            Dictionary<string, object> row = Json.AsDict(o);
                            if (row == null) continue;
                            HomeworkItem it = new HomeworkItem();
                            it.Course = Json.GetString(row, "Course", "");
                            it.Text = Json.GetString(row, "Text", "");
                            it.Method = Json.GetString(row, "Method", "");          // 旧数据没有该字段 -> 空字符串
                            it.Done = Json.GetBool(row, "Done", false);
                            it.LastClearedKey = Json.GetString(row, "LastClearedKey", "");
                            it.LastText = Json.GetString(row, "LastText", "");      // 旧数据没有该字段 -> 空字符串
                            it.LastClearedAt = Json.GetString(row, "LastClearedAt", "");
                            it.Updated = Json.GetString(row, "Updated", "");
                            if (it.Course.Length > 0) st.Items.Add(it);
                        }
                    }
                }
                st.Log = Json.GetStrings(d, "Log");
            }
            catch (Exception ex)
            {
                // the file exists but does not parse: keep a copy before the app can ever overwrite it
                st.LoadFailed = true;
                st.LoadBackupPath = BackupBrokenFile();
                Log.Error("homework.json 解析失败（已备份到 " + st.LoadBackupPath + "）：" + ex.Message);
            }
            return st;
        }

        /// <summary>Copies a damaged file next to the original (never moves or deletes it).</summary>
        private static string BackupBrokenFile()
        {
            try
            {
                string dest = Paths.HomeworkFile + ".bad-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                File.Copy(Paths.HomeworkFile, dest, true);
                return dest;
            }
            catch (Exception ex)
            {
                Log.Error("备份损坏的 homework.json 失败：" + ex.Message);
                return Paths.HomeworkFile;
            }
        }

        /// <summary>
        /// Writes a file in one step: the caller's data is written to a temporary file first, so a crash
        /// or a kill in the middle can never truncate the existing (good) file.
        /// </summary>
        public static void WriteAtomic(string path, string text)
        {
            string tmp = path + ".tmp-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            File.WriteAllText(tmp, text, Encoding.UTF8);
            try
            {
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
            }
            catch
            {
                // File.Replace can be refused (temporarily locked target): fall back to a plain copy
                File.Copy(tmp, path, true);
                try { File.Delete(tmp); } catch { }
            }
        }

        public static void SaveHomework(HomeworkState st)
        {
            try
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                List<object> rows = new List<object>();
                for (int i = 0; i < st.Items.Count; i++)
                {
                    HomeworkItem it = st.Items[i];
                    Dictionary<string, object> row = new Dictionary<string, object>();
                    row["Course"] = it.Course;
                    row["Text"] = it.Text;
                    row["Method"] = it.Method;
                    row["Done"] = it.Done;
                    row["LastClearedKey"] = it.LastClearedKey;
                    row["LastText"] = it.LastText;
                    row["LastClearedAt"] = it.LastClearedAt;
                    row["Updated"] = it.Updated;
                    rows.Add(row);
                }
                d["Items"] = rows;
                d["Log"] = st.Log;
                WriteAtomic(Paths.HomeworkFile, Json.Pretty(d));
            }
            catch (Exception ex)
            {
                Log.Error("保存 homework.json 失败：" + ex.Message);
            }
        }

        /// <summary>Copy an imported .ics into the app data folder.</summary>
        public static bool ImportIcs(string sourcePath, out string error)
        {
            error = "";
            try
            {
                if (!File.Exists(sourcePath))
                {
                    error = "文件不存在：" + sourcePath;
                    return false;
                }
                byte[] bytes = File.ReadAllBytes(sourcePath);
                File.WriteAllBytes(Paths.IcsFile, bytes);
                Log.Info("已导入课表：" + sourcePath + " -> " + Paths.IcsFile);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Log.Error("导入课表失败：" + ex.Message);
                return false;
            }
        }

        /// <summary>On very first run, look for a .ics on the desktop and import it.</summary>
        public static string AutoDetectDesktopIcs()
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (string.IsNullOrEmpty(desktop) || !Directory.Exists(desktop)) return null;
                string[] files = Directory.GetFiles(desktop, "*.ics");
                if (files.Length == 0) return null;
                string best = null;
                DateTime bestTime = DateTime.MinValue;
                for (int i = 0; i < files.Length; i++)
                {
                    DateTime t = File.GetLastWriteTime(files[i]);
                    if (best == null || t > bestTime)
                    {
                        best = files[i];
                        bestTime = t;
                    }
                }
                return best;
            }
            catch
            {
                return null;
            }
        }
    }
}
