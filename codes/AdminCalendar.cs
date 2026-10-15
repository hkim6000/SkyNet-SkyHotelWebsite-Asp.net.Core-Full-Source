using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyHotel.Core;
using SkyHotel.Models;
using SkyNet;

namespace SkyHotel.codes
{
    public class AdminCalendar : WebPage
    {
        private const string AreaKey = "calendar";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Calendar");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--cl-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("cl-menu", Auth.Menu("cl", "AdminCalendar", c, site));
            response.SetElementContents("cl-who", Auth.Who("cl", c));
            response.SetElementContents("cl-demo", Auth.DemoPill("cl", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminCalendarJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"cl-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"cl-panel\"><p>You don't have access to this area. Ask the general manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminCalendarJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminCalendarJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("cl-main", NoAccess("Calendar"));
                return response;
            }
            response.SetElementContents("cl-main", await MainHtml(c, site));
            response.ExecuteScript("AdminCalendarJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"cl-tr cl-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private const int Size = 20;
        private const int Days = 14;

        private static async Task<string> MainHtml(Ctx c, SiteData site)
        {
            StringBuilder sb = new StringBuilder("<div class=\"cl-ptop\"><div><h1>Calendar</h1><p>Month and week show arrivals and guests staying each night; tap a number for the list. Day shows who sleeps where.</p></div></div>");
            sb.Append("<div class=\"cl-tabs cl-views\"><button type=\"button\" class=\"cl-tab\" data-v=\"month\" onclick=\"AdminCalendarJs.setView('month')\">Month</button><button type=\"button\" class=\"cl-tab\" data-v=\"week\" onclick=\"AdminCalendarJs.setView('week')\">Week</button><button type=\"button\" class=\"cl-tab cl-act\" data-v=\"day\" onclick=\"AdminCalendarJs.setView('day')\">Day</button></div>");
            sb.Append("<div class=\"cl-bar\"><div class=\"cl-calnav\"><label>Date<input type=\"date\" id=\"cl-cstart\" value=\"" + Ui.Ymd(DateTime.UtcNow.Date) + "\" onchange=\"AdminCalendarJs.list(1)\"></label><button type=\"button\" class=\"cl-btn cl-btn-sm cl-btn-ghost\" onclick=\"AdminCalendarJs.shift(-1)\">&larr; Back</button><button type=\"button\" class=\"cl-btn cl-btn-sm cl-btn-ghost\" onclick=\"AdminCalendarJs.shift(0)\">Today</button><button type=\"button\" class=\"cl-btn cl-btn-sm cl-btn-ghost\" onclick=\"AdminCalendarJs.shift(1)\">Next &rarr;</button></div><label>Room type<select id=\"cl-ctype\" onchange=\"AdminCalendarJs.list(1)\"><option value=\"\">All room types</option>" + Ui.Opts(site.Types.Select(t => (t.Key, t.Name)), string.Empty) + "</select></label><label>Search<input type=\"search\" id=\"cl-q\" list=\"cl-qlist\" placeholder=\"Room or guest\" maxlength=\"40\" oninput=\"AdminCalendarJs.search()\"></label>" + Ui.DataList("cl-qlist", site.Rooms.Select(r => r.No)) + "</div>");
            sb.Append("<div id=\"cl-unas\" class=\"cl-donly\"></div><div class=\"cl-legend cl-donly\"><span><i style=\"background:#3D6FB0\"></i>Booked</span><span><i style=\"background:#2F7A4F\"></i>In house</span><span><i style=\"background:#8C8A85\"></i>Checked out</span><span><i style=\"background:#61656C\"></i>Blocked</span></div><div class=\"cl-calwrap\" id=\"cl-calwrap\"><div class=\"cl-cal\" id=\"cl-list\"></div></div><div id=\"cl-pager\" class=\"cl-donly\"></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminCalendarJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Dictionary<string, Guest> gm = Ui.GuestMap(site);
            DateTime start = Ui.ParseDay(GetDataValue("cstart")) ?? DateTime.UtcNow.Date;
            if (GetDataValue("cview") is "month" or "week")
            {
                response.SetElementContents("cl-list", GetDataValue("cview") == "month" ? MonthHtml(site, gm, start) : WeekHtml(site, gm, start));
                return response;
            }
            DateTime end = start.AddDays(Days);
            DateTime today = DateTime.UtcNow.Date;
            string type = Ui.Clip(GetDataValue("ctype"), 30);
            string term = Ui.Clip(GetDataValue("term"), 40);
            List<Booking> win = site.Bookings.Where(b => (Book.Holds(b) || b.Status == "out") && b.In < end && b.Out > start && (type == string.Empty || b.Type == type)).ToList();
            IEnumerable<HotelRoom> rq = site.Rooms.Where(r => type == string.Empty || r.Type == type);
            if (term != string.Empty)
            {
                HashSet<string> hit = new HashSet<string>(win.Where(b => b.Room != string.Empty && gm.TryGetValue(b.Guest, out Guest? g) && Ui.GuestName(g).Contains(term, StringComparison.OrdinalIgnoreCase)).Select(b => b.Room));
                rq = rq.Where(r => r.No.Contains(term, StringComparison.OrdinalIgnoreCase) || hit.Contains(r.No));
            }
            List<HotelRoom> rooms = rq.OrderBy(r => r.No).ToList();
            int page = Ui.PageOf(GetDataValue("pg"), Size, rooms.Count);
            StringBuilder sb = new StringBuilder("<div class=\"cl-crow cl-chead\"><span class=\"cl-clabel\">Room</span>");
            for (int i = 0; i < Days; i++)
            {
                DateTime d = start.AddDays(i);
                sb.Append("<span class=\"" + (d == today ? "cl-ctoday" : string.Empty) + "\">" + d.ToString("ddd", Ui.Inv) + "<b>" + d.Day + "</b></span>");
            }
            sb.Append("</div>");
            if (rooms.Count == 0)
            {
                sb.Append("<p class=\"cl-none\" style=\"padding:14px\">No rooms match.</p>");
            }
            foreach (HotelRoom r in rooms.Skip((page - 1) * Size).Take(Size))
            {
                sb.Append("<div class=\"cl-crow\"><span class=\"cl-clabel\"><b>" + Ui.Enc(r.No) + "</b><small>" + Ui.Enc(Ui.TypeName(site, r.Type)) + "</small></span>");
                for (int i = 0; i < Days; i++)
                {
                    DateTime d = start.AddDays(i);
                    bool wk = d.DayOfWeek == DayOfWeek.Friday || d.DayOfWeek == DayOfWeek.Saturday;
                    sb.Append("<span class=\"cl-ccell" + (d == today ? " cl-ctoday" : wk ? " cl-wk" : string.Empty) + "\" style=\"grid-column:" + (i + 2) + "\"></span>");
                }
                foreach (RoomBlock k in site.Blocks.Where(x => x.Room == r.No && x.From.Date < end && x.To.Date >= start))
                {
                    int s0 = Math.Max(0, (int)(k.From.Date - start).TotalDays);
                    int s1 = Math.Min(Days, (int)(k.To.Date - start).TotalDays + 1);
                    sb.Append("<span class=\"cl-bar2 cl-b-block\" style=\"grid-column:" + (s0 + 2) + " / " + (s1 + 2) + "\" title=\"" + Ui.Attr(k.Reason) + "\">" + Ui.Enc(k.Reason) + "</span>");
                }
                foreach (Booking b in win.Where(x => x.Room == r.No))
                {
                    int s0 = (int)(b.In.Date - start).TotalDays;
                    int s1 = (int)(b.Out.Date - start).TotalDays;
                    bool cl = s0 < 0;
                    bool cr = s1 > Days;
                    s0 = Math.Max(0, s0);
                    s1 = Math.Min(Days, s1);
                    if (s1 <= s0)
                    {
                        continue;
                    }
                    Guest? g = gm.TryGetValue(b.Guest, out Guest? x) ? x : null;
                    sb.Append("<button type=\"button\" class=\"cl-bar2 cl-b-" + b.Status + (cl ? " cl-cut-l" : string.Empty) + (cr ? " cl-cut-r" : string.Empty) + "\" style=\"grid-column:" + (s0 + 2) + " / " + (s1 + 2) + "\" onclick=\"AdminCalendarJs.open('" + b.Id + "')\" title=\"" + Ui.Attr(Ui.GuestName(g) + " · " + b.Code) + "\">" + Ui.Enc(g?.Last ?? "Guest") + "</button>");
                }
                sb.Append("</div>");
            }
            List<Booking> un = win.Where(b => b.Room == string.Empty && Book.Holds(b)).OrderBy(b => b.In).ToList();
            StringBuilder ub = new StringBuilder();
            if (un.Count > 0)
            {
                ub.Append("<section class=\"cl-panel cl-unas\"><div class=\"cl-ph\"><h2>No room yet &middot; " + un.Count + "</h2><span class=\"cl-hint\">Arriving in this window</span></div>");
                foreach (Booking b in un.Take(8))
                {
                    Guest? g = gm.TryGetValue(b.Guest, out Guest? x) ? x : null;
                    ub.Append("<div class=\"cl-row\"><div><b>" + Ui.Enc(Ui.GuestName(g)) + " &middot; " + Ui.Enc(Ui.TypeName(site, b.Type)) + "</b><small>" + Ui.Enc(b.Code) + " &middot; " + b.In.ToString("ddd d MMM", Ui.Inv) + " &rarr; " + b.Out.ToString("ddd d MMM", Ui.Inv) + "</small></div><button type=\"button\" class=\"cl-btn cl-btn-sm cl-btn-ghost\" onclick=\"AdminCalendarJs.open('" + b.Id + "')\">" + (Access.Can(c.U, AreaKey, 'E') ? "Give a room" : "View") + "</button></div>");
                }
                if (un.Count > 8)
                {
                    ub.Append("<p class=\"cl-hint\">and " + (un.Count - 8) + " more. Narrow by room type to see them.</p>");
                }
                ub.Append("</section>");
            }
            response.SetElementContents("cl-list", sb.ToString());
            response.SetElementContents("cl-unas", ub.ToString());
            response.SetElementContents("cl-pager", Ui.Pager("cl", page, Size, rooms.Count, "room", "rooms", "AdminCalendarJs.list"));
            return response;
        }

        public async Task<ApiResponse> Open()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminCalendarJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Booking? b = site.Bookings.FirstOrDefault(x => x.Id == GetDataValue("bid"));
            if (b == null)
            {
                return response;
            }
            Guest? g = site.Guests.FirstOrDefault(x => x.Id == b.Guest);
            bool edit = Access.Can(c.U, AreaKey, 'E') && Book.Holds(b);
            StringBuilder sb = new StringBuilder("<h2>" + Ui.Enc(Ui.GuestName(g)) + " " + Ui.StayBadge("cl", b) + "</h2><dl class=\"cl-dl\"><dt>Booking</dt><dd class=\"cl-code\">" + Ui.Enc(b.Code) + "</dd><dt>Stay</dt><dd>" + Ui.Day(b.In) + " &rarr; " + Ui.Day(b.Out) + "</dd><dt>Room</dt><dd>" + Ui.Enc(Ui.TypeName(site, b.Type)) + (b.Room != string.Empty ? " &middot; room " + Ui.Enc(b.Room) : " &middot; not assigned") + "</dd><dt>Guests</dt><dd>" + b.Adults + " adults" + (b.Children > 0 ? ", " + b.Children + " children" : string.Empty) + "</dd>" + (b.Requests != string.Empty ? "<dt>Requests</dt><dd>" + Ui.Enc(b.Requests) + "</dd>" : string.Empty) + "</dl>");
            if (edit)
            {
                sb.Append("<h3>" + (b.Room == string.Empty ? "Give a room" : "Move to another room") + "</h3><div class=\"cl-rooms\">");
                foreach (HotelRoom r in site.Rooms.Where(r => r.Type == b.Type).OrderBy(r => r.No))
                {
                    bool free = r.No == b.Room || Book.RoomFree(site, r.No, b.In, b.Out, b.Id);
                    sb.Append("<label class=\"cl-rpick\"" + (free ? string.Empty : " style=\"opacity:0.45\"") + "><input type=\"radio\" name=\"cl-room\" value=\"" + Ui.Attr(r.No) + "\"" + (free ? string.Empty : " disabled") + (r.No == b.Room ? " checked" : string.Empty) + "><b>" + Ui.Enc(r.No) + "</b><small class=\"cl-hint\">" + (free ? "Free" : "Taken") + " &middot; " + Ui.Enc(r.Status) + "</small></label>");
                }
                sb.Append("</div>");
            }
            string bday = Ui.ParseDay(GetDataValue("bday")) is DateTime bd ? Ui.Ymd(bd) : string.Empty;
            string bkind = GetDataValue("bkind") is "arr" or "dep" or "stay" ? GetDataValue("bkind") : string.Empty;
            sb.Append("<div class=\"cl-mfoot\">" + (bday != string.Empty && bkind != string.Empty ? "<button type=\"button\" class=\"cl-btn cl-btn-ghost\" onclick=\"AdminCalendarJs.dayList('" + bday + "','" + bkind + "')\">&larr; Back to list</button><span class=\"cl-grow\"></span>" : string.Empty) + "<button type=\"button\" class=\"cl-btn cl-btn-ghost\" onclick=\"AdminCalendarJs.closeModal()\">Close</button>" + (edit ? "<button type=\"button\" class=\"cl-btn cl-btn-accent\" onclick=\"AdminCalendarJs.assign('" + b.Id + "')\">Save room</button>" : string.Empty) + "</div>");
            response.SetElementContents("cl-mbody", sb.ToString());
            response.ExecuteScript("AdminCalendarJs.openModal();");
            return response;
        }

        private List<Booking> Stays(SiteData site, Dictionary<string, Guest> gm, DateTime from, DateTime to)
        {
            string type = Ui.Clip(GetDataValue("ctype"), 30);
            string term = Ui.Clip(GetDataValue("term"), 40);
            return site.Bookings.Where(b => (b.Status == "booked" || b.Status == "inhouse" || b.Status == "out") && b.In.Date <= to && b.Out.Date >= from && (type == string.Empty || b.Type == type) && (term == string.Empty || b.Room.Contains(term, StringComparison.OrdinalIgnoreCase) || b.Code.Contains(term, StringComparison.OrdinalIgnoreCase) || (gm.TryGetValue(b.Guest, out Guest? g) && Ui.GuestName(g).Contains(term, StringComparison.OrdinalIgnoreCase)))).ToList();
        }

        private int RoomCount(SiteData site)
        {
            string type = Ui.Clip(GetDataValue("ctype"), 30);
            return Math.Max(1, site.Rooms.Count(r => type == string.Empty || r.Type == type));
        }

        private static IEnumerable<Booking> Of(List<Booking> list, DateTime d, string kind)
        {
            return kind switch
            {
                "arr" => list.Where(b => b.In.Date == d),
                "dep" => list.Where(b => b.Out.Date == d),
                _ => list.Where(b => b.In.Date <= d && b.Out.Date > d)
            };
        }

        private static string Counts(List<Booking> list, DateTime d, int rooms)
        {
            int a = Of(list, d, "arr").Count();
            int st = Of(list, d, "stay").Count();
            int dp = Of(list, d, "dep").Count();
            string ymd = Ui.Ymd(d);
            int pct = Math.Min(100, (int)Math.Round(st * 100.0 / rooms));
            return "<button type=\"button\" class=\"cl-cq cl-cq-arr\" onclick=\"AdminCalendarJs.dayList('" + ymd + "','arr')\"" + (a == 0 ? " disabled" : string.Empty) + " title=\"Arrivals\"><b>" + a + "</b><span>" + (a == 1 ? "arrival" : "arrivals") + "</span></button><button type=\"button\" class=\"cl-cq cl-cq-stay\" onclick=\"AdminCalendarJs.dayList('" + ymd + "','stay')\"" + (st == 0 ? " disabled" : string.Empty) + " title=\"Staying\"><b>" + st + "</b><span>staying</span></button><button type=\"button\" class=\"cl-cq cl-cq-dep\" onclick=\"AdminCalendarJs.dayList('" + ymd + "','dep')\"" + (dp == 0 ? " disabled" : string.Empty) + " title=\"Departures\"><b>" + dp + "</b><span>" + (dp == 1 ? "departure" : "departures") + "</span></button><span class=\"cl-occ\">" + st + "/" + rooms + " &middot; " + pct + "%<i style=\"--o:" + pct + "%\"></i></span>";
        }

        private static readonly string[] WeekDays = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

        private string MonthHtml(SiteData site, Dictionary<string, Guest> gm, DateTime day)
        {
            DateTime first = new DateTime(day.Year, day.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime start = first.AddDays(-(int)first.DayOfWeek);
            int weeks = (int)Math.Ceiling(((first.AddMonths(1) - start).TotalDays) / 7.0);
            DateTime end = start.AddDays(weeks * 7 - 1);
            List<Booking> list = Stays(site, gm, start, end.AddDays(1));
            int rooms = RoomCount(site);
            DateTime today = DateTime.UtcNow.Date;
            StringBuilder sb = new StringBuilder("<h2 class=\"cl-vtitle\">" + first.ToString("MMMM yyyy", Ui.Inv) + "</h2><div class=\"cl-legend\"><span><i style=\"background:#E3ECF7\"></i>Arrivals</span><span><i style=\"background:#E2F1E8\"></i>Staying that night</span><span><i style=\"background:#ECEAE6\"></i>Departures</span></div><div class=\"cl-mhead\">");
            foreach (string w in WeekDays)
            {
                sb.Append("<span>" + w + "</span>");
            }
            sb.Append("</div><div class=\"cl-mgrid\">");
            for (DateTime d = start; d <= end; d = d.AddDays(1))
            {
                sb.Append("<div class=\"cl-mc" + (d.Month != first.Month ? " cl-mother" : string.Empty) + (d == today ? " cl-ctoday" : string.Empty) + "\"><span class=\"cl-mday\">" + d.Day + "</span>" + Counts(list, d, rooms) + "</div>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string NameChip(SiteData site, Booking b, Guest? g, string ymd)
        {
            return "<button type=\"button\" class=\"cl-wn\" onclick=\"AdminCalendarJs.open('" + b.Id + "','" + ymd + "','arr')\"><b>" + Ui.Enc(Ui.GuestName(g)) + "</b><small>" + (b.Status == "inhouse" ? "Checked in" : b.Status == "out" ? "Checked out" : "Booked") + " &middot; " + Ui.Enc(Ui.TypeName(site, b.Type)) + (b.Arrival != string.Empty ? " &middot; ~" + Ui.Enc(b.Arrival) : string.Empty) + "</small></button>";
        }

        private string WeekHtml(SiteData site, Dictionary<string, Guest> gm, DateTime day)
        {
            DateTime start = day.AddDays(-(int)day.DayOfWeek);
            DateTime end = start.AddDays(6);
            List<Booking> list = Stays(site, gm, start, end.AddDays(1));
            int rooms = RoomCount(site);
            DateTime today = DateTime.UtcNow.Date;
            StringBuilder sb = new StringBuilder("<h2 class=\"cl-vtitle\">" + start.ToString("d MMM", Ui.Inv) + " &ndash; " + end.ToString("d MMM yyyy", Ui.Inv) + "</h2><div class=\"cl-wgrid\">");
            for (DateTime d = start; d <= end; d = d.AddDays(1))
            {
                string ymd = Ui.Ymd(d);
                List<Booking> arr = Sorted(Of(list, d, "arr"), gm);
                sb.Append("<div class=\"cl-wd" + (d == today ? " cl-ctoday" : string.Empty) + "\"><h3>" + d.ToString("ddd", Ui.Inv) + "<b>" + d.ToString("d MMM", Ui.Inv) + "</b></h3>" + Counts(list, d, rooms));
                if (arr.Count > 0 && arr.Count <= 5)
                {
                    sb.Append("<div class=\"cl-wnames\">");
                    foreach (Booking b in arr)
                    {
                        sb.Append(NameChip(site, b, gm.TryGetValue(b.Guest, out Guest? g) ? g : null, ymd));
                    }
                    sb.Append("</div>");
                }
                else if (arr.Count > 5)
                {
                    sb.Append("<button type=\"button\" class=\"cl-wmore\" onclick=\"AdminCalendarJs.dayList('" + ymd + "','arr')\">See all " + arr.Count + " arrivals &rarr;</button>");
                }
                sb.Append("</div>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static List<Booking> Sorted(IEnumerable<Booking> list, Dictionary<string, Guest> gm)
        {
            return list.OrderBy(b => b.In.Date).ThenBy(b => b.Arrival == string.Empty ? "99:99" : b.Arrival).ThenBy(b => gm.TryGetValue(b.Guest, out Guest? g) ? g.Last : string.Empty).ToList();
        }

        public async Task<ApiResponse> DayList()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminCalendarJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Dictionary<string, Guest> gm = Ui.GuestMap(site);
            DateTime d = Ui.ParseDay(GetDataValue("cday")) ?? DateTime.UtcNow.Date;
            string kind = GetDataValue("ckind") is "arr" or "dep" ? GetDataValue("ckind") : "stay";
            List<Booking> list = Sorted(Of(Stays(site, gm, d, d.AddDays(1)), d, kind), gm);
            string ymd = Ui.Ymd(d);
            StringBuilder sb = new StringBuilder("<h2>" + (kind == "arr" ? "Arrivals" : kind == "dep" ? "Departures" : "Staying") + " &middot; " + d.ToString("ddd d MMM yyyy", Ui.Inv) + " <span class=\"cl-st\">" + list.Count + "</span></h2><p class=\"cl-hint\" style=\"margin-bottom:12px\">Sorted by arrival date, then expected arrival time.</p><div class=\"cl-dlist\">");
            foreach (Booking b in list)
            {
                Guest? g = gm.TryGetValue(b.Guest, out Guest? x) ? x : null;
                sb.Append("<button type=\"button\" class=\"cl-dli\" onclick=\"AdminCalendarJs.open('" + b.Id + "','" + ymd + "','" + kind + "')\"><span>" + b.In.ToString("d MMM", Ui.Inv) + "<small>" + (b.Arrival != string.Empty ? "~" + Ui.Enc(b.Arrival) : "time n/a") + "</small></span><span><b>" + Ui.Enc(Ui.GuestName(g)) + (g != null && g.Member ? " " + Ui.GradePill("cl", g) : string.Empty) + (g != null && g.Vip ? "<span class=\"cl-vip\">VIP</span>" : string.Empty) + "</b><small>" + Ui.Enc(Ui.TypeName(site, b.Type)) + (b.Room != string.Empty ? " &middot; room " + Ui.Enc(b.Room) : " &middot; no room yet") + " &middot; " + Book.Nights(b.In, b.Out) + (Book.Nights(b.In, b.Out) == 1 ? " night" : " nights") + " to " + b.Out.ToString("d MMM", Ui.Inv) + " &middot; " + Ui.Enc(b.Code) + "</small></span><span>" + Ui.StayBadge("cl", b) + "</span></button>");
            }
            if (list.Count == 0)
            {
                sb.Append("<p class=\"cl-none\">Nobody on this day.</p>");
            }
            sb.Append("</div><div class=\"cl-mfoot\"><button type=\"button\" class=\"cl-btn\" onclick=\"AdminCalendarJs.closeModal()\">Close</button></div>");
            response.SetElementContents("cl-mbody", sb.ToString());
            response.ExecuteScript("AdminCalendarJs.openModal();");
            return response;
        }

        public async Task<ApiResponse> Assign()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminCalendarJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "assign", GetDataValue("bid"), "You can view the calendar but not move guests.");
            }
            string room = Ui.Clip(GetDataValue("room"), 10);
            string err = string.Empty;
            string log = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Booking? b = site.Bookings.FirstOrDefault(x => x.Id == GetDataValue("bid") && (x.Status == "booked" || x.Status == "inhouse"));
                HotelRoom? r = site.Rooms.FirstOrDefault(x => x.No == room);
                if (b == null || r == null || r.Type != b.Type)
                {
                    err = "Choose a room of the booked type.";
                }
                else if (!Book.RoomFree(site, r.No, b.In, b.Out, b.Id))
                {
                    err = "Room " + r.No + " is taken on some of those nights.";
                }
                else
                {
                    log = b.Code + " · " + (b.Room == string.Empty ? "room " + r.No : b.Room + " → " + r.No);
                    if (b.Status == "inhouse" && b.Room != r.No)
                    {
                        HotelRoom? old = site.Rooms.FirstOrDefault(x => x.No == b.Room);
                        if (old != null)
                        {
                            old.Status = "dirty";
                            old.Changed = DateTime.UtcNow;
                            old.By = c.U.Id;
                        }
                        r.Status = "occupied";
                        r.Changed = DateTime.UtcNow;
                        r.By = c.U.Id;
                    }
                    b.Room = r.No;
                    await Store.SaveSite(site, c.Sbx);
                }
            }
            if (err != string.Empty)
            {
                return await Refuse(response, c, "assign", GetDataValue("bid"), err);
            }
            await Store.Log(c.Sbx, c.U.Id, "assign", log, true);
            response.ExecuteScript("AdminCalendarJs.closeModal();AdminCalendarJs.list(1);AdminCalendarJs.toast('" + Ui.Js("Saved: " + log) + "');");
            return response;
        }
    }
}
