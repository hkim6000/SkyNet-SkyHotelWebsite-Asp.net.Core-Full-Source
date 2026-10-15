using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyHotel.Core;
using SkyHotel.Models;
using SkyNet;

namespace SkyHotel.codes
{
    public class AdminHousekeeping : WebPage
    {
        private const string AreaKey = "housekeeping";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Housekeeping");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--hk-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("hk-menu", Auth.Menu("hk", "AdminHousekeeping", c, site));
            response.SetElementContents("hk-who", Auth.Who("hk", c));
            response.SetElementContents("hk-demo", Auth.DemoPill("hk", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminHousekeepingJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"hk-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"hk-panel\"><p>You don't have access to this area. Ask the general manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminHousekeepingJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminHousekeepingJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("hk-main", NoAccess("Housekeeping"));
                return response;
            }
            response.SetElementContents("hk-main", await MainHtml(c, site));
            response.ExecuteScript("AdminHousekeepingJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"hk-tr hk-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private const int Size = 20;

        private static readonly (string Key, string Name)[] Sts = { ("clean", "Clean"), ("inspected", "Inspected"), ("dirty", "Dirty"), ("ooo", "Out of order") };

        private static readonly (string Key, string Name)[] AllSts = { ("occupied", "Occupied"), ("dirty", "Dirty"), ("clean", "Clean"), ("inspected", "Inspected"), ("ooo", "Out of order") };

        private static string Chips(SiteData site, string cur)
        {
            StringBuilder sb = new StringBuilder("<div class=\"hk-chips\"><button type=\"button\" class=\"hk-chip" + (cur == string.Empty ? " hk-act" : string.Empty) + "\" onclick=\"AdminHousekeepingJs.chip('')\">All<em>" + site.Rooms.Count + "</em></button>");
            foreach ((string k, string n) in AllSts)
            {
                sb.Append("<button type=\"button\" class=\"hk-chip" + (cur == k ? " hk-act" : string.Empty) + "\" onclick=\"AdminHousekeepingJs.chip('" + k + "')\">" + n + "<em>" + site.Rooms.Count(r => r.Status == k) + "</em></button>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static async Task<string> MainHtml(Ctx c, SiteData site)
        {
            StringBuilder sb = new StringBuilder("<div class=\"hk-ptop\"><div><h1>Housekeeping</h1><p>Room status for the team. Check-in turns a room Occupied and check-out turns it Dirty. Front desk can only use Clean or Inspected rooms.</p></div></div>");
            sb.Append("<div id=\"hk-hchips\" style=\"margin-bottom:14px\">" + Chips(site, "dirty") + "</div><div class=\"hk-bar\"><label>Search<input type=\"search\" id=\"hk-q\" list=\"hk-qlist\" placeholder=\"Room or note\" maxlength=\"40\" oninput=\"AdminHousekeepingJs.search()\"></label>" + Ui.DataList("hk-qlist", site.Rooms.Select(r => r.No)) + "<label>Status<select id=\"hk-hst\" onchange=\"AdminHousekeepingJs.list(1)\"><option value=\"\">All statuses</option>" + Ui.Opts(AllSts.Select(x => (x.Key, x.Name)), "dirty") + "</select></label><label>Floor<select id=\"hk-hfloor\" onchange=\"AdminHousekeepingJs.list(1)\"><option value=\"\">All floors</option>" + Ui.Opts(site.Rooms.Select(r => r.Floor).Distinct().OrderBy(f => f).Select(f => (f.ToString(Ui.Inv), "Floor " + f)), string.Empty) + "</select></label></div>");
            sb.Append("<div class=\"hk-tbl hk-hktbl\" id=\"hk-list\"></div><div id=\"hk-pager\"></div>");
            return sb.ToString();
        }

        private static string Rows(SiteData site, Ctx c, Dictionary<string, Staff> staff, string st, string floor, string term, string pg, out string pager)
        {
            DateTime today = DateTime.UtcNow.Date;
            Dictionary<string, Guest> gm = Ui.GuestMap(site);
            IEnumerable<HotelRoom> q = site.Rooms;
            if (st != string.Empty)
            {
                q = q.Where(r => r.Status == st);
            }
            if (floor != string.Empty)
            {
                q = q.Where(r => r.Floor.ToString(Ui.Inv) == floor);
            }
            if (term != string.Empty)
            {
                q = q.Where(r => r.No.Contains(term, StringComparison.OrdinalIgnoreCase) || r.Note.Contains(term, StringComparison.OrdinalIgnoreCase));
            }
            List<HotelRoom> all = q.OrderBy(r => r.No).ToList();
            int page = Ui.PageOf(pg, Size, all.Count);
            bool edit = Access.Can(c.U, AreaKey, 'E');
            StringBuilder sb = new StringBuilder(Head(new[] { ("Room", string.Empty), ("Type", string.Empty), ("Floor", string.Empty), ("Status", string.Empty), ("Today", string.Empty), ("Note", string.Empty), ("Updated", string.Empty), (string.Empty, string.Empty) }));
            if (all.Count == 0)
            {
                sb.Append("<div class=\"hk-tr\"><span class=\"hk-none hk-wide\">No rooms match.</span></div>");
            }
            foreach (HotelRoom r in all.Skip((page - 1) * Size).Take(Size))
            {
                Booking? inh = site.Bookings.FirstOrDefault(b => b.Room == r.No && b.Status == "inhouse");
                Booking? arr = site.Bookings.FirstOrDefault(b => b.Room == r.No && b.Status == "booked" && b.In.Date == today);
                string occ = inh != null ? (inh.Out.Date <= today ? "Departing today" : "In house until " + inh.Out.ToString("d MMM", Ui.Inv)) + "<small>" + Ui.Enc(Ui.GuestName(gm.TryGetValue(inh.Guest, out Guest? g1) ? g1 : null)) + "</small>" : arr != null ? "<b>Arriving today</b><small>" + Ui.Enc(Ui.GuestName(gm.TryGetValue(arr.Guest, out Guest? g2) ? g2 : null)) + "</small>" : "Vacant";
                string name = AllSts.FirstOrDefault(x => x.Key == r.Status).Name ?? r.Status;
                sb.Append("<div class=\"hk-tr\"><span data-l=\"Room\"><b>" + Ui.Enc(r.No) + "</b></span><span data-l=\"Type\">" + Ui.Enc(Ui.TypeName(site, r.Type)) + "</span><span data-l=\"Floor\">" + r.Floor + "</span><span data-l=\"Status\"><span class=\"hk-st hk-st-" + r.Status + "\">" + name + "</span></span><span data-l=\"Today\">" + occ + "</span><span data-l=\"Note\">" + Ui.Enc(r.Note) + "</span><span data-l=\"Updated\">" + Ui.Time(r.Changed) + "<small>" + Ui.Enc(Who(staff, r.By)) + "</small></span><span class=\"hk-hkbtns\">");
                if (r.Status == "occupied")
                {
                    sb.Append("<small class=\"hk-hint\">Guest in room &middot; turns Dirty at check-out</small>");
                }
                else if (edit)
                {
                    foreach ((string k, string n) in Sts)
                    {
                        sb.Append("<button type=\"button\" class=\"" + (k == r.Status ? "hk-on" : string.Empty) + "\" onclick=\"AdminHousekeepingJs.set('" + Ui.Attr(r.No) + "','" + k + "'," + page + ")\">" + n + "</button>");
                    }
                }
                sb.Append("</span></div>");
            }
            pager = Ui.Pager("hk", page, Size, all.Count, "room", "rooms", "AdminHousekeepingJs.list");
            return sb.ToString();
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminHousekeepingJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            string st = Ui.Clip(GetDataValue("hst"), 12);
            response.SetElementContents("hk-list", Rows(site, c, Ui.StaffMap(await Store.LoadStaff(c.Sbx)), st, Ui.Clip(GetDataValue("hfloor"), 3), Ui.Clip(GetDataValue("term"), 40), GetDataValue("pg"), out string pager));
            response.SetElementContents("hk-pager", pager);
            response.SetElementContents("hk-hchips", Chips(site, st));
            return response;
        }

        public async Task<ApiResponse> SetStatus()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminHousekeepingJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "room-status", GetDataValue("room"), "You can view housekeeping but not change room status.");
            }
            string no = Ui.Clip(GetDataValue("room"), 10);
            string st = Sts.Any(x => x.Key == GetDataValue("hstatus")) ? GetDataValue("hstatus") : string.Empty;
            string note = Ui.Clip(GetDataValue("hnote"), 120);
            string err = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                HotelRoom? r = site.Rooms.FirstOrDefault(x => x.No == no);
                if (r == null || st == string.Empty)
                {
                    return response;
                }
                if (r.Status == "occupied" || site.Bookings.Any(b => b.Room == no && b.Status == "inhouse"))
                {
                    err = "Room " + no + " is occupied. It turns Dirty when the guest checks out.";
                }
                else
                {
                    r.Status = st;
                    r.Note = st == "ooo" ? note : st == "clean" || st == "inspected" ? string.Empty : r.Note;
                    r.Changed = DateTime.UtcNow;
                    r.By = c.U.Id;
                    await Store.SaveSite(site, c.Sbx);
                }
            }
            if (err != string.Empty)
            {
                return await Refuse(response, c, "room-status", no, err);
            }
            await Store.Log(c.Sbx, c.U.Id, "room-status", "room " + no + " · " + st, true);
            SiteData fresh = await Store.LoadSite(c.Sbx);
            Chrome(response, c, fresh);
            response.ExecuteScript("AdminHousekeepingJs.list(" + (int.TryParse(GetDataValue("pg"), out int pg) ? pg : 1) + ");");
            return response;
        }
    }
}
