using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyHotel.Core;
using SkyHotel.Models;
using SkyNet;

namespace SkyHotel.codes
{
    public class Admin : WebPage
    {
        private const string AreaKey = "";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Dashboard");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--db-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("db-menu", Auth.Menu("db", "Admin", c, site));
            response.SetElementContents("db-who", Auth.Who("db", c));
            response.SetElementContents("db-demo", Auth.DemoPill("db", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"db-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"db-panel\"><p>You don't have access to this area. Ask the general manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("db-main", NoAccess("Dashboard"));
                return response;
            }
            response.SetElementContents("db-main", await MainHtml(c, site));
            response.ExecuteScript("AdminJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"db-tr db-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static async Task<string> MainHtml(Ctx c, SiteData site)
        {
            Staff u = c.U;
            DateTime today = DateTime.UtcNow.Date;
            Dictionary<string, Guest> gm = Ui.GuestMap(site);
            Dictionary<string, Staff> staff = Ui.StaffMap(await Store.LoadStaff(c.Sbx));
            List<Booking> arr = site.Bookings.Where(b => b.Status == "booked" && b.In.Date == today).OrderBy(b => b.Arrival == string.Empty).ThenBy(b => b.Arrival).ToList();
            List<Booking> dep = site.Bookings.Where(b => b.Status == "inhouse" && b.Out.Date <= today).ToList();
            int inhouse = site.Bookings.Count(b => b.Status == "inhouse");
            int sellable = site.Rooms.Count - site.Blocks.Where(x => today >= x.From.Date && today <= x.To.Date).Select(x => x.Room).Distinct().Count();
            int tonight = site.Bookings.Count(b => Book.Holds(b) && b.In.Date <= today && b.Out.Date > today);
            int occ = sellable > 0 ? (int)Math.Round(tonight * 100.0 / sellable) : 0;
            DateTime m0 = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            decimal rev = 0;
            foreach (Booking b in site.Bookings.Where(b => b.Status is "booked" or "inhouse" or "out" && b.In < m0.AddMonths(1) && b.Out > m0))
            {
                for (DateTime d = b.In.Date; d < b.Out.Date; d = d.AddDays(1))
                {
                    if (d >= m0 && d < m0.AddMonths(1) && d <= today)
                    {
                        rev += Book.PerNight(b);
                    }
                }
            }
            StringBuilder sb = new StringBuilder("<div class=\"db-ptop\"><div><h1>Good " + (DateTime.UtcNow.Hour < 12 ? "morning" : DateTime.UtcNow.Hour < 18 ? "afternoon" : "evening") + ", " + Ui.Enc(u.Name.Split(' ')[0]) + "</h1><p>" + Ui.Enc(u.Title) + " &middot; " + Ui.Day(today) + "</p></div>");
            if (Access.Can(u, "bookings", 'V'))
            {
                sb.Append("<div class=\"db-acts\"><a class=\"db-btn db-btn-ghost\" href=\"Reservation\" target=\"_blank\" rel=\"noopener\">Booking page</a></div>");
            }
            sb.Append("</div><div class=\"db-stats\">");
            sb.Append("<a class=\"db-stat" + (arr.Count > 0 ? " db-hot" : string.Empty) + "\" href=\"AdminFrontDesk\"><b>" + arr.Count + "</b><span>Arrivals today</span></a>");
            sb.Append("<a class=\"db-stat\" href=\"AdminFrontDesk?tab=out\"><b>" + dep.Count + "</b><span>Departures today</span></a>");
            sb.Append("<a class=\"db-stat\" href=\"AdminFrontDesk?tab=in\"><b>" + inhouse + "</b><span>Rooms in house</span></a>");
            sb.Append("<div class=\"db-stat\"><b>" + occ + "%</b><span>Occupancy tonight &middot; " + tonight + " of " + sellable + "</span><div class=\"db-occ\"><i style=\"width:" + Math.Clamp(occ, 0, 100) + "%\"></i></div></div>");
            if (Access.Can(u, "reports", 'V'))
            {
                int joined = site.Guests.Count(g => g.Member && g.Joined >= m0);
                int earned = site.Points.Where(p => p.When >= m0 && p.Kind == "earn").Sum(p => p.Pts);
                int nights = site.Bookings.Where(b => b.Status is "booked" or "inhouse" or "out").Sum(b => Enumerable.Range(0, Math.Max(0, Book.Nights(b.In, b.Out))).Count(i => b.In.Date.AddDays(i) >= m0 && b.In.Date.AddDays(i) <= today));
                sb.Append("<a class=\"db-stat\" href=\"AdminReports\"><b>" + Ui.Money(rev) + "</b><span>Room revenue this month</span></a>");
                sb.Append("<a class=\"db-stat\" href=\"AdminReports\"><b>" + Ui.Money(nights > 0 ? rev / nights : 0) + "</b><span>Average daily rate</span></a>");
                sb.Append("<a class=\"db-stat\" href=\"AdminMembers\"><b>" + joined + "</b><span>New members this month</span></a>");
                sb.Append("<a class=\"db-stat\" href=\"AdminMembers?tab=points\"><b>" + Ui.Num(earned) + "</b><span>Points earned this month</span></a>");
            }
            sb.Append("</div><div class=\"db-grid2\"><div>");
            if (Access.Can(u, "frontdesk", 'V'))
            {
                sb.Append("<section class=\"db-panel\"><div class=\"db-ph\"><h2>Arriving today</h2><a href=\"AdminFrontDesk\">Check-in</a></div>");
                if (arr.Count == 0)
                {
                    sb.Append("<p class=\"db-none\">No more arrivals today.</p>");
                }
                foreach (Booking b in arr.Take(7))
                {
                    Guest? g = gm.TryGetValue(b.Guest, out Guest? x) ? x : null;
                    sb.Append("<div class=\"db-row\"><div><b>" + Ui.Enc(Ui.GuestName(g)) + (g != null && g.Vip ? "<span class=\"db-vip\">VIP</span>" : string.Empty) + "</b><small>" + Ui.Enc(Ui.TypeName(site, b.Type)) + " &middot; " + (b.Room != string.Empty ? "room " + Ui.Enc(b.Room) : "no room yet") + (b.Arrival != string.Empty ? " &middot; around " + Ui.Enc(b.Arrival) : string.Empty) + "</small></div>" + (g != null && g.Member ? Ui.GradePill("db", g) : string.Empty) + "</div>");
                }
                sb.Append("</section>");
            }
            if (Access.Can(u, "housekeeping", 'V'))
            {
                List<HotelRoom> dirty = site.Rooms.Where(r => r.Status == "dirty" || r.Status == "ooo").OrderBy(r => r.No).ToList();
                sb.Append("<section class=\"db-panel\"><div class=\"db-ph\"><h2>Rooms not ready</h2><a href=\"AdminHousekeeping\">Housekeeping</a></div>");
                if (dirty.Count == 0)
                {
                    sb.Append("<p class=\"db-none\">Every room is ready.</p>");
                }
                foreach (HotelRoom r in dirty.Take(8))
                {
                    sb.Append("<div class=\"db-row\"><div><b>Room " + Ui.Enc(r.No) + "</b><small>" + Ui.Enc(Ui.TypeName(site, r.Type)) + (r.Note != string.Empty ? " &middot; " + Ui.Enc(r.Note) : string.Empty) + "</small></div><span class=\"db-st db-st-" + r.Status + "\">" + (r.Status == "ooo" ? "Out of order" : "Dirty") + "</span></div>");
                }
                sb.Append("</section>");
            }
            if (Access.Can(u, "reviews", 'V'))
            {
                List<Review> pend = site.Reviews.Where(r => r.Status == "pending").ToList();
                sb.Append("<section class=\"db-panel\"><div class=\"db-ph\"><h2>Reviews waiting</h2><a href=\"AdminReviews\">All reviews</a></div>");
                if (pend.Count == 0)
                {
                    sb.Append("<p class=\"db-none\">Nothing waiting.</p>");
                }
                foreach (Review r in pend.Take(5))
                {
                    sb.Append("<div class=\"db-row\"><div><b>" + new string('★', r.Rating) + " " + Ui.Enc(r.Title) + "</b><small>" + Ui.Enc(r.Name) + " &middot; " + Ui.Time(r.When) + "</small></div></div>");
                }
                sb.Append("</section>");
            }
            sb.Append("</div><div>");
            if (Access.Can(u, "frontdesk", 'V'))
            {
                sb.Append("<section class=\"db-panel\"><div class=\"db-ph\"><h2>Leaving today</h2><a href=\"AdminFrontDesk?tab=out\">Check-out</a></div>");
                if (dep.Count == 0)
                {
                    sb.Append("<p class=\"db-none\">Everyone due out has checked out.</p>");
                }
                foreach (Booking b in dep.Take(7))
                {
                    Guest? g = gm.TryGetValue(b.Guest, out Guest? x) ? x : null;
                    decimal bal = Book.Balance(site, b);
                    sb.Append("<div class=\"db-row\"><div><b>" + Ui.Enc(Ui.GuestName(g)) + "</b><small>Room " + Ui.Enc(b.Room) + " &middot; " + (bal > 0 ? "balance " + Ui.Cash(bal) : "nothing due") + "</small></div>" + (g != null && g.Member ? Ui.GradePill("db", g) : string.Empty) + "</div>");
                }
                sb.Append("</section>");
            }
            sb.Append("<section class=\"db-panel\"><div class=\"db-ph\"><h2>Recent activity</h2><a href=\"AdminActivity\">All</a></div>");
            List<Activity> acts = (await Store.LoadActivity(c.Sbx)).Where(a => Access.Can(u, "activity", 'V') || a.User == u.Id).Take(8).ToList();
            if (acts.Count == 0)
            {
                sb.Append("<p class=\"db-none\">No activity yet.</p>");
            }
            foreach (Activity a in acts)
            {
                sb.Append("<div class=\"db-row\"><div><b>" + Ui.Enc(Who(staff, a.User)) + " &middot; " + Ui.Enc(a.Action) + (a.Ok ? string.Empty : " (refused)") + "</b><small>" + Ui.Enc(a.Target) + " &middot; " + Ui.Time(a.When) + "</small></div></div>");
            }
            sb.Append("</section><section class=\"db-panel\"><h2>Quick links</h2><div class=\"db-quick\">");
            foreach (Area a in Access.Areas.Where(a => Access.Can(u, a.Key, 'V')).Take(8))
            {
                sb.Append("<a class=\"db-btn db-btn-sm db-btn-ghost\" href=\"" + a.Page + "\">" + Ui.Enc(a.Name) + "</a>");
            }
            sb.Append("</div></section></div></div>");
            return sb.ToString();
        }
    }
}
