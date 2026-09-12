using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Threading;

namespace CampusClock
{
    /// <summary>Shared application state: config, timetable, homework.</summary>
    public class AppCore
    {
        public AppConfig Config;
        public HomeworkState Homework;
        public IcsDocument Doc;
        public Schedule Schedule;

        public event EventHandler ConfigChanged;
        public event EventHandler HomeworkChanged;
        public event EventHandler TimetableChanged;

        private DispatcherTimer saveTimer;
        private bool savePending;

        public AppCore()
        {
            Config = Storage.LoadConfig();
            Homework = Storage.LoadHomework();
            Palette.SetAccent(Config.Accent);
            saveTimer = new DispatcherTimer();
            saveTimer.Interval = TimeSpan.FromMilliseconds(700);
            saveTimer.Tick += delegate
            {
                saveTimer.Stop();
                if (savePending)
                {
                    savePending = false;
                    Storage.SaveHomework(Homework);
                }
            };
        }

        public void LoadTimetable()
        {
            if (!File.Exists(Paths.IcsFile))
            {
                string guess = Storage.AutoDetectDesktopIcs();
                if (guess != null)
                {
                    string err;
                    Storage.ImportIcs(guess, out err);
                }
            }
            if (File.Exists(Paths.IcsFile))
            {
                try
                {
                    Doc = Ics.Load(Paths.IcsFile);
                    Schedule = new Schedule(Doc);
                    Log.Info("课表已加载：" + Doc.Rules.Count + " 条课程规则，" + Doc.EventCount + " 个事件");
                }
                catch (Exception ex)
                {
                    Log.Error("课表解析失败：" + ex.Message);
                    Doc = null;
                    Schedule = null;
                }
            }
            if (Schedule == null)
            {
                Doc = new IcsDocument();
                Schedule = new Schedule(Doc);
            }
            SyncTrackedCourses();
            if (TimetableChanged != null) TimetableChanged(this, EventArgs.Empty);
        }

        public bool ImportIcs(string path, out string error)
        {
            bool ok = Storage.ImportIcs(path, out error);
            if (ok)
            {
                LoadTimetable();
                SaveConfig();
            }
            return ok;
        }

        /// <summary>Keep the tracked course list in sync with the timetable.</summary>
        public void SyncTrackedCourses()
        {
            List<string> courses = Doc != null ? Doc.Courses() : new List<string>();
            List<string> keep = new List<string>();
            for (int i = 0; i < courses.Count; i++)
            {
                if (Config.TrackedCourses.Contains(courses[i])) keep.Add(courses[i]);
            }
            bool changed = keep.Count != Config.TrackedCourses.Count;
            Config.TrackedCourses = keep;
            Homework.RetainOnly(keep);
            for (int i = 0; i < keep.Count; i++) Homework.Ensure(keep[i]);
            if (changed) Storage.SaveConfig(Config);
        }

        public void TrackCourse(string course, bool on)
        {
            if (on)
            {
                if (!Config.TrackedCourses.Contains(course))
                {
                    Config.TrackedCourses.Add(course);
                    Homework.Ensure(course);
                }
            }
            else
            {
                Config.TrackedCourses.Remove(course);
            }
            SaveConfig();
            SaveHomeworkSoon();
            if (HomeworkChanged != null) HomeworkChanged(this, EventArgs.Empty);
        }

        public void SaveConfig()
        {
            Storage.SaveConfig(Config);
            if (ConfigChanged != null) ConfigChanged(this, EventArgs.Empty);
        }

        public void SaveHomeworkSoon()
        {
            savePending = true;
            saveTimer.Stop();
            saveTimer.Start();
        }

        public void SaveHomeworkNow()
        {
            savePending = false;
            saveTimer.Stop();
            Storage.SaveHomework(Homework);
        }

        public void RaiseHomeworkChanged()
        {
            if (HomeworkChanged != null) HomeworkChanged(this, EventArgs.Empty);
        }

        public void RaiseConfigChanged()
        {
            if (ConfigChanged != null) ConfigChanged(this, EventArgs.Empty);
        }

        /// <summary>
        /// Clear homework content after a class meeting. Returns the courses that were cleared.
        /// </summary>
        public List<string> CheckAutoClear(DateTime now)
        {
            List<string> cleared = new List<string>();
            if (Config.ClearMode == "Manual") return cleared;
            if (Schedule == null) return cleared;
            for (int i = 0; i < Config.TrackedCourses.Count; i++)
            {
                string course = Config.TrackedCourses[i];
                Session s = Schedule.LatestFinishedSession(course, now, 21);
                if (s == null) continue;
                string key = Config.ClearMode == "PerDay"
                    ? Fmt.Date(s.Start)
                    : Fmt.Date(s.Start) + " " + Fmt.Minutes(s.Rule.StartMin);
                HomeworkItem item = Homework.Ensure(course);
                if (item.LastClearedKey.Length == 0)
                {
                    // first observation: remember it as the baseline, do not clear now
                    item.LastClearedKey = key;
                    SaveHomeworkNow();
                    continue;
                }
                if (item.LastClearedKey != key)
                {
                    item.LastClearedKey = key;
                    if (item.Text.Trim().Length > 0 || item.Done)
                    {
                        item.Text = "";
                        item.Done = false;
                        item.Updated = Fmt.DateTimeText(now);
                        cleared.Add(course);
                    }
                }
            }
            if (cleared.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < cleared.Count; i++)
                {
                    if (i > 0) sb.Append("、");
                    sb.Append(cleared[i]);
                }
                string msg = Fmt.DateTimeText(now) + " 课后自动清空作业：" + sb.ToString();
                Homework.Log.Add(msg);
                if (Homework.Log.Count > 200) Homework.Log.RemoveAt(0);
                Log.Info(msg);
                SaveHomeworkNow();
                if (HomeworkChanged != null) HomeworkChanged(this, EventArgs.Empty);
            }
            return cleared;
        }

        public string ClearModeText()
        {
            if (Config.ClearMode == "Manual") return "手动清空";
            if (Config.ClearMode == "PerDay") return "每天首次课后清空";
            return "每次课后清空";
        }
    }
}
