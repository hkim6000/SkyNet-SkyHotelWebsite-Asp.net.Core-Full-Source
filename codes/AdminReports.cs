using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyHotel.Core;
using SkyHotel.Models;
using SkyNet;

namespace SkyHotel.codes
{
    public class AdminReports : WebPage
    {
        private const string AreaKey = "reports";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Reports");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--rp-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("rp-menu", Auth.Menu("rp", "AdminReports", c, site));
            response.SetElementContents("rp-who", Auth.Who("rp", c));
            response.SetElementContents("rp-demo", Auth.DemoPill("rp", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminReportsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"rp-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"rp-panel\"><p>You don't have access to this area. Ask the general manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminReportsJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminReportsJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("rp-main", NoAccess("Reports"));
                return response;
            }
            response.SetElementContents("rp-main", await MainHtml(c, site));
            response.ExecuteScript("AdminReportsJs.loaded();");
            return response;
        }

        private static string Per(string v)
        {
            return v is "today" or "7" or "30" or "range" ? v : "all";
        }

        private static string Who(Dictionary<string, Staff> staff, string id)
        {
            return id == "web" ? "Website" : staff.TryGetValue(id, out Staff? s) ? s.Name : id;
        }

        private static string Head((string Name, string Cls)[] cols)
        {
            StringBuilder sb = new StringBuilder("<div class=\"rp-tr rp-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static async Task<string> MainHtml(Ctx c, SiteData site)
        {
            StringBuilder sb = new StringBuilder("<div class=\"rp-ptop\"><div><h1>Reports</h1><p>Occupancy, rates and revenue by night. Room revenue excludes tax and breakfast. Future nights count bookings on the books.</p></div></div>");
            sb.Append("<div class=\"rp-bar\" id=\"rp-rbar\"></div><div id=\"rp-report\"><p class=\"rp-loading\">Working&hellip;</p></div>");
            return sb.ToString();
        }

        private static string Bar(string cur, string from, string to)
        {
            (string k, string n)[] items = { ("today", "Today"), ("week", "This week"), ("7", "Last 7 days"), ("30", "Last 30 days"), ("month", "This month"), ("next30", "Next 30 days") };
            StringBuilder sb = new StringBuilder("<div class=\"rp-chips\">");
            foreach ((string k, string n) in items)
            {
                sb.Append("<button type=\"button\" class=\"rp-chip" + (k == cur ? " rp-act" : string.Empty) + "\" onclick=\"AdminReportsJs.preset('" + k + "')\">" + n + "</button>");
            }
            sb.Append("</div><label class=\"rp-pf\"><span>From</span><input type=\"date\" id=\"rp-from\" value=\"" + Ui.Attr(cur == "range" ? from : string.Empty) + "\" onchange=\"AdminReportsJs.preset('range')\"></label><label class=\"rp-pf\"><span>To</span><input type=\"date\" id=\"rp-to\" value=\"" + Ui.Attr(cur == "range" ? to : string.Empty) + "\" onchange=\"AdminReportsJs.preset('range')\"></label><input type=\"hidden\" id=\"rp-rper\" value=\"" + Ui.Attr(cur) + "\">");
            return sb.ToString();
        }

        public async Task<ApiResponse> Run()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminReportsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            DateTime today = DateTime.UtcNow.Date;
            string per = GetDataValue("rper") is "today" or "week" or "7" or "month" or "next30" or "range" ? GetDataValue("rper") : "30";
            DateTime from = per switch { "today" => today, "week" => today.AddDays(-(int)today.DayOfWeek), "7" => today.AddDays(-6), "month" => new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc), "next30" => today, _ => today.AddDays(-29) };
            DateTime to = per switch { "week" => today.AddDays(6 - (int)today.DayOfWeek), "next30" => today.AddDays(29), "month" => new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1).AddDays(-1), _ => today };
            if (per == "range")
            {
                from = Ui.ParseDay(GetDataValue("pfrom")) ?? today.AddDays(-29);
                to = Ui.ParseDay(GetDataValue("pto")) ?? today;
                if (to < from)
                {
                    (from, to) = (to, from);
                }
                if ((to - from).TotalDays > 120)
                {
                    to = from.AddDays(120);
                }
            }
            int days = (int)(to - from).TotalDays + 1;
            int rooms = site.Rooms.Count;
            List<Booking> stays = site.Bookings.Where(b => (b.Status is "booked" or "inhouse" or "out") && b.In.Date <= to && b.Out.Date > from).ToList();
            Dictionary<string, Guest> gm = Ui.GuestMap(site);
            int[] sold = new int[days];
            decimal[] rev = new decimal[days];
            int memberNights = 0;
            Dictionary<string, (int Nights, decimal Rev)> byType = site.Types.ToDictionary(t => t.Key, t => (0, 0m));
            Dictionary<string, (int Nights, decimal Rev, int Count)> bySrc = new Dictionary<string, (int, decimal, int)>();
            foreach (Booking b in stays)
            {
                decimal pn = Book.PerNight(b);
                bool mem = gm.TryGetValue(b.Guest, out Guest? g) && g.Member;
                int n = 0;
                for (DateTime d = b.In.Date; d < b.Out.Date; d = d.AddDays(1))
                {
                    if (d < from || d > to)
                    {
                        continue;
                    }
                    int i = (int)(d - from).TotalDays;
                    sold[i]++;
                    rev[i] += pn;
                    n++;
                }
                if (n == 0)
                {
                    continue;
                }
                memberNights += mem ? n : 0;
                if (byType.TryGetValue(b.Type, out (int Nights, decimal Rev) t))
                {
                    byType[b.Type] = (t.Nights + n, t.Rev + pn * n);
                }
                (int Nights, decimal Rev, int Count) s = bySrc.TryGetValue(b.Source, out (int, decimal, int) x) ? x : (0, 0m, 0);
                bySrc[b.Source] = (s.Nights + n, s.Rev + pn * n, s.Count + 1);
            }
            int nights = sold.Sum();
            decimal revenue = rev.Sum();
            double occ = rooms * days > 0 ? nights * 100.0 / (rooms * days) : 0;
            decimal adr = nights > 0 ? revenue / nights : 0;
            decimal revpar = rooms * days > 0 ? revenue / (rooms * days) : 0;
            int made = site.Bookings.Count(b => b.Created.Date >= from && b.Created.Date <= to);
            int canc = site.Bookings.Count(b => b.Cancelled != null && b.Cancelled.Value.Date >= from && b.Cancelled.Value.Date <= to);
            int earned = site.Points.Where(p => p.When.Date >= from && p.When.Date <= to && p.Kind == "earn").Sum(p => p.Pts);
            int used = -site.Points.Where(p => p.When.Date >= from && p.When.Date <= to && p.Kind == "redeem").Sum(p => p.Pts);
            List<Booking> outs = stays.Where(b => b.In.Date >= from && b.In.Date <= to).ToList();
            double los = outs.Count > 0 ? outs.Average(b => Book.Nights(b.In, b.Out)) : 0;
            StringBuilder sb = new StringBuilder("<div class=\"rp-kpis\">");
            (string Label, string Value, string Note)[] k =
            {
                ("Occupancy", occ.ToString("0.0", Ui.Inv) + "%", Ui.Num(nights) + " of " + Ui.Num(rooms * days) + " room nights"),
                ("Average daily rate", Ui.Money(adr), "per occupied room"),
                ("RevPAR", Ui.Money(revpar), "revenue per available room"),
                ("Room revenue", Ui.Money(revenue), Ui.Day(from, false) + " – " + Ui.Day(to, false)),
                ("Bookings made", Ui.Num(made), canc + " cancelled in the period"),
                ("Average stay", los.ToString("0.0", Ui.Inv) + " nights", outs.Count + " arrivals"),
                ("Member share", (nights > 0 ? memberNights * 100.0 / nights : 0).ToString("0", Ui.Inv) + "%", "of room nights"),
                ("Points", "+" + Ui.Num(earned), Ui.Num(used) + " used")
            };
            foreach ((string l, string v, string n) in k)
            {
                sb.Append("<div class=\"rp-kpi\"><small>" + l + "</small><b>" + v + "</b><span>" + n + "</span></div>");
            }
            sb.Append("</div><section class=\"rp-panel\"><h2>Occupancy by night</h2><div class=\"rp-chart\" role=\"img\" aria-label=\"Occupancy by night\">");
            for (int i = 0; i < days; i++)
            {
                DateTime d = from.AddDays(i);
                double pct = rooms > 0 ? sold[i] * 100.0 / rooms : 0;
                sb.Append("<span class=\"rp-col" + (d > today ? " rp-fut" : string.Empty) + "\" title=\"" + Ui.Attr(d.ToString("ddd d MMM", Ui.Inv) + ": " + pct.ToString("0", Ui.Inv) + "% · " + sold[i] + " rooms · " + Ui.Cash(rev[i])) + "\"><i style=\"height:" + Math.Clamp(pct, 0, 100).ToString("0.0", Ui.Inv) + "%\"></i></span>");
            }
            sb.Append("</div><div class=\"rp-axis\"><span>" + Ui.Day(from, false) + "</span><span>" + (from <= today && to > today ? "Light bars are future nights on the books" : string.Empty) + "</span><span>" + Ui.Day(to, false) + "</span></div></section>");
            sb.Append("<div class=\"rp-grid2\"><section class=\"rp-panel\"><h2>By room type</h2><div class=\"rp-tbl rp-rtbl\">" + Head(new[] { ("Room type", string.Empty), ("Nights", "rp-num"), ("Occupancy", "rp-num"), ("Revenue", "rp-num"), ("ADR", "rp-num"), ("Rooms", "rp-num") }));
            foreach (RoomType t in site.Types.OrderBy(x => x.Order))
            {
                (int n, decimal r) = byType[t.Key];
                int cnt = site.Rooms.Count(x => x.Type == t.Key);
                sb.Append("<div class=\"rp-tr\"><span data-l=\"Room type\" class=\"rp-wide\"><b>" + Ui.Enc(t.Name) + "</b></span><span data-l=\"Nights\" class=\"rp-num\">" + n + "</span><span data-l=\"Occupancy\" class=\"rp-num\">" + (cnt * days > 0 ? (n * 100.0 / (cnt * days)).ToString("0", Ui.Inv) : "0") + "%</span><span data-l=\"Revenue\" class=\"rp-num\">" + Ui.Money(r) + "</span><span data-l=\"ADR\" class=\"rp-num\">" + Ui.Money(n > 0 ? r / n : 0) + "</span><span data-l=\"Rooms\" class=\"rp-num\">" + cnt + "</span></div>");
            }
            sb.Append("</div></section><section class=\"rp-panel\"><h2>By source</h2><div class=\"rp-tbl rp-stbl\">" + Head(new[] { ("Source", string.Empty), ("Stays", "rp-num"), ("Nights", "rp-num"), ("Revenue", "rp-num"), ("Share", "rp-num") }));
            foreach (KeyValuePair<string, (int Nights, decimal Rev, int Count)> s in bySrc.OrderByDescending(x => x.Value.Rev))
            {
                sb.Append("<div class=\"rp-tr\"><span data-l=\"Source\" class=\"rp-wide\"><b>" + Ui.Enc(s.Key) + "</b></span><span data-l=\"Stays\" class=\"rp-num\">" + s.Value.Count + "</span><span data-l=\"Nights\" class=\"rp-num\">" + s.Value.Nights + "</span><span data-l=\"Revenue\" class=\"rp-num\">" + Ui.Money(s.Value.Rev) + "</span><span data-l=\"Share\" class=\"rp-num\">" + (revenue > 0 ? (s.Value.Rev * 100 / revenue).ToString("0", Ui.Inv) : "0") + "%</span></div>");
            }
            sb.Append("</div></section></div>");
            response.SetElementContents("rp-report", sb.ToString());
            response.SetElementContents("rp-rbar", Bar(per, GetDataValue("pfrom"), GetDataValue("pto")));
            return response;
        }
    }
}
