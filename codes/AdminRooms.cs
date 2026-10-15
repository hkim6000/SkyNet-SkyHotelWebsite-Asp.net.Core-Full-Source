using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyHotel.Core;
using SkyHotel.Models;
using SkyNet;

namespace SkyHotel.codes
{
    public class AdminRooms : WebPage
    {
        private const string AreaKey = "rates";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Rooms & Rates");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--rr-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("rr-menu", Auth.Menu("rr", "AdminRooms", c, site));
            response.SetElementContents("rr-who", Auth.Who("rr", c));
            response.SetElementContents("rr-demo", Auth.DemoPill("rr", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminRoomsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"rr-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"rr-panel\"><p>You don't have access to this area. Ask the general manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminRoomsJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminRoomsJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("rr-main", NoAccess("Rooms & Rates"));
                return response;
            }
            response.SetElementContents("rr-main", await MainHtml(c, site));
            response.ExecuteScript("AdminRoomsJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"rr-tr rr-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private const int Size = 20;

        private static async Task<string> MainHtml(Ctx c, SiteData site)
        {
            StringBuilder sb = new StringBuilder("<div class=\"rr-ptop\"><div><h1>Rooms &amp; Rates</h1><p>Room types and prices, the rooms in each type, seasonal price changes and blocked dates.</p></div></div>");
            sb.Append("<div class=\"rr-tabs\"><button type=\"button\" class=\"rr-tab rr-act\" data-t=\"types\" onclick=\"AdminRoomsJs.setTab('types')\">Room types<em>" + site.Types.Count + "</em></button><button type=\"button\" class=\"rr-tab\" data-t=\"rooms\" onclick=\"AdminRoomsJs.setTab('rooms')\">Rooms<em>" + site.Rooms.Count + "</em></button><button type=\"button\" class=\"rr-tab\" data-t=\"seasons\" onclick=\"AdminRoomsJs.setTab('seasons')\">Seasons<em>" + site.Seasons.Count + "</em></button><button type=\"button\" class=\"rr-tab\" data-t=\"blocks\" onclick=\"AdminRoomsJs.setTab('blocks')\">Blocked dates<em>" + site.Blocks.Count + "</em></button></div>");
            sb.Append("<div class=\"rr-bar\"><label>Search<input type=\"search\" id=\"rr-q\" placeholder=\"Name or room\" maxlength=\"40\" oninput=\"AdminRoomsJs.search()\"></label><label>Room type<select id=\"rr-rt\" onchange=\"AdminRoomsJs.list(1)\"><option value=\"\">All room types</option>" + Ui.Opts(site.Types.Select(t => (t.Key, t.Name)), string.Empty) + "</select></label><span class=\"rr-grow\"></span><span id=\"rr-addbtn\"></span></div>");
            sb.Append("<div class=\"rr-tbl\" id=\"rr-list\"></div><div id=\"rr-pager\"></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminRoomsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            string tab = GetDataValue("rtab") is "rooms" or "seasons" or "blocks" ? GetDataValue("rtab") : "types";
            string term = Ui.Clip(GetDataValue("term"), 40);
            string rt = Ui.Clip(GetDataValue("rtype"), 30);
            bool edit = Access.Can(c.U, AreaKey, 'E');
            bool block = Access.Can(c.U, AreaKey, 'X');
            DateTime today = DateTime.UtcNow.Date;
            StringBuilder sb = new StringBuilder();
            int total;
            int page;
            string cls;
            string add = string.Empty;
            if (tab == "types")
            {
                cls = "rr-tytbl";
                List<RoomType> all = site.Types.Where(t => (rt == string.Empty || t.Key == rt) && (term == string.Empty || t.Name.Contains(term, StringComparison.OrdinalIgnoreCase))).OrderBy(t => t.Order).ToList();
                total = all.Count;
                page = Ui.PageOf(GetDataValue("pg"), Size, total);
                sb.Append(Head(new[] { ("Room type", string.Empty), ("Size", string.Empty), ("Sleeps", string.Empty), ("Weekday", "rr-num"), ("Weekend", "rr-num"), ("Rooms", string.Empty), ("Status", string.Empty), (string.Empty, string.Empty) }));
                foreach (RoomType t in all.Skip((page - 1) * Size).Take(Size))
                {
                    sb.Append("<div class=\"rr-tr\"><span data-l=\"Room type\" class=\"rr-wide\"><b>" + Ui.Enc(t.Name) + "</b><small>" + Ui.Enc(t.Beds) + " &middot; " + Ui.Enc(t.View) + " view</small></span><span data-l=\"Size\">" + t.Size + " m&sup2;</span><span data-l=\"Sleeps\">" + t.MaxGuests + "</span><span data-l=\"Weekday\" class=\"rr-num\">" + Ui.Money(t.Base) + "</span><span data-l=\"Weekend\" class=\"rr-num\">" + Ui.Money(t.Weekend) + "</span><span data-l=\"Rooms\">" + site.Rooms.Count(r => r.Type == t.Key) + "</span><span data-l=\"Status\"><span class=\"rr-st rr-st-" + (t.Active ? "live\">On sale" : "off\">Hidden") + "</span></span><span><button type=\"button\" class=\"rr-btn rr-btn-sm rr-btn-ghost\" onclick=\"AdminRoomsJs.form('type','" + Ui.Attr(t.Key) + "')\">" + (edit ? "Edit" : "View") + "</button></span></div>");
                }
            }
            else if (tab == "rooms")
            {
                cls = "rr-rmtbl";
                List<HotelRoom> all = site.Rooms.Where(r => (rt == string.Empty || r.Type == rt) && (term == string.Empty || r.No.Contains(term, StringComparison.OrdinalIgnoreCase))).OrderBy(r => r.No).ToList();
                total = all.Count;
                page = Ui.PageOf(GetDataValue("pg"), Size, total);
                sb.Append(Head(new[] { ("Room", string.Empty), ("Type", string.Empty), ("Floor", string.Empty), ("Status", string.Empty), ("Next guest", string.Empty), (string.Empty, string.Empty) }));
                foreach (HotelRoom r in all.Skip((page - 1) * Size).Take(Size))
                {
                    Booking? next = site.Bookings.Where(b => b.Room == r.No && Book.Holds(b) && b.Out.Date > today).OrderBy(b => b.In).FirstOrDefault();
                    sb.Append("<div class=\"rr-tr\"><span data-l=\"Room\"><b>" + Ui.Enc(r.No) + "</b></span><span data-l=\"Type\">" + Ui.Enc(Ui.TypeName(site, r.Type)) + "</span><span data-l=\"Floor\">" + r.Floor + "</span><span data-l=\"Status\"><span class=\"rr-st rr-st-" + r.Status + "\">" + Ui.Enc(r.Status == "ooo" ? "Out of order" : r.Status) + "</span></span><span data-l=\"Next guest\">" + (next != null ? next.In.ToString("d MMM", Ui.Inv) + " &rarr; " + next.Out.ToString("d MMM", Ui.Inv) : "&ndash;") + "</span><span class=\"rr-acts\" style=\"justify-content:flex-end\">" + (edit ? "<button type=\"button\" class=\"rr-btn rr-btn-sm rr-btn-ghost\" onclick=\"AdminRoomsJs.form('room','" + Ui.Attr(r.No) + "')\">Edit</button><button type=\"button\" class=\"rr-btn rr-btn-sm rr-btn-danger\" onclick=\"AdminRoomsJs.del('room','" + Ui.Attr(r.No) + "','room " + Ui.Attr(r.No) + "')\">Delete</button>" : string.Empty) + "</span></div>");
                }
                add = edit ? "<button type=\"button\" class=\"rr-btn rr-btn-sm rr-btn-accent\" onclick=\"AdminRoomsJs.form('room','')\">Add a room</button>" : string.Empty;
            }
            else if (tab == "seasons")
            {
                cls = "rr-setbl";
                List<Season> all = site.Seasons.Where(s => term == string.Empty || s.Name.Contains(term, StringComparison.OrdinalIgnoreCase)).OrderBy(s => s.From).ToList();
                total = all.Count;
                page = Ui.PageOf(GetDataValue("pg"), Size, total);
                sb.Append(Head(new[] { ("Season", string.Empty), ("From", string.Empty), ("To", string.Empty), ("Change", string.Empty), ("Status", string.Empty), (string.Empty, string.Empty) }));
                foreach (Season s in all.Skip((page - 1) * Size).Take(Size))
                {
                    string state = s.To.Date < today ? "expired\">Past" : s.From.Date > today ? "upcoming\">Upcoming" : "live\">Now";
                    sb.Append("<div class=\"rr-tr\"><span data-l=\"Season\" class=\"rr-wide\"><b>" + Ui.Enc(s.Name) + "</b></span><span data-l=\"From\">" + Ui.Day(s.From) + "</span><span data-l=\"To\">" + Ui.Day(s.To) + "</span><span data-l=\"Change\"><b>" + (s.Pct > 0 ? "+" : string.Empty) + s.Pct + "%</b></span><span data-l=\"Status\"><span class=\"rr-st rr-st-" + state + "</span></span><span class=\"rr-acts\" style=\"justify-content:flex-end\">" + (edit ? "<button type=\"button\" class=\"rr-btn rr-btn-sm rr-btn-ghost\" onclick=\"AdminRoomsJs.form('season','" + Ui.Attr(s.Id) + "')\">Edit</button><button type=\"button\" class=\"rr-btn rr-btn-sm rr-btn-danger\" onclick=\"AdminRoomsJs.del('season','" + Ui.Attr(s.Id) + "','" + Ui.Attr(Ui.Js(s.Name)) + "')\">Delete</button>" : string.Empty) + "</span></div>");
                }
                add = edit ? "<button type=\"button\" class=\"rr-btn rr-btn-sm rr-btn-accent\" onclick=\"AdminRoomsJs.form('season','')\">Add a season</button>" : string.Empty;
            }
            else
            {
                cls = "rr-bltbl";
                Dictionary<string, Staff> staff = Ui.StaffMap(await Store.LoadStaff(c.Sbx));
                HashSet<string> ofType = new HashSet<string>(site.Rooms.Where(r => rt == string.Empty || r.Type == rt).Select(r => r.No));
                List<RoomBlock> all = site.Blocks.Where(k => ofType.Contains(k.Room) && (term == string.Empty || k.Room.Contains(term, StringComparison.OrdinalIgnoreCase) || k.Reason.Contains(term, StringComparison.OrdinalIgnoreCase))).OrderByDescending(k => k.From).ToList();
                total = all.Count;
                page = Ui.PageOf(GetDataValue("pg"), Size, total);
                sb.Append(Head(new[] { ("Room", string.Empty), ("From", string.Empty), ("To", string.Empty), ("Reason", string.Empty), ("By", string.Empty), (string.Empty, string.Empty) }));
                foreach (RoomBlock k in all.Skip((page - 1) * Size).Take(Size))
                {
                    sb.Append("<div class=\"rr-tr\"><span data-l=\"Room\"><b>" + Ui.Enc(k.Room) + "</b></span><span data-l=\"From\">" + Ui.Day(k.From) + "</span><span data-l=\"To\">" + Ui.Day(k.To) + "</span><span data-l=\"Reason\">" + Ui.Enc(k.Reason) + "</span><span data-l=\"By\">" + Ui.Enc(Who(staff, k.By)) + "</span><span>" + (block ? "<button type=\"button\" class=\"rr-btn rr-btn-sm rr-btn-danger\" onclick=\"AdminRoomsJs.del('block','" + Ui.Attr(k.Id) + "','this block')\">Remove</button>" : string.Empty) + "</span></div>");
                }
                add = block ? "<button type=\"button\" class=\"rr-btn rr-btn-sm rr-btn-accent\" onclick=\"AdminRoomsJs.form('block','')\">Block a room</button>" : "<span class=\"rr-hint\">Blocking dates needs the Block dates right.</span>";
            }
            if (total == 0)
            {
                sb.Append("<div class=\"rr-tr\"><span class=\"rr-none rr-wide\">Nothing matches.</span></div>");
            }
            response.SetElementContents("rr-list", sb.ToString());
            response.SetElementContents("rr-pager", Ui.Pager("rr", page, Size, total, "item", "items", "AdminRoomsJs.list"));
            response.SetElementContents("rr-addbtn", add);
            response.ExecuteScript("document.getElementById('rr-list').className='rr-tbl " + cls + "';");
            return response;
        }

        private static string Input(string label, string key, string value, string type = "text", string extra = "", bool ro = false)
        {
            return "<label class=\"rr-field\"><span>" + label + "</span><input type=\"" + type + "\" data-k=\"" + key + "\" value=\"" + Ui.Attr(value) + "\" " + extra + (ro ? " readonly" : string.Empty) + "></label>";
        }

        public async Task<ApiResponse> Form()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminRoomsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            string kind = GetDataValue("fkind");
            string id = Ui.Clip(GetDataValue("fid"), 40);
            bool edit = Access.Can(c.U, AreaKey, kind == "block" ? 'X' : 'E');
            bool ro = !edit;
            StringBuilder sb = new StringBuilder();
            DateTime today = DateTime.UtcNow.Date;
            if (kind == "type")
            {
                RoomType? t = site.Types.FirstOrDefault(x => x.Key == id);
                if (t == null)
                {
                    return response;
                }
                sb.Append("<h2>" + Ui.Enc(t.Name) + "</h2><div class=\"rr-form\"><div class=\"rr-f2\">" + Input("Name", "tname", t.Name, "text", "maxlength=\"60\"", ro) + Input("Beds", "tbeds", t.Beds, "text", "maxlength=\"60\"", ro) + "</div><div class=\"rr-f4\">" + Input("Size (m&sup2;)", "tsize", t.Size.ToString(Ui.Inv), "number", "min=\"8\" max=\"400\"", ro) + Input("Sleeps", "tmax", t.MaxGuests.ToString(Ui.Inv), "number", "min=\"1\" max=\"6\"", ro) + Input("Weekday rate ($)", "tbase", t.Base.ToString("0", Ui.Inv), "number", "min=\"20\" max=\"5000\"", ro) + Input("Weekend rate ($)", "twk", t.Weekend.ToString("0", Ui.Inv), "number", "min=\"20\" max=\"5000\"", ro) + "</div><div class=\"rr-f2\">" + Input("View", "tview", t.View, "text", "maxlength=\"30\"", ro) + "<label class=\"rr-chk\" style=\"align-self:end\"><input type=\"checkbox\" data-k=\"tactive\"" + (t.Active ? " checked" : string.Empty) + (ro ? " disabled" : string.Empty) + "> On sale on the website</label></div>" + Input("Short description", "tshort", t.Short, "text", "maxlength=\"140\"", ro) + "<label class=\"rr-field\"><span>Description</span><textarea data-k=\"ttext\" rows=\"3\" maxlength=\"600\"" + (ro ? " readonly" : string.Empty) + ">" + Ui.Enc(t.Text) + "</textarea></label>" + Input("Amenities (comma separated)", "tamen", string.Join(", ", t.Amenities), "text", "maxlength=\"400\"", ro));
                sb.Append("<div><span class=\"rr-hint\">Next 14 nights (flexible rate)</span><div class=\"rr-strip\">");
                for (int i = 0; i < 14; i++)
                {
                    DateTime d = today.AddDays(i);
                    sb.Append("<span>" + d.ToString("ddd", Ui.Inv) + "<b>" + Ui.Money(Book.Rate(site, t, d)) + "</b>" + Book.Free(site, t.Key, d) + " free</span>");
                }
                sb.Append("</div></div><div><span class=\"rr-hint\">Photos from the media library (first one is the main photo, up to 6)</span>" + Ui.Picker("rr", site, "rr-img", "photo", t.Images, true, ro) + "</div><div><span class=\"rr-hint\">Room video (shows a play button on the room page)</span>" + Ui.Picker("rr", site, "rr-vid", "video", new[] { t.Video }, false, ro, true) + "</div></div>");
            }
            else if (kind == "room")
            {
                HotelRoom? r = site.Rooms.FirstOrDefault(x => x.No == id);
                sb.Append("<h2>" + (r == null ? "Add a room" : "Room " + Ui.Enc(r.No)) + "</h2><div class=\"rr-form\"><div class=\"rr-f3\">" + Input("Room number", "rno", r?.No ?? string.Empty, "text", "maxlength=\"6\"", r != null) + "<label class=\"rr-field\"><span>Room type</span><select data-k=\"rtype2\">" + Ui.Opts(site.Types.Select(t => (t.Key, t.Name)), r?.Type ?? site.Types.FirstOrDefault()?.Key ?? string.Empty) + "</select></label>" + Input("Floor", "rfloor", (r?.Floor ?? 1).ToString(Ui.Inv), "number", "min=\"0\" max=\"60\"") + "</div><p class=\"rr-hint\">Changing the type is refused while the room has upcoming guests.</p></div>");
            }
            else if (kind == "season")
            {
                Season? s = site.Seasons.FirstOrDefault(x => x.Id == id);
                sb.Append("<h2>" + (s == null ? "Add a season" : Ui.Enc(s.Name)) + "</h2><div class=\"rr-form\">" + Input("Name", "sname", s?.Name ?? string.Empty, "text", "maxlength=\"60\"") + "<div class=\"rr-f3\">" + Input("From", "sfrom", s != null ? Ui.Ymd(s.From) : Ui.Ymd(today), "date") + Input("To", "sto", s != null ? Ui.Ymd(s.To) : Ui.Ymd(today.AddDays(7)), "date") + Input("Price change (%)", "spct", (s?.Pct ?? 10).ToString(Ui.Inv), "number", "min=\"-50\" max=\"200\"") + "</div><p class=\"rr-hint\">Use a negative number for a low season. If seasons overlap, the highest change wins.</p></div>");
            }
            else if (kind == "block")
            {
                sb.Append("<h2>Block a room</h2><div class=\"rr-form\"><div class=\"rr-f3\"><label class=\"rr-field\"><span>Room</span><select data-k=\"broom\">" + Ui.Opts(site.Rooms.OrderBy(r => r.No).Select(r => (r.No, r.No + " · " + Ui.TypeName(site, r.Type))), string.Empty) + "</select></label>" + Input("From", "bfrom", Ui.Ymd(today), "date") + Input("To", "bto", Ui.Ymd(today.AddDays(2)), "date") + "</div>" + Input("Reason", "breason", string.Empty, "text", "maxlength=\"80\" placeholder=\"Repairs, deep clean, staff use...\"") + "<p class=\"rr-hint\">A blocked room can't be sold. Refused if a guest is already assigned to it on those dates, or if the type would be overbooked.</p></div>");
            }
            else
            {
                return response;
            }
            sb.Append("<em class=\"rr-err\" id=\"rr-e-form\"></em><div class=\"rr-mfoot\"><button type=\"button\" class=\"rr-btn rr-btn-ghost\" onclick=\"AdminRoomsJs.closeModal()\">" + (edit ? "Cancel" : "Close") + "</button>" + (edit ? "<button type=\"button\" class=\"rr-btn rr-btn-accent\" onclick=\"AdminRoomsJs.save('" + kind + "','" + Ui.Attr(id) + "')\">Save</button>" : string.Empty) + "</div>");
            response.SetElementContents("rr-mbody", sb.ToString());
            response.ExecuteScript("AdminRoomsJs.openModal();");
            return response;
        }

        private static decimal Dec(string v, decimal min, decimal max, decimal def)
        {
            return decimal.TryParse(v, System.Globalization.NumberStyles.Number, Ui.Inv, out decimal d) ? Math.Clamp(Math.Round(d, 0), min, max) : def;
        }

        private static int Int(string v, int min, int max, int def)
        {
            return int.TryParse(v, out int d) ? Math.Clamp(d, min, max) : def;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminRoomsJs.expired();");
                return response;
            }
            string kind = GetDataValue("fkind");
            string id = Ui.Clip(GetDataValue("fid"), 40);
            char need = kind == "block" ? 'X' : 'E';
            if (!Access.Can(c.U, AreaKey, need))
            {
                return await Refuse(response, c, kind == "block" ? "block" : "rates", id, need == 'X' ? "Blocking dates needs the Block dates right." : "You can view rates but not change them.");
            }
            string err = string.Empty;
            string log = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                DateTime today = DateTime.UtcNow.Date;
                if (kind == "type")
                {
                    RoomType? t = site.Types.FirstOrDefault(x => x.Key == id);
                    string name = Ui.Clip(GetDataValue("tname"), 60);
                    if (t == null)
                    {
                        return response;
                    }
                    if (name.Length < 3)
                    {
                        err = "Give the room type a name.";
                    }
                    else
                    {
                        t.Name = name;
                        t.Beds = Ui.Clip(GetDataValue("tbeds"), 60);
                        t.Size = Int(GetDataValue("tsize"), 8, 400, t.Size);
                        t.MaxGuests = Int(GetDataValue("tmax"), 1, 6, t.MaxGuests);
                        t.Base = Dec(GetDataValue("tbase"), 20, 5000, t.Base);
                        t.Weekend = Dec(GetDataValue("twk"), 20, 5000, t.Weekend);
                        t.View = Ui.Clip(GetDataValue("tview"), 30);
                        t.Active = GetDataValue("tactive") == "1";
                        t.Short = Ui.Clip(GetDataValue("tshort"), 140);
                        t.Text = Ui.Clip(GetDataValue("ttext"), 600);
                        t.Amenities = GetDataValue("tamen").Split(',').Select(a => Ui.Clip(a, 40)).Where(a => a != string.Empty).Take(14).ToList();
                        List<string> imgs = GetDataValue("fimgs").Split('|').Where(x => Access.SafeImage(x) && site.Media.Any(m => m.Full == x && m.Kind == "photo")).Distinct().Take(6).ToList();
                        if (imgs.Count > 0)
                        {
                            t.Images = imgs;
                        }
                        string vid = GetDataValue("fvid");
                        t.Video = vid == string.Empty ? string.Empty : Access.SafeVideo(vid) && site.Media.Any(m => m.Full == vid && m.Kind == "video") ? vid : t.Video;
                        log = t.Key + " · " + Ui.Money(t.Base) + " / " + Ui.Money(t.Weekend);
                    }
                }
                else if (kind == "room")
                {
                    string no = id != string.Empty ? id : Regex.Replace(Ui.Clip(GetDataValue("rno"), 6), "[^A-Za-z0-9]", string.Empty);
                    string type = Ui.Clip(GetDataValue("rtype2"), 30);
                    HotelRoom? r = site.Rooms.FirstOrDefault(x => x.No == no);
                    if (Ui.TypeOf(site, type) == null || no == string.Empty)
                    {
                        err = "Enter a room number and type.";
                    }
                    else if (id == string.Empty && r != null)
                    {
                        err = "Room " + no + " already exists.";
                    }
                    else if (id == string.Empty && site.Rooms.Count >= 400)
                    {
                        err = "That's the most rooms this demo allows.";
                    }
                    else if (r != null && r.Type != type && site.Bookings.Any(b => b.Room == no && Book.Holds(b)))
                    {
                        err = "Room " + no + " has upcoming guests. Move them first.";
                    }
                    else
                    {
                        if (r == null)
                        {
                            r = new HotelRoom { No = no, Status = "clean", Changed = DateTime.UtcNow, By = c.U.Id };
                            site.Rooms.Add(r);
                        }
                        r.Type = type;
                        r.Floor = Int(GetDataValue("rfloor"), 0, 60, 1);
                        log = "room " + no + " · " + type;
                    }
                }
                else if (kind == "season")
                {
                    DateTime? f = Ui.ParseDay(GetDataValue("sfrom"));
                    DateTime? t = Ui.ParseDay(GetDataValue("sto"));
                    string name = Ui.Clip(GetDataValue("sname"), 60);
                    if (name.Length < 3 || f == null || t == null || t < f)
                    {
                        err = "Enter a name and a valid date range.";
                    }
                    else if ((t.Value - f.Value).TotalDays > 366)
                    {
                        err = "Keep a season under a year.";
                    }
                    else
                    {
                        Season? s = site.Seasons.FirstOrDefault(x => x.Id == id);
                        if (s == null)
                        {
                            s = new Season { Id = Book.NewId() };
                            site.Seasons.Add(s);
                        }
                        s.Name = name;
                        s.From = f.Value;
                        s.To = t.Value;
                        s.Pct = Int(GetDataValue("spct"), -50, 200, 0);
                        log = name + " · " + s.Pct + "%";
                    }
                }
                else if (kind == "block")
                {
                    string no = Ui.Clip(GetDataValue("broom"), 10);
                    HotelRoom? r = site.Rooms.FirstOrDefault(x => x.No == no);
                    DateTime? f = Ui.ParseDay(GetDataValue("bfrom"));
                    DateTime? t = Ui.ParseDay(GetDataValue("bto"));
                    string reason = Ui.Clip(GetDataValue("breason"), 80);
                    if (r == null || f == null || t == null || t < f || reason.Length < 3)
                    {
                        err = "Choose a room, valid dates and a reason.";
                    }
                    else if (site.Bookings.Any(b => b.Room == no && Book.Holds(b) && b.In.Date <= t.Value && b.Out.Date > f.Value))
                    {
                        err = "A guest is assigned to room " + no + " on those dates.";
                    }
                    else
                    {
                        bool over = false;
                        for (DateTime d = f.Value; d <= t.Value; d = d.AddDays(1))
                        {
                            if (Book.Free(site, r.Type, d) <= 0)
                            {
                                over = true;
                                break;
                            }
                        }
                        if (over)
                        {
                            err = "Blocking room " + no + " would overbook " + Ui.TypeName(site, r.Type) + " on some nights.";
                        }
                        else
                        {
                            site.Blocks.Add(new RoomBlock { Id = Book.NewId(), Room = no, From = f.Value, To = t.Value, Reason = reason, By = c.U.Id });
                            log = "room " + no + " · " + Ui.Ymd(f.Value) + " to " + Ui.Ymd(t.Value);
                        }
                    }
                }
                if (err == string.Empty && log != string.Empty)
                {
                    await Store.SaveSite(site, c.Sbx);
                }
            }
            if (err != string.Empty)
            {
                response.SetElementContents("rr-e-form", err);
                return response;
            }
            await Store.Log(c.Sbx, c.U.Id, kind == "block" ? "block" : "rates", log, true);
            response.ExecuteScript("AdminRoomsJs.closeModal();AdminRoomsJs.list(1);AdminRoomsJs.toast('Saved');");
            return response;
        }

