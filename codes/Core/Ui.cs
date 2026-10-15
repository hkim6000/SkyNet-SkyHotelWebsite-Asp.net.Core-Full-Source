using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyHotel.Models;

namespace SkyHotel.Core
{
    public static class Ui
    {
        public static string Enc(string? v)
        {
            return System.Net.WebUtility.HtmlEncode(v ?? string.Empty);
        }

        public static string Attr(string? v)
        {
            return System.Net.WebUtility.HtmlEncode(v ?? string.Empty);
        }

        public static string Js(string? v)
        {
            return (v ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "&quot;").Replace("<", "\\x3c").Replace("\n", " ").Replace("\r", string.Empty);
        }

        public static string Clip(string? value, int max)
        {
            string v = (value ?? string.Empty).Trim();
            return v.Length > max ? v.Substring(0, max) : v;
        }

        public static string Iso(DateTime d)
        {
            return d.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        }

        public static string Ago(DateTime d)
        {
            TimeSpan t = DateTime.UtcNow - d.ToUniversalTime();
            if (t.TotalMinutes < 1)
            {
                return "Just now";
            }
            if (t.TotalMinutes < 60)
            {
                int m = (int)t.TotalMinutes;
                return m + (m == 1 ? " min ago" : " mins ago");
            }
            if (t.TotalHours < 24)
            {
                int h = (int)t.TotalHours;
                return h + (h == 1 ? " hour ago" : " hours ago");
            }
            return d.ToUniversalTime().ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        }

        public static string Time(DateTime d, string mode = "ago")
        {
            return "<time datetime=\"" + Iso(d) + "\" data-t=\"" + mode + "\">" + (mode == "ago" ? Ago(d) : d.ToUniversalTime().ToString("d MMMM yyyy, HH:mm 'UTC'", CultureInfo.InvariantCulture)) + "</time>";
        }

        public static string Initials(string name)
        {
            string[] parts = (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return "?";
            }
            string a = parts[0].Substring(0, 1);
            string b = parts.Length > 1 ? parts[^1].Substring(0, 1) : string.Empty;
            return (a + b).ToUpperInvariant();
        }

        public static string Hue(string id)
        {
            int h = 0;
            foreach (char c in id ?? string.Empty)
            {
                h = (h * 31 + c) % 360;
            }
            return h.ToString(CultureInfo.InvariantCulture);
        }

        public static string Avatar(string p, Staff? u, string size = "")
        {
            string name = u?.Name ?? "Staff";
            return "<span class=\"" + p + "-av" + (size != string.Empty ? " " + p + "-av-" + size : string.Empty) + "\" style=\"--h:" + Hue(u?.Id ?? name) + "\" aria-hidden=\"true\">" + Enc(Initials(name)) + "</span>";
        }


        public static string Pager(string p, int page, int size, int total, string one, string many, string js)
        {
            if (total == 0)
            {
                return string.Empty;
            }
            int pages = Math.Max(1, (total + size - 1) / size);
            string count = total + " " + (total == 1 ? one : many);
            return "<div class=\"" + p + "-pager\"><button type=\"button\" class=\"" + p + "-btn " + p + "-btn-sm " + p + "-btn-ghost\" onclick=\"" + js + "(" + (page - 1) + ")\"" + (page <= 1 ? " disabled" : string.Empty) + ">&larr; Previous</button><span>Page " + page + " of " + pages + " &middot; " + count + "</span><button type=\"button\" class=\"" + p + "-btn " + p + "-btn-sm " + p + "-btn-ghost\" onclick=\"" + js + "(" + (page + 1) + ")\"" + (page >= pages ? " disabled" : string.Empty) + ">Next &rarr;</button></div>";
        }

        public static int PageOf(string value, int size, int total)
        {
            int pages = Math.Max(1, (total + size - 1) / size);
            return Math.Clamp(int.TryParse(value, out int n) ? n : 1, 1, pages);
        }

        public static Dictionary<string, Staff> StaffMap(List<Staff> list)
        {
            Dictionary<string, Staff> d = new Dictionary<string, Staff>();
            foreach (Staff s in list)
            {
                d[s.Id] = s;
            }
            return d;
        }

