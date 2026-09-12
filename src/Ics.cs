using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CampusClock
{
    public class IcsProperty
    {
        public string Name = "";
        public Dictionary<string, string> Params = new Dictionary<string, string>();
        public string Value = "";
    }

    public class IcsDocument
    {
        public List<CourseRule> Rules = new List<CourseRule>();
        public List<string> Warnings = new List<string>();
        public int EventCount;
        public int SkippedEvents;
        public string SourcePath = "";

        public List<string> Courses()
        {
            List<string> list = new List<string>();
            for (int i = 0; i < Rules.Count; i++)
            {
                if (!list.Contains(Rules[i].Summary)) list.Add(Rules[i].Summary);
            }
            list.Sort(StringComparer.CurrentCulture);
            return list;
        }

        public DateTime FirstDate()
        {
            DateTime d = DateTime.MaxValue;
            for (int i = 0; i < Rules.Count; i++)
            {
                if (Rules[i].FirstDate < d) d = Rules[i].FirstDate;
            }
            return d == DateTime.MaxValue ? DateTime.MinValue : d.Date;
        }

        public DateTime LastDate()
        {
            DateTime d = DateTime.MinValue;
            for (int i = 0; i < Rules.Count; i++)
            {
                DateTime u = Rules[i].LastOccurrence();
                if (u > d) d = u;
            }
            return d.Date;
        }
    }

    public static class Ics
    {
        public static IcsDocument Load(string path)
        {
            IcsDocument doc = new IcsDocument();
            doc.SourcePath = path;
            string text = ReadText(path);
            List<string> lines = Unfold(text);
            List<IcsProperty> current = null;
            string currentUid = "";
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                if (line.Length == 0) continue;
                if (string.Equals(line, "BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
                {
                    current = new List<IcsProperty>();
                    currentUid = "";
                    continue;
                }
                if (string.Equals(line, "END:VEVENT", StringComparison.OrdinalIgnoreCase))
                {
                    if (current != null)
                    {
                        doc.EventCount++;
                        try
                        {
                            List<CourseRule> rules = BuildRules(current, doc);
                            if (rules.Count == 0) doc.SkippedEvents++;
                            else doc.Rules.AddRange(rules);
                        }
                        catch (Exception ex)
                        {
                            doc.SkippedEvents++;
                            doc.Warnings.Add("解析事件失败：" + ex.Message);
                        }
                    }
                    current = null;
                    continue;
                }
                if (current == null) continue;
                IcsProperty p = ParseProperty(line);
                if (p != null)
                {
                    if (p.Name == "UID") currentUid = p.Value;
                    current.Add(p);
                }
            }
            for (int i = 0; i < doc.Rules.Count; i++)
            {
                doc.Rules[i].ColorIndex = ColorIndexFor(doc.Rules[i].Summary);
            }
            return doc;
        }

        private static string ReadText(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            try
            {
                UTF8Encoding strict = new UTF8Encoding(false, true);
                string s = strict.GetString(bytes);
                return s;
            }
            catch
            {
                // not valid UTF-8: fall back to GB18030 (common for exported Chinese files)
            }
            try
            {
                Encoding gb = Encoding.GetEncoding(54936); // GB18030
                return gb.GetString(bytes);
            }
            catch
            {
                return Encoding.Default.GetString(bytes);
            }
        }

        /// <summary>Join RFC 5545 folded lines (continuation starts with space or tab).</summary>
        public static List<string> Unfold(string text)
        {
            List<string> result = new List<string>();
            string[] raw = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < raw.Length; i++)
            {
                string line = raw[i];
                if (line.Length > 0 && (line[0] == ' ' || line[0] == '\t'))
                {
                    sb.Append(line.Substring(1));
                    continue;
                }
                if (sb.Length > 0) result.Add(sb.ToString());
                sb.Length = 0;
                sb.Append(line);
            }
            if (sb.Length > 0) result.Add(sb.ToString());
            return result;
        }

        public static IcsProperty ParseProperty(string line)
        {
            int colon = -1;
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"') inQuotes = !inQuotes;
                if (c == ':' && !inQuotes)
                {
                    colon = i;
                    break;
                }
            }
            if (colon < 0) return null;
            string head = line.Substring(0, colon);
            string value = line.Substring(colon + 1);
            IcsProperty p = new IcsProperty();
            string[] parts = head.Split(';');
            p.Name = parts[0].Trim().ToUpperInvariant();
            for (int i = 1; i < parts.Length; i++)
            {
                int eq = parts[i].IndexOf('=');
                if (eq > 0)
                {
                    string k = parts[i].Substring(0, eq).Trim().ToUpperInvariant();
                    string v = parts[i].Substring(eq + 1).Trim().Trim('"');
                    p.Params[k] = v;
                }
            }
            p.Value = value;
            return p;
        }

        private static string Param(IcsProperty p, string key)
        {
            if (p.Params.ContainsKey(key)) return p.Params[key];
            return null;
        }

        private static List<CourseRule> BuildRules(List<IcsProperty> props, IcsDocument doc)
        {
            List<CourseRule> outRules = new List<CourseRule>();
            IcsProperty dtStart = null;
            IcsProperty dtEnd = null;
            IcsProperty summary = null;
            IcsProperty location = null;
            IcsProperty description = null;
            IcsProperty rrule = null;
            IcsProperty status = null;
            List<IcsProperty> exdates = new List<IcsProperty>();

            for (int i = 0; i < props.Count; i++)
            {
                string n = props[i].Name;
                if (n == "DTSTART") dtStart = props[i];
                else if (n == "DTEND") dtEnd = props[i];
                else if (n == "SUMMARY") summary = props[i];
                else if (n == "LOCATION") location = props[i];
                else if (n == "DESCRIPTION") description = props[i];
                else if (n == "RRULE") rrule = props[i];
                else if (n == "STATUS") status = props[i];
                else if (n == "EXDATE") exdates.Add(props[i]);
            }

            if (status != null && status.Value.Trim().Equals("CANCELLED", StringComparison.OrdinalIgnoreCase))
            {
                return outRules;
            }
            if (dtStart == null || summary == null) return outRules;

            DateTime start;
            if (!TryParseDateTime(dtStart, out start)) return outRules;
            DateTime end;
            if (dtEnd == null || !TryParseDateTime(dtEnd, out end) || end <= start)
            {
                end = start.AddMinutes(100);
            }

            string title = Unescape(summary.Value).Trim();
            if (title.Length == 0) return outRules;
            string where = location != null ? Unescape(location.Value).Trim() : "";
            string desc = description != null ? Unescape(description.Value) : "";

            string period = "";
            Match m = Regex.Match(desc, @"第\s*(\d+)\s*[-–~至]\s*(\d+)\s*节");
            if (m.Success)
            {
                period = "第" + m.Groups[1].Value + "-" + m.Groups[2].Value + "节";
            }
            else
            {
                Match m2 = Regex.Match(desc, @"第\s*(\d+)\s*节");
                if (m2.Success) period = "第" + m2.Groups[1].Value + "节";
            }

            int interval = 1;
            DateTime until = DateTime.MaxValue;
            List<DayOfWeek> byDays = new List<DayOfWeek>();
            string freq = "WEEKLY";
            if (rrule != null)
            {
                string[] bits = rrule.Value.Split(';');
                for (int i = 0; i < bits.Length; i++)
                {
                    string b = bits[i].Trim();
                    if (b.Length == 0) continue;
                    int eq = b.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = b.Substring(0, eq).Trim().ToUpperInvariant();
                    string v = b.Substring(eq + 1).Trim();
                    if (k == "FREQ") freq = v.ToUpperInvariant();
                    else if (k == "INTERVAL")
                    {
                        int parsed;
                        if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) && parsed > 0)
                            interval = parsed;
                    }
                    else if (k == "UNTIL")
                    {
                        DateTime u;
                        if (TryParseRaw(v, null, out u))
                        {
                            // UNTIL is inclusive
                            until = u;
                        }
                    }
                    else if (k == "BYDAY")
                    {
                        string[] dayTokens = v.Split(',');
                        for (int j = 0; j < dayTokens.Length; j++)
                        {
                            string d = dayTokens[j].Trim().ToUpperInvariant();
                            if (d.Length > 2) d = d.Substring(d.Length - 2);
                            if (d == "MO") byDays.Add(DayOfWeek.Monday);
                            else if (d == "TU") byDays.Add(DayOfWeek.Tuesday);
                            else if (d == "WE") byDays.Add(DayOfWeek.Wednesday);
                            else if (d == "TH") byDays.Add(DayOfWeek.Thursday);
                            else if (d == "FR") byDays.Add(DayOfWeek.Friday);
                            else if (d == "SA") byDays.Add(DayOfWeek.Saturday);
                            else if (d == "SU") byDays.Add(DayOfWeek.Sunday);
                        }
                    }
                }
            }

            if (until == DateTime.MinValue) until = DateTime.MaxValue;

            List<DateTime> ex = new List<DateTime>();
            for (int i = 0; i < exdates.Count; i++)
            {
                string[] vals = exdates[i].Value.Split(',');
                for (int j = 0; j < vals.Length; j++)
                {
                    DateTime d;
                    if (TryParseDateTime(exdates[i], vals[j].Trim(), out d)) ex.Add(d.Date);
                }
            }

            bool weekly = freq == "WEEKLY" || freq == "DAILY";
            if (!weekly)
            {
                doc.Warnings.Add("暂不支持的重复规则 FREQ=" + freq + "（" + title + "），仅按首次时间显示。");
            }

            List<DayOfWeek> days = new List<DayOfWeek>();
            if (byDays.Count > 0) days.AddRange(byDays);
            else days.Add(start.DayOfWeek);

            for (int i = 0; i < days.Count; i++)
            {
                CourseRule r = new CourseRule();
                r.Summary = title;
                r.Location = where;
                r.PeriodLabel = period;
                r.Day = days[i];
                r.StartMin = start.Hour * 60 + start.Minute;
                int dur = (int)Math.Round((end - start).TotalMinutes);
                if (dur <= 0) dur = 100;
                r.EndMin = r.StartMin + dur;
                r.FirstDate = start.Date;
                // move the first date forward to the requested weekday if needed
                if (r.FirstDate.DayOfWeek != r.Day)
                {
                    int delta = ((int)r.Day - (int)r.FirstDate.DayOfWeek + 7) % 7;
                    r.FirstDate = r.FirstDate.AddDays(delta);
                }
                r.UntilLocal = weekly ? until : start;
                r.IntervalWeeks = freq == "WEEKLY" ? interval : 1;
                r.ExDates = ex;
                outRules.Add(r);
            }
            return outRules;
        }

        private static bool TryParseDateTime(IcsProperty p, out DateTime result)
        {
            return TryParseDateTime(p, p.Value.Trim(), out result);
        }

        private static bool TryParseDateTime(IcsProperty p, string value, out DateTime result)
        {
            return TryParseRaw(value, Param(p, "TZID"), out result);
        }

        public static bool TryParseRaw(string value, string tzid, out DateTime result)
        {
            result = DateTime.MinValue;
            if (string.IsNullOrEmpty(value)) return false;
            value = value.Trim();
            bool utc = false;
            if (value.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
            {
                utc = true;
                value = value.Substring(0, value.Length - 1);
            }
            try
            {
                DateTime dt;
                if (value.Length == 8)
                {
                    // date only (all day)
                    dt = DateTime.ParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture);
                }
                else if (value.Length >= 15)
                {
                    dt = DateTime.ParseExact(value.Substring(0, 15), "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);
                }
                else
                {
                    return false;
                }
                if (utc)
                {
                    result = dt.Add(UtcOffsetFor(dt));
                    return true;
                }
                if (!string.IsNullOrEmpty(tzid))
                {
                    TimeZoneInfo tz = FindZone(tzid);
                    if (tz != null)
                    {
                        try
                        {
                            DateTime unspecified = DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
                            DateTime local = TimeZoneInfo.ConvertTime(unspecified, tz, TimeZoneInfo.Local);
                            result = local;
                            return true;
                        }
                        catch
                        {
                            // fall through to "treat as local"
                        }
                    }
                }
                result = dt;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static TimeSpan UtcOffsetFor(DateTime utcApprox)
        {
            try
            {
                return TimeZoneInfo.Local.GetUtcOffset(utcApprox);
            }
            catch
            {
                return TimeSpan.FromHours(8);
            }
        }

        private static readonly Dictionary<string, string> ZoneMap = BuildZoneMap();

        private static Dictionary<string, string> BuildZoneMap()
        {
            Dictionary<string, string> m = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            m["Asia/Shanghai"] = "China Standard Time";
            m["Asia/Chongqing"] = "China Standard Time";
            m["Asia/Urumqi"] = "China Standard Time";
            m["Asia/Hong_Kong"] = "Hong Kong Standard Time";
            m["Asia/Macau"] = "China Standard Time";
            m["Asia/Taipei"] = "Taipei Standard Time";
            m["Asia/Tokyo"] = "Tokyo Standard Time";
            m["Asia/Seoul"] = "Korea Standard Time";
            m["Asia/Singapore"] = "Singapore Standard Time";
            m["Asia/Bangkok"] = "SE Asia Standard Time";
            m["Europe/London"] = "GMT Standard Time";
            m["Europe/Paris"] = "Romance Standard Time";
            m["Europe/Berlin"] = "W. Europe Standard Time";
            m["Europe/Moscow"] = "Russian Standard Time";
            m["America/New_York"] = "Eastern Standard Time";
            m["America/Chicago"] = "Central Standard Time";
            m["America/Denver"] = "Mountain Standard Time";
            m["America/Los_Angeles"] = "Pacific Standard Time";
            m["Australia/Sydney"] = "AUS Eastern Standard Time";
            m["UTC"] = "UTC";
            return m;
        }

        private static TimeZoneInfo FindZone(string tzid)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(tzid);
            }
            catch
            {
                // not a Windows time zone id; try the IANA mapping
            }
            string mapped;
            if (ZoneMap.TryGetValue(tzid, out mapped))
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(mapped);
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        public static string Unescape(string value)
        {
            if (value == null) return "";
            StringBuilder sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\' && i + 1 < value.Length)
                {
                    char n = value[i + 1];
                    if (n == 'n' || n == 'N') { sb.Append('\n'); i++; continue; }
                    if (n == '\\') { sb.Append('\\'); i++; continue; }
                    if (n == ';') { sb.Append(';'); i++; continue; }
                    if (n == ',') { sb.Append(','); i++; continue; }
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        public static int ColorIndexFor(string course)
        {
            int hash = 0;
            for (int i = 0; i < course.Length; i++)
            {
                hash = (hash * 31 + course[i]) & 0x7fffffff;
            }
            return hash % 10;
        }
    }

    /// <summary>Timetable queries: period rows, week sessions, next class, week numbers.</summary>
    public class Schedule
    {
        public IcsDocument Doc;
        private List<PeriodRow> rows;

        public Schedule(IcsDocument doc)
        {
            Doc = doc;
            BuildRows();
        }

        public static DateTime WeekStart(DateTime date)
        {
            int dow = ((int)date.DayOfWeek + 6) % 7;   // Monday = 0
            return date.Date.AddDays(-dow);
        }

        public DateTime FirstWeekStart
        {
            get { return Doc == null || Doc.Rules.Count == 0 ? WeekStart(DateTime.Today) : WeekStart(Doc.FirstDate()); }
        }

        public int WeekIndex(DateTime date)
        {
            if (Doc == null || Doc.Rules.Count == 0) return 1;
            int days = (int)Math.Round((WeekStart(date) - FirstWeekStart).TotalDays);
            return (int)Math.Floor(days / 7.0) + 1;
        }

        public List<PeriodRow> Rows
        {
            get { return rows; }
        }

        private void BuildRows()
        {
            rows = new List<PeriodRow>();
            if (Doc == null) return;
            List<CourseRule> ordered = new List<CourseRule>(Doc.Rules);
            ordered.Sort(delegate (CourseRule a, CourseRule b)
            {
                int c = a.StartMin.CompareTo(b.StartMin);
                if (c != 0) return c;
                return a.EndMin.CompareTo(b.EndMin);
            });
            for (int i = 0; i < ordered.Count; i++)
            {
                CourseRule r = ordered[i];
                int existing = IndexOfRow(r.StartMin, r.EndMin);
                if (existing >= 0)
                {
                    if (rows[existing].Label.Length == 0 && r.PeriodLabel.Length > 0) rows[existing].Label = r.PeriodLabel;
                    continue;
                }
                PeriodRow row = new PeriodRow();
                row.StartMin = r.StartMin;
                row.EndMin = r.EndMin;
                row.TimeText = Fmt.Minutes(r.StartMin) + "-" + Fmt.Minutes(r.EndMin);
                row.Label = r.PeriodLabel;
                rows.Add(row);
            }
            rows.Sort(delegate (PeriodRow a, PeriodRow b) { return a.StartMin.CompareTo(b.StartMin); });
        }

        private int IndexOfRow(int startMin, int endMin)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].StartMin == startMin && rows[i].EndMin == endMin) return i;
            }
            return -1;
        }

        public int RowIndexOf(CourseRule r)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].StartMin == r.StartMin) return i;
            }
            for (int i = 0; i < rows.Count; i++)
            {
                if (r.StartMin >= rows[i].StartMin && r.StartMin < rows[i].EndMin) return i;
            }
            return -1;
        }

        public int RowSpanOf(CourseRule r)
        {
            int start = RowIndexOf(r);
            if (start < 0) return 1;
            int end = start;
            for (int i = start; i < rows.Count; i++)
            {
                if (rows[i].StartMin < r.EndMin) end = i;
            }
            return Math.Max(1, end - start + 1);
        }

        public List<Session> WeekSessions(DateTime weekStart)
        {
            List<Session> list = new List<Session>();
            if (Doc == null) return list;
            DateTime start = WeekStart(weekStart);
            for (int d = 0; d < 7; d++)
            {
                DateTime date = start.AddDays(d);
                for (int i = 0; i < Doc.Rules.Count; i++)
                {
                    CourseRule r = Doc.Rules[i];
                    if (!r.OccursOn(date)) continue;
                    Session s = new Session();
                    s.Rule = r;
                    s.Start = date.AddMinutes(r.StartMin);
                    s.End = date.AddMinutes(r.EndMin);
                    list.Add(s);
                }
            }
            list.Sort(delegate (Session a, Session b) { return a.Start.CompareTo(b.Start); });
            AssignLanes(list);
            return list;
        }

        /// <summary>Split overlapping classes of the same day into horizontal lanes.</summary>
        private static void AssignLanes(List<Session> sessions)
        {
            int i = 0;
            while (i < sessions.Count)
            {
                int j = i;
                DateTime day = sessions[i].Start.Date;
                while (j < sessions.Count && sessions[j].Start.Date == day) j++;
                List<int> laneEnds = new List<int>();
                for (int k = i; k < j; k++)
                {
                    int startM = sessions[k].Rule.StartMin;
                    int endM = sessions[k].Rule.EndMin;
                    int lane = -1;
                    for (int l = 0; l < laneEnds.Count; l++)
                    {
                        if (laneEnds[l] <= startM)
                        {
                            lane = l;
                            break;
                        }
                    }
                    if (lane < 0)
                    {
                        laneEnds.Add(endM);
                        lane = laneEnds.Count - 1;
                    }
                    else
                    {
                        laneEnds[lane] = endM;
                    }
                    sessions[k].Lane = lane;
                    sessions[k].LaneCount = 1;
                }
                for (int a = i; a < j; a++)
                {
                    int maxLane = sessions[a].Lane;
                    for (int b = i; b < j; b++)
                    {
                        if (b == a) continue;
                        bool overlap = sessions[a].Rule.StartMin < sessions[b].Rule.EndMin &&
                                       sessions[b].Rule.StartMin < sessions[a].Rule.EndMin;
                        if (overlap && sessions[b].Lane > maxLane) maxLane = sessions[b].Lane;
                    }
                    sessions[a].LaneCount = maxLane + 1;
                }
                i = j;
            }
        }

        public Session NextSession(DateTime now, int withinDays)
        {
            DateTime weekStart = WeekStart(now);
            List<Session> sessions = WeekSessions(weekStart);
            sessions.AddRange(WeekSessions(weekStart.AddDays(7)));
            if (withinDays > 7) sessions.AddRange(WeekSessions(weekStart.AddDays(14)));
            for (int i = 0; i < sessions.Count; i++)
            {
                if (sessions[i].Start >= now) return sessions[i];
            }
            return null;
        }

        /// <summary>Latest session of a course that has already finished, searching back a few weeks.</summary>
        public Session LatestFinishedSession(string course, DateTime now, int lookbackDays)
        {
            Session best = null;
            for (int w = 0; w <= (lookbackDays / 7) + 1; w++)
            {
                DateTime weekStart = WeekStart(now).AddDays(-7 * w);
                List<Session> sessions = WeekSessions(weekStart);
                for (int i = 0; i < sessions.Count; i++)
                {
                    Session s = sessions[i];
                    if (s.Course != course) continue;
                    if (s.End > now) continue;
                    if (best == null || s.End > best.End) best = s;
                }
                if (best != null && w > 0) break;
            }
            return best;
        }
    }
}