        public async Task<ApiResponse> Remove()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminRoomsJs.expired();");
                return response;
            }
            string kind = GetDataValue("fkind");
            string id = Ui.Clip(GetDataValue("fid"), 40);
            char need = kind == "block" ? 'X' : 'E';
            if (!Access.Can(c.U, AreaKey, need))
            {
                return await Refuse(response, c, "delete", kind + " " + id, "You don't have the right to delete this.");
            }
            string err = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                if (kind == "room")
                {
                    if (site.Bookings.Any(b => b.Room == id && Book.Holds(b)))
                    {
                        err = "Room " + id + " has upcoming guests.";
                    }
                    else
                    {
                        HotelRoom? r = site.Rooms.FirstOrDefault(x => x.No == id);
                        if (r != null && site.Rooms.Count(x => x.Type == r.Type) <= 1)
                        {
                            err = "Each room type needs at least one room.";
                        }
                        else if (r != null)
                        {
                            site.Rooms.Remove(r);
                            DateTime today = DateTime.UtcNow.Date;
                            for (int i = 0; i < 400 && err == string.Empty; i++)
                            {
                                if (Book.Free(site, r.Type, today.AddDays(i)) < 0)
                                {
                                    err = "Removing room " + id + " would overbook " + Ui.TypeName(site, r.Type) + " on " + Ui.Day(today.AddDays(i)) + ".";
                                }
                            }
                        }
                    }
                }
                else if (kind == "season")
                {
                    site.Seasons.RemoveAll(s => s.Id == id);
                }
                else if (kind == "block")
                {
                    site.Blocks.RemoveAll(k => k.Id == id);
                }
                if (err == string.Empty)
                {
                    await Store.SaveSite(site, c.Sbx);
                }
            }
            if (err != string.Empty)
            {
                return await Refuse(response, c, "delete", kind + " " + id, err);
            }
            await Store.Log(c.Sbx, c.U.Id, "delete", kind + " " + id, true);
            response.ExecuteScript("AdminRoomsJs.list(1);AdminRoomsJs.toast('Deleted');");
            return response;
        }
    }
}