        public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Money(decimal v, bool cents = false)
        {
            return (v < 0 ? "&minus;$" : "$") + Math.Abs(v).ToString(cents ? "#,##0.00" : "#,##0", Inv);
        }

        public static string Cash(decimal v)
        {
            return (v < 0 ? "-$" : "$") + Math.Abs(v).ToString("#,##0.00", Inv);
        }

        public static string Num(int v)
        {
            return v.ToString("#,##0", Inv);
        }

        public static string Day(DateTime d, bool year = true)
        {
            return d.ToString(year ? "ddd d MMM yyyy" : "ddd d MMM", Inv);
        }

        public static string Ymd(DateTime d)
        {
            return d.ToString("yyyy-MM-dd", Inv);
        }

        public static DateTime? ParseDay(string? v)
        {
            return DateTime.TryParseExact((v ?? string.Empty).Trim(), "yyyy-MM-dd", Inv, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime d) ? DateTime.SpecifyKind(d.Date, DateTimeKind.Utc) : null;
        }

        public static string Thumb(string full)
        {
            if (full.Contains("/full/", StringComparison.Ordinal))
            {
                return full.Replace("/full/", "/thumbs/");
            }
            return full.StartsWith("images/uploads/", StringComparison.Ordinal) ? Regex.Replace(full, "\\.(webp|jpg|png)$", "-t.$1") : full;
        }

        public static bool IsVideo(string? src)
        {
            return Regex.IsMatch(src ?? string.Empty, "\\.(mp4|webm)$");
        }

        public static MediaItem? MediaOf(SiteData site, string src)
        {
            return site.Media.FirstOrDefault(m => m.Full == src);
        }

        public static string Poster(SiteData site, string video)
        {
            MediaItem? m = MediaOf(site, video);
            return m != null && m.Thumb != string.Empty ? m.Thumb : "images/hotel/thumbs/exterior-dusk.webp";
        }

        public static string Clock(int secs)
        {
            return (secs / 60).ToString(Inv) + ":" + (secs % 60).ToString("00", Inv);
        }

        public static string Picker(string p, SiteData site, string name, string kind, IEnumerable<string> chosen, bool multi, bool ro, bool none = false)
        {
            List<string> pick = chosen.Where(x => x != string.Empty).ToList();
            List<MediaItem> items = pick.Select(x => MediaOf(site, x)).Where(m => m != null && (kind == string.Empty || m.Kind == kind)).Select(m => m!).ToList();
            items.AddRange(site.Media.Where(m => (kind == string.Empty || m.Kind == kind) && !pick.Contains(m.Full)).OrderByDescending(m => m.When));
            StringBuilder sb = new StringBuilder("<div class=\"" + p + "-imgs\">");
            string type = multi ? "checkbox" : "radio";
            string off = ro ? " disabled" : string.Empty;
            if (none)
            {
                sb.Append("<label class=\"" + p + "-pnone\"><input type=\"radio\" name=\"" + name + "\" value=\"\"" + (pick.Count == 0 ? " checked" : string.Empty) + off + "><span>No video</span></label>");
            }
            foreach (MediaItem m in items)
            {
                bool vid = m.Kind == "video";
                sb.Append("<label title=\"" + Attr(m.Title) + "\"><input type=\"" + type + "\" name=\"" + name + "\" value=\"" + Attr(m.Full) + "\"" + (pick.Contains(m.Full) ? " checked" : string.Empty) + off + "><img src=\"" + Attr(m.Thumb) + "\" alt=\"\" loading=\"lazy\">" + (vid ? "<i class=\"" + p + "-pplay\"></i><em class=\"" + p + "-pdur\">" + Clock(m.Seconds) + "</em>" : string.Empty) + "<small>" + Enc(m.Title) + "</small></label>");
            }
            if (items.Count == 0)
            {
                sb.Append("<p class=\"" + p + "-hint\">Nothing in the media library yet.</p>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        public static string Pic(string src, string alt, string cls, bool thumb = false, bool eager = false)
        {
            return "<img class=\"" + cls + "\" src=\"" + Attr(thumb ? Thumb(src) : src) + "\" alt=\"" + Attr(alt) + "\"" + (eager ? string.Empty : " loading=\"lazy\"") + " width=\"" + (thumb ? "640" : "1600") + "\" height=\"" + (thumb ? "427" : "1067") + "\">";
        }

        public const string LatestOut = "15:00";

        public static bool LateOk(Guest? g)
        {
            return g != null && g.Member && g.Lifetime >= Grades[1].Min;
        }

        public static string OutTime(SiteData site, Booking b)
        {
            return b.LateOut != string.Empty ? b.LateOut : site.Settings.CheckOut;
        }

        public static List<string> OutTimes(SiteData site)
        {
            List<string> list = new List<string>();
            TimeSpan t = TimeSpan.TryParse(site.Settings.CheckOut, Inv, out TimeSpan x) ? x : new TimeSpan(11, 0, 0);
            TimeSpan end = TimeSpan.Parse(LatestOut, Inv);
            list.Add(t.ToString("hh\\:mm", Inv));
            for (TimeSpan u = new TimeSpan(t.Hours, t.Minutes >= 30 ? 30 : 0, 0).Add(TimeSpan.FromMinutes(30)); u <= end; u = u.Add(TimeSpan.FromMinutes(30)))
            {
                if (u > t)
                {
                    list.Add(u.ToString("hh\\:mm", Inv));
                }
            }
            return list;
        }

        public static string LateBadge(string p, Guest? g)
        {
            return LateOk(g) ? "<span class=\"" + p + "-late\">Late checkout eligible</span>" : string.Empty;
        }

        public static string StayBadge(string p, Booking b)
        {
            string label = b.Status switch { "inhouse" => "Checked in", "out" => "Checked out", "cancelled" => "Cancelled", "noshow" => "No-show", _ => "Booked" };
            return "<span class=\"" + p + "-st " + p + "-st-" + b.Status + "\">" + label + "</span>";
        }

        public static string CleanPhone(string? v)
        {
            return Regex.Replace(Clip(v, 30), "[^0-9+()\\-. ]", string.Empty).Trim();
        }

        public static readonly string[] Countries =
        {
            "Argentina", "Australia", "Austria", "Belgium", "Brazil", "Canada", "Chile", "China", "Colombia", "Czech Republic", "Denmark", "Egypt", "Finland", "France", "Germany", "Greece",
            "Hong Kong", "Iceland", "India", "Indonesia", "Ireland", "Israel", "Italy", "Japan", "Malaysia", "Mexico", "Morocco", "Netherlands", "New Zealand", "Nigeria", "Norway", "Peru",
            "Philippines", "Poland", "Portugal", "Saudi Arabia", "Singapore", "South Africa", "South Korea", "Spain", "Sweden", "Switzerland", "Taiwan", "Thailand", "Turkey",
            "United Arab Emirates", "United Kingdom", "United States", "Vietnam"
        };

        public static string CleanPassport(string? v)
        {
            return Regex.Replace(Clip(v, 20).ToUpperInvariant(), "[^A-Z0-9]", string.Empty);
        }

        public static string MaskPassport(string? v)
        {
            string p = v ?? string.Empty;
            return p.Length == 0 ? string.Empty : "&bull;&bull;&bull;&bull; " + Enc(p.Length > 4 ? p.Substring(p.Length - 4) : p);
        }

        public static bool NeedsPassport(SiteData site, string country)
        {
            return country != string.Empty && !country.Equals(site.Settings.Country, StringComparison.OrdinalIgnoreCase);
        }

        public static string CleanPlate(string? v)
        {
            return Regex.Replace(Clip(v, 12).ToUpperInvariant(), "[^A-Z0-9 -]", string.Empty).Trim();
        }

        public static RoomType? TypeOf(SiteData site, string key)
        {
            return site.Types.FirstOrDefault(t => t.Key == key);
        }

        public static string TypeName(SiteData site, string key)
        {
            return TypeOf(site, key)?.Name ?? key;
        }

        public static Dictionary<string, Guest> GuestMap(SiteData site)
        {
            Dictionary<string, Guest> d = new Dictionary<string, Guest>();
            foreach (Guest g in site.Guests)
            {
                d[g.Id] = g;
            }
            return d;
        }

        public static string GuestName(Guest? g)
        {
            return g == null ? "Guest" : (g.First + " " + g.Last).Trim();
        }

        public static readonly (string Key, string Name, int Min, int Pct, string Perks)[] Grades =
        {
            ("member", "Member", 0, 5, "Member rate 5% off"),
            ("bronze", "Bronze", 1000, 5, "5% off, welcome drink, late checkout"),
            ("silver", "Silver", 10000, 7, "7% off, welcome drink, late checkout, priority check-in"),
            ("gold", "Gold", 100000, 10, "10% off, late checkout, free breakfast, upgrade when available"),
            ("diamond", "Diamond", 1000000, 15, "15% off, late checkout, suite upgrade, lounge access, guaranteed room")
        };

        public static (string Key, string Name, int Min, int Pct, string Perks) GradeOf(int lifetime)
        {
            (string, string, int, int, string) g = Grades[0];
            foreach ((string, string, int, int, string) x in Grades)
            {
                if (lifetime >= x.Item3)
                {
                    g = x;
                }
            }
            return g;
        }

        public static (string Key, string Name, int Min, int Pct, string Perks)? NextGrade(int lifetime)
        {
            foreach ((string, string, int, int, string) x in Grades)
            {
                if (lifetime < x.Item3)
                {
                    return x;
                }
            }
            return null;
        }

        public static string GradePill(string p, Guest g)
        {
            (string key, string name, _, _, _) = GradeOf(g.Lifetime);
            return "<span class=\"" + p + "-grade " + p + "-g-" + key + "\">" + name + "</span>";
        }

        public static string Opts(IEnumerable<(string Value, string Text)> items, string cur)
        {
            StringBuilder sb = new StringBuilder();
            foreach ((string v, string t) in items)
            {
                sb.Append("<option value=\"" + Attr(v) + "\"" + (v == cur ? " selected" : string.Empty) + ">" + Enc(t) + "</option>");
            }
            return sb.ToString();
        }

        public static string DataList(string id, IEnumerable<string> values)
        {
            StringBuilder sb = new StringBuilder("<datalist id=\"" + id + "\">");
            foreach (string v in values.Where(x => x != string.Empty).Distinct().OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                sb.Append("<option value=\"" + Attr(v) + "\"></option>");
            }
            sb.Append("</datalist>");
            return sb.ToString();
        }

        public static (DateTime? From, DateTime? To) Period(string period, string from, string to, bool future = false)
        {
            DateTime today = DateTime.UtcNow.Date;
            return period switch
            {
                "today" => (today, today),
                "7" => future ? (today, today.AddDays(6)) : (today.AddDays(-6), today),
                "30" => future ? (today, today.AddDays(29)) : (today.AddDays(-29), today),
                "range" => (ParseDay(from), ParseDay(to)),
                _ => (null, null)
            };
        }

        public static bool InPeriod(DateTime d, DateTime? from, DateTime? to)
        {
            return (from == null || d.Date >= from.Value) && (to == null || d.Date <= to.Value);
        }

        public static string PeriodBar(string p, string js, string cur, string from, string to, bool future = false)
        {
            (string k, string n)[] chips = { ("today", "Today"), ("7", future ? "Next 7 days" : "7 days"), ("30", future ? "Next 30 days" : "30 days"), ("all", "All") };
            StringBuilder sb = new StringBuilder("<div class=\"" + p + "-period\"><div class=\"" + p + "-chips\">");
            foreach ((string k, string n) in chips)
            {
                sb.Append("<button type=\"button\" class=\"" + p + "-chip" + (k == cur ? " " + p + "-act" : string.Empty) + "\" onclick=\"" + js + "('" + k + "')\">" + n + "</button>");
            }
            sb.Append("</div><label class=\"" + p + "-pf\"><span>From</span><input type=\"date\" id=\"" + p + "-from\" value=\"" + Attr(cur == "range" ? from : string.Empty) + "\" onchange=\"" + js + "('range')\"></label><label class=\"" + p + "-pf\"><span>To</span><input type=\"date\" id=\"" + p + "-to\" value=\"" + Attr(cur == "range" ? to : string.Empty) + "\" onchange=\"" + js + "('range')\"></label><input type=\"hidden\" id=\"" + p + "-per\" value=\"" + Attr(cur) + "\"></div>");
            return sb.ToString();
        }
    }
}
