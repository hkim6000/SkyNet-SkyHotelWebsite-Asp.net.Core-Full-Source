using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyHotel.Core;
using SkyHotel.Models;
using SkyNet;

namespace SkyHotel.codes
{
    public class AdminFrontDesk : WebPage
    {
        private const string AreaKey = "frontdesk";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Check-in / Check-out");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--fd-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("fd-menu", Auth.Menu("fd", "AdminFrontDesk", c, site));
            response.SetElementContents("fd-who", Auth.Who("fd", c));
            response.SetElementContents("fd-demo", Auth.DemoPill("fd", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminFrontDeskJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"fd-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"fd-panel\"><p>You don't have access to this area. Ask the general manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminFrontDeskJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminFrontDeskJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("fd-main", NoAccess("Check-in / Check-out"));
                return response;
            }
            response.SetElementContents("fd-main", await MainHtml(c, site));
            response.ExecuteScript("AdminFrontDeskJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"fd-tr fd-th\">");
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
            DateTime today = DateTime.UtcNow.Date;
            int a = site.Bookings.Count(b => b.Status == "booked" && b.In.Date == today);
            int h = site.Bookings.Count(b => b.Status == "inhouse");
            int d = site.Bookings.Count(b => b.Status == "inhouse" && b.Out.Date <= today);
            StringBuilder sb = new StringBuilder("<div class=\"fd-ptop\"><div><h1>Check-in / Check-out</h1><p>Today's arrivals and departures. Check guests in to a clean room and settle their bill when they leave.</p></div></div>");
            sb.Append("<div class=\"fd-tabs\"><button type=\"button\" class=\"fd-tab fd-act\" data-t=\"arr\" onclick=\"AdminFrontDeskJs.setTab('arr')\">Arrivals<em>" + a + "</em></button><button type=\"button\" class=\"fd-tab\" data-t=\"in\" onclick=\"AdminFrontDeskJs.setTab('in')\">In house<em>" + h + "</em></button><button type=\"button\" class=\"fd-tab\" data-t=\"out\" onclick=\"AdminFrontDeskJs.setTab('out')\">Departures<em>" + d + "</em></button></div>");
            sb.Append("<div class=\"fd-bar\"><label>Date<input type=\"date\" id=\"fd-fday\" onchange=\"AdminFrontDeskJs.list(1)\"></label><button type=\"button\" class=\"fd-btn fd-btn-sm fd-btn-ghost\" onclick=\"AdminFrontDeskJs.today()\">Today</button><label>Search<input type=\"search\" id=\"fd-q\" list=\"fd-qlist\" placeholder=\"Guest, code or room\" maxlength=\"60\" oninput=\"AdminFrontDeskJs.search()\"></label>" + Ui.DataList("fd-qlist", site.Bookings.Where(b => Book.Holds(b) && b.In.Date <= today.AddDays(7)).Select(b => site.Guests.FirstOrDefault(g => g.Id == b.Guest)).Where(g => g != null).Select(g => Ui.GuestName(g))) + "<label>Room type<select id=\"fd-ftype\" onchange=\"AdminFrontDeskJs.list(1)\"><option value=\"\">All room types</option>" + Ui.Opts(site.Types.Select(t => (t.Key, t.Name)), string.Empty) + "</select></label></div>");
            sb.Append("<div class=\"fd-tbl fd-fdtbl\" id=\"fd-list\"></div><div id=\"fd-pager\"></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminFrontDeskJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            string tab = GetDataValue("fdtab") is "in" or "out" ? GetDataValue("fdtab") : "arr";
            DateTime day = Ui.ParseDay(GetDataValue("fday")) ?? DateTime.UtcNow.Date;
            string term = Ui.Clip(GetDataValue("term"), 60);
            string type = Ui.Clip(GetDataValue("ftype"), 30);
            Dictionary<string, Guest> gm = Ui.GuestMap(site);
            IEnumerable<Booking> q = tab switch
            {
                "in" => site.Bookings.Where(b => b.Status == "inhouse" && b.In.Date <= day),
                "out" => site.Bookings.Where(b => (b.Status == "inhouse" && b.Out.Date <= day) || (b.Status == "out" && b.Out.Date == day)),
                _ => site.Bookings.Where(b => (b.Status == "booked" && b.In.Date == day) || (b.Status == "inhouse" && b.In.Date == day && b.CheckedIn?.Date == day))
            };
            if (type != string.Empty)
            {
                q = q.Where(b => b.Type == type);
            }
            if (term != string.Empty)
            {
                q = q.Where(b => b.Code.Contains(term, StringComparison.OrdinalIgnoreCase) || b.Room == term || (gm.TryGetValue(b.Guest, out Guest? g) && Ui.GuestName(g).Contains(term, StringComparison.OrdinalIgnoreCase)));
            }
            List<Booking> all = q.OrderBy(b => b.Status == "out" || (tab == "arr" && b.Status == "inhouse")).ThenBy(b => b.Room == string.Empty).ThenBy(b => b.Room).ToList();
            StringBuilder sb = new StringBuilder(Head(new[] { ("Guest", string.Empty), ("Code", string.Empty), ("Room", string.Empty), ("Guests", string.Empty), ("Stay", string.Empty), ("Status", string.Empty), ("Balance", "fd-num"), (string.Empty, string.Empty) }));
            if (all.Count == 0)
            {
                sb.Append("<div class=\"fd-tr\"><span class=\"fd-none fd-wide\">" + (tab == "arr" ? "No arrivals" : tab == "in" ? "No guests in house" : "No departures") + " for " + Ui.Day(day) + ".</span></div>");
            }
            int page = Ui.PageOf(GetDataValue("pg"), Size, all.Count);
            bool edit = Access.Can(c.U, AreaKey, 'E');
            foreach (Booking b in all.Skip((page - 1) * Size).Take(Size))
            {
                Guest? g = gm.TryGetValue(b.Guest, out Guest? x) ? x : null;
                decimal bal = b.Status == "out" ? 0 : Book.Balance(site, b);
                string btn = string.Empty;
                if (b.Status == "booked")
                {
                    btn = b.In.Date <= DateTime.UtcNow.Date && edit ? "<button type=\"button\" class=\"fd-btn fd-btn-sm fd-btn-accent\" onclick=\"AdminFrontDeskJs.checkIn('" + b.Id + "')\">Check in</button>" : "<button type=\"button\" class=\"fd-btn fd-btn-sm fd-btn-ghost\" onclick=\"AdminFrontDeskJs.checkIn('" + b.Id + "')\">View</button>";
                }
                else if (b.Status == "inhouse")
                {
                    btn = "<button type=\"button\" class=\"fd-btn fd-btn-sm" + (edit ? string.Empty : " fd-btn-ghost") + "\" onclick=\"AdminFrontDeskJs.checkOut('" + b.Id + "')\">" + (edit ? "Check out" : "Folio") + "</button>";
                }
                sb.Append("<div class=\"fd-tr\"><span data-l=\"Guest\" class=\"fd-wide\"><b>" + Ui.Enc(Ui.GuestName(g)) + (g != null && g.Vip ? "<span class=\"fd-vip\">VIP</span>" : string.Empty) + "</b><small>" + (g != null && g.Member ? Ui.GradeOf(g.Lifetime).Name + " member" : "Guest") + (b.Arrival != string.Empty && b.Status == "booked" ? " &middot; arrives ~" + Ui.Enc(b.Arrival) : string.Empty) + "</small>" + Ui.LateBadge("fd", g) + "</span><span data-l=\"Code\" class=\"fd-code\">" + Ui.Enc(b.Code) + (b.Promo != string.Empty ? "<small class=\"fd-pcode\">" + Ui.Enc(b.Promo) + "</small>" : string.Empty) + "</span><span data-l=\"Room\">" + (b.Room != string.Empty ? "<b>" + Ui.Enc(b.Room) + "</b>" : "<i>Not assigned</i>") + "<small>" + Ui.Enc(Ui.TypeName(site, b.Type)) + "</small></span><span data-l=\"Guests\">" + (b.Adults + b.Children) + "</span><span data-l=\"Stay\">" + b.In.ToString("d MMM", Ui.Inv) + " &rarr; " + b.Out.ToString("d MMM", Ui.Inv) + "<small>" + Book.Nights(b.In, b.Out) + " nights" + (b.LateOut != string.Empty ? " &middot; <span class=\"fd-lateout\">out " + Ui.Enc(b.LateOut) + "</span>" : string.Empty) + "</small></span><span data-l=\"Status\">" + Auth.StatusPill("fd", b) + "</span><span data-l=\"Balance\" class=\"fd-num\">" + (bal > 0 ? "<b>" + Ui.Cash(bal) + "</b>" : "&ndash;") + "</span><span>" + btn + "</span></div>");
            }
            response.SetElementContents("fd-list", sb.ToString());
            response.SetElementContents("fd-pager", Ui.Pager("fd", page, Size, all.Count, "booking", "bookings", "AdminFrontDeskJs.list"));
            return response;
        }

        private static string Summary(SiteData site, Booking b, Guest? g)
        {
            return "<dl class=\"fd-dl\"><dt>Guest</dt><dd><b>" + Ui.Enc(Ui.GuestName(g)) + "</b>" + (g != null && g.Member ? " " + Ui.GradePill("fd", g) + " " + Ui.Enc(g.MemberNo) : string.Empty) + (g != null && g.Vip ? "<span class=\"fd-vip\">VIP</span>" : string.Empty) + "</dd><dt>Booking</dt><dd class=\"fd-code\">" + Ui.Enc(b.Code) + "</dd><dt>Stay</dt><dd>" + Ui.Day(b.In) + " &rarr; " + Ui.Day(b.Out) + " &middot; " + Book.Nights(b.In, b.Out) + " nights</dd><dt>Room type</dt><dd>" + Ui.Enc(Ui.TypeName(site, b.Type)) + " &middot; " + b.Adults + " adults" + (b.Children > 0 ? ", " + b.Children + " children" : string.Empty) + " &middot; " + Book.PlanName(b.Plan) + "</dd>" + (b.Requests != string.Empty ? "<dt>Requests</dt><dd>" + Ui.Enc(b.Requests) + "</dd>" : string.Empty) + (g != null && g.Notes != string.Empty ? "<dt>Guest notes</dt><dd>" + Ui.Enc(g.Notes) + "</dd>" : string.Empty) + (b.Source == "agency" ? "<dt>Agency</dt><dd>" + Ui.Enc(b.Agency) + (b.AgencyRef != string.Empty ? " &middot; ref " + Ui.Enc(b.AgencyRef) : string.Empty) + "</dd>" : string.Empty) + (g != null && (g.Country != string.Empty || g.Passport != string.Empty) ? "<dt>Country / ID</dt><dd>" + Ui.Enc(g.Country) + (g.Passport != string.Empty ? " &middot; passport " + Ui.MaskPassport(g.Passport) : Ui.NeedsPassport(site, g.Country) ? " &middot; <b class=\"fd-lateout\">passport needed</b>" : string.Empty) + "</dd>" : string.Empty) + "<dt>Payment</dt><dd>" + (b.PayAtFront ? "Pays at the front desk" + (b.Paid > 0 ? " &middot; " + Ui.Cash(b.Paid) + " paid by " + (b.PaidBy == "cash" ? "cash" : "card") : string.Empty) : Ui.Enc(b.Card) + " &bull;&bull;&bull;&bull; " + Ui.Enc(b.Last4) + " (demo)") + "</dd>" + (b.Deposit > 0 ? "<dt>Deposit</dt><dd>" + Ui.Cash(b.Deposit) + " " + (b.DepositBy == "cash" ? "cash" : "card hold") + "</dd>" : string.Empty) + "</dl>";
        }

        private static string DetailsForm(SiteData site, Booking b, Guest? g, bool can)
        {
            string ro = can ? string.Empty : " readonly";
            string eligible = Ui.LateOk(g) ? Ui.GradePill("fd", g!) + Ui.LateBadge("fd", g) + "<span>Bronze and above: offer a later check-out, up to " + Ui.LatestOut + ".</span>" : (g != null && g.Member ? Ui.GradePill("fd", g) + " " : string.Empty) + "<span>Standard check-out " + Ui.Enc(site.Settings.CheckOut) + ". You can still give a later time, up to " + Ui.LatestOut + ".</span>";
            StringBuilder sb = new StringBuilder("<div class=\"fd-gd\"><h3>Guest details</h3><div class=\"fd-form\"><div class=\"fd-gdgrid\"><label class=\"fd-field\"><span>Phone</span><input type=\"tel\" id=\"fd-dphone\" maxlength=\"30\" value=\"" + Ui.Attr(g?.Phone) + "\"" + ro + "></label><label class=\"fd-field\"><span>Address</span><input type=\"text\" id=\"fd-daddr\" maxlength=\"160\" value=\"" + Ui.Attr(g?.Address) + "\" placeholder=\"Street, town, postcode\"" + ro + "></label></div>");
            string ctry = g?.Country ?? string.Empty;
            IEnumerable<string> countries = Ui.Countries.Contains(ctry) || ctry == string.Empty ? Ui.Countries : Ui.Countries.Append(ctry).OrderBy(x => x);
            sb.Append("<div class=\"fd-gdgrid2\"><label class=\"fd-field\"><span>Country</span><select id=\"fd-dcountry\"" + (can ? string.Empty : " disabled") + "><option value=\"\">Choose a country</option>" + Ui.Opts(countries.Select(x => (x, x)), ctry) + "</select></label><label class=\"fd-field\"><span>Passport no.</span><input type=\"text\" id=\"fd-dpass\" maxlength=\"20\" value=\"" + Ui.Attr(g?.Passport) + "\" style=\"text-transform:uppercase\" autocomplete=\"off\" placeholder=\"Required outside " + Ui.Attr(site.Settings.Country) + "\"" + ro + "></label></div>");
            sb.Append("<div class=\"fd-gdgrid3\"><label class=\"fd-field\"><span>Car plate</span><input type=\"text\" id=\"fd-dplate\" maxlength=\"12\" value=\"" + Ui.Attr(b.Plate) + "\" style=\"text-transform:uppercase\"" + ro + "></label><label class=\"fd-field\"><span>Valet ticket no.</span><input type=\"text\" id=\"fd-dvalet\" maxlength=\"12\" value=\"" + Ui.Attr(b.Valet) + "\"" + ro + "></label><label class=\"fd-field\"><span>Check-out time</span><select id=\"fd-dout\"" + (can ? string.Empty : " disabled") + ">" + Ui.Opts(Ui.OutTimes(site).Select(t => (t, t + (t == site.Settings.CheckOut ? " (standard)" : " (late)"))), Ui.OutTime(site, b)) + "</select></label></div>");
            sb.Append("<p class=\"fd-gdnote\">" + eligible + "</p><em class=\"fd-err\" id=\"fd-e-gd\"></em>" + (can && b.Status == "inhouse" ? "<div><button type=\"button\" class=\"fd-btn fd-btn-sm fd-btn-ghost\" onclick=\"AdminFrontDeskJs.saveDetails('" + b.Id + "')\">Save details</button></div>" : string.Empty) + "</div></div>");
            return sb.ToString();
        }

        private string ReadDetails(SiteData site, Booking b, Guest? g)
        {
            string phone = Ui.CleanPhone(GetDataValue("dphone"));
            string addr = Regex.Replace(Ui.Clip(GetDataValue("daddr"), 160), "\\s+", " ").Trim();
            string plate = Ui.CleanPlate(GetDataValue("dplate"));
            string valet = Regex.Replace(Ui.Clip(GetDataValue("dvalet"), 12), "[^A-Za-z0-9-]", string.Empty);
            string late = GetDataValue("dout");
            string country = Ui.Clip(GetDataValue("dcountry"), 40);
            string pass = Ui.CleanPassport(GetDataValue("dpass"));
            if (country == string.Empty || !(Ui.Countries.Contains(country) || country == (g?.Country ?? string.Empty)))
            {
                return "Choose the guest's country.";
            }
            if (Ui.NeedsPassport(site, country) && pass == string.Empty)
            {
                return "Passport no. is required for guests from outside " + site.Settings.Country + ".";
            }
            if (pass != string.Empty && (pass.Length < 5 || pass.Length > 15))
            {
                return "Check the passport number (5 to 15 letters or digits).";
            }
            if (!Ui.OutTimes(site).Contains(late))
            {
                return "Choose a check-out time up to " + Ui.LatestOut + ".";
            }
            if (phone != string.Empty && Regex.Replace(phone, "[^0-9]", string.Empty).Length < 6)
            {
                return "Check the phone number.";
            }
            if (g != null)
            {
                g.Phone = phone;
                g.Address = addr;
                g.Country = country;
                g.Passport = pass;
            }
            b.Plate = plate;
            b.Valet = valet;
            b.LateOut = late == site.Settings.CheckOut ? string.Empty : late;
            return string.Empty;
        }

        public async Task<ApiResponse> Details()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminFrontDeskJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "guest-details", GetDataValue("bid"), "You can view the front desk but not change guest details.");
            }
            string err = string.Empty;
            string log = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Booking? b = site.Bookings.FirstOrDefault(x => x.Id == GetDataValue("bid") && (x.Status == "inhouse" || x.Status == "booked"));
                if (b == null)
                {
                    return response;
                }
                Guest? g = site.Guests.FirstOrDefault(x => x.Id == b.Guest);
                err = ReadDetails(site, b, g);
                if (err == string.Empty)
                {
                    log = b.Code + " · out " + Ui.OutTime(site, b) + (b.Plate != string.Empty ? " · car " + b.Plate : string.Empty);
                    await Store.SaveSite(site, c.Sbx);
                    response.SetElementContents("fd-mbody", Folio(site, b, g, true));
                }
            }
            if (err != string.Empty)
            {
                response.SetElementContents("fd-e-gd", err);
                return response;
            }
            await Store.Log(c.Sbx, c.U.Id, "guest-details", log, true);
            response.ExecuteScript("AdminFrontDeskJs.done();AdminFrontDeskJs.toast('Details saved');");
            return response;
        }

        public async Task<ApiResponse> InForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminFrontDeskJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Booking? b = site.Bookings.FirstOrDefault(x => x.Id == GetDataValue("bid"));
            if (b == null || !Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            Guest? g = site.Guests.FirstOrDefault(x => x.Id == b.Guest);
            bool can = Access.Can(c.U, AreaKey, 'E') && b.Status == "booked" && b.In.Date <= DateTime.UtcNow.Date;
            StringBuilder sb = new StringBuilder("<div id=\"fd-instep\"><h2>Check in</h2>" + Summary(site, b, g) + PromoBox(site, b, can));
            sb.Append("<h3>Choose a room</h3><div class=\"fd-rooms\">");
            foreach (HotelRoom r in site.Rooms.Where(r => r.Type == b.Type).OrderBy(r => r.No))
            {
                bool free = Book.RoomFree(site, r.No, b.In, b.Out, b.Id);
                bool ready = r.Status == "clean" || r.Status == "inspected";
                bool ok = free && ready;
                string why = !free || r.Status == "occupied" ? "Occupied" : r.Status == "dirty" ? "Dirty" : r.Status == "ooo" ? "Out of order" : r.Status == "inspected" ? "Inspected" : "Clean";
                sb.Append("<label class=\"fd-rpick" + (ok ? string.Empty : " fd-no") + "\"><input type=\"radio\" name=\"fd-room\" value=\"" + Ui.Attr(r.No) + "\"" + (ok ? string.Empty : " disabled") + (r.No == b.Room && ok ? " checked" : string.Empty) + "><b>" + Ui.Enc(r.No) + "</b><span class=\"fd-st fd-st-" + (free ? r.Status : "occupied") + "\">" + why + "</span><small class=\"fd-hint\">Floor " + r.Floor + "</small></label>");
            }
            sb.Append("</div><p class=\"fd-hint\" style=\"margin-top:10px\">Only free rooms that are Clean or Inspected can be used. Ask housekeeping to update a dirty room.</p>" + DetailsForm(site, b, g, can) + "<div class=\"fd-mfoot\"><button type=\"button\" class=\"fd-btn fd-btn-ghost\" onclick=\"AdminFrontDeskJs.closeModal()\">Close</button>" + (can ? "<button type=\"button\" class=\"fd-btn fd-btn-accent\" onclick=\"AdminFrontDeskJs.doIn('" + b.Id + "')\">" + (Book.Balance(site, b) > 0 || site.Settings.Deposit > 0 ? "Continue to payment" : "Check in") + "</button>" : string.Empty) + "</div></div><div id=\"fd-paystep\" hidden></div>");
            response.SetElementContents("fd-mbody", sb.ToString());
            response.ExecuteScript("AdminFrontDeskJs.openModal();");
            return response;
        }

        public async Task<ApiResponse> CheckIn()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminFrontDeskJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "check-in", GetDataValue("bid"), "You can view the front desk but not check guests in.");
            }
            string id = GetDataValue("bid");
            string room = Ui.Clip(GetDataValue("room"), 10);
            string err = string.Empty;
            string code = string.Empty;
            string inpay = GetDataValue("inpay") is "card" or "desk" or "cash" ? GetDataValue("inpay") : "desk";
            string indep = GetDataValue("indep") == "cash" ? "cash" : "card";
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Booking? b = site.Bookings.FirstOrDefault(x => x.Id == id);
                HotelRoom? r = site.Rooms.FirstOrDefault(x => x.No == room);
                if (b == null || b.Status != "booked")
                {
                    err = "This booking can't be checked in.";
                }
                else if (b.In.Date > DateTime.UtcNow.Date)
                {
                    err = "This guest arrives on " + Ui.Day(b.In) + ".";
                }
                else if (r == null || r.Type != b.Type)
                {
                    err = "Choose a room first.";
                }
                else if (!Book.RoomFree(site, r.No, b.In, b.Out, b.Id))
                {
                    err = "Room " + r.No + " is taken for these dates.";
                }
                else if (r.Status != "clean" && r.Status != "inspected")
                {
                    err = "Room " + r.No + " isn't ready yet.";
                }
                else if ((err = ReadDetails(site, b, site.Guests.FirstOrDefault(x => x.Id == b.Guest))) != string.Empty)
                {
                    response.SetElementContents("fd-e-gd", err);
                    response.ExecuteScript("AdminFrontDeskJs.backIn();AdminFrontDeskJs.inFail();");
                    return response;
                }
                else
                {
                    string dropped = GetDataValue("pkeep") == "remove" ? DropPromo(site, b) : string.Empty;
                    decimal due = Math.Max(0, Book.Balance(site, b));
                    decimal dep = site.Settings.Deposit;
                    if (GetDataValue("step") != "go" && (due > 0 || dep > 0))
                    {
                        response.SetElementContents("fd-paystep", PayStep(b, r.No, due, dep));
                        response.ExecuteScript("AdminFrontDeskJs.showPay();");
                        return response;
                    }
                    if (due > 0)
                    {
                        b.Paid += due;
                        b.PaidBy = b.Card == string.Empty && inpay == "card" ? "desk" : inpay;
                    }
                    b.Deposit = dep;
                    b.DepositBy = dep > 0 ? indep : string.Empty;
                    b.DepositBack = 0;
                    b.Room = r.No;
                    b.Status = "inhouse";
                    b.CheckedIn = DateTime.UtcNow;
                    r.Status = "occupied";
                    r.Changed = DateTime.UtcNow;
                    r.By = c.U.Id;
                    code = b.Code + " · room " + r.No + (dropped != string.Empty ? " · removed " + dropped : string.Empty) + (due > 0 ? " · took " + Ui.Cash(due) + " " + (b.PaidBy == "cash" ? "cash" : "card") : string.Empty) + (dep > 0 ? " · deposit " + Ui.Cash(dep) + " " + (indep == "cash" ? "cash" : "card hold") : string.Empty);
                    await Store.SaveSite(site, c.Sbx);
                }
            }
            if (err != string.Empty)
            {
                response.ExecuteScript("AdminFrontDeskJs.backIn();AdminFrontDeskJs.inFail();");
                return await Refuse(response, c, "check-in", id, err);
            }
            await Store.Log(c.Sbx, c.U.Id, "check-in", code, true);
            SiteData fresh = await Store.LoadSite(c.Sbx);
            Chrome(response, c, fresh);
            response.ExecuteScript("AdminFrontDeskJs.closeModal();AdminFrontDeskJs.done();AdminFrontDeskJs.toast('" + Ui.Js("Checked in: " + code) + "');");
            return response;
        }

        private static string PromoBox(SiteData site, Booking b, bool can)
        {
            if (b.Promo == string.Empty)
            {
                return string.Empty;
            }
            Offer? o = site.Offers.FirstOrDefault(x => x.Code == b.Promo);
            string head = "<h3>Promotion &middot; <span class=\"fd-code\">" + Ui.Enc(b.Promo) + "</span></h3><span>" + (o != null ? Ui.Enc(o.Title) + " &middot; " : string.Empty) + (b.Discount > 0 ? Ui.Cash(b.Discount) + " off the room" : "no discount") + (b.Bonus > 1 ? " &middot; " + b.Bonus + "&times; points" : string.Empty) + "</span>";
            if (o == null)
            {
                return "<div class=\"fd-promo fd-promo-info\">" + head + "<span class=\"fd-hint\">This offer has been deleted. The booking keeps its price.</span></div>";
            }
            if (Book.Fits(o, b.Type))
            {
                return "<div class=\"fd-promo fd-promo-ok\">" + head + "<span>&#10003; Valid for " + Ui.Enc(Ui.TypeName(site, b.Type)) + (o.Types.Count == 0 ? " (all room types)" : string.Empty) + "</span>" + (!o.Active ? "<span class=\"fd-hint\">The offer is switched off now; the booking keeps its price.</span>" : string.Empty) + "</div>";
            }
            return "<div class=\"fd-promo fd-promo-bad\">" + head + "<span><b>&#9888; Not valid for " + Ui.Enc(Ui.TypeName(site, b.Type)) + ".</b> " + Ui.Enc(o.Code) + " is for " + Ui.Enc(Book.TypesText(site, o)) + ".</span><div class=\"fd-pays\"><label><input type=\"radio\" name=\"fd-promo\" value=\"keep\" checked" + (can ? string.Empty : " disabled") + ">Honor the discount</label><label><input type=\"radio\" name=\"fd-promo\" value=\"remove\"" + (can ? string.Empty : " disabled") + ">Remove the discount (" + Ui.Cash(b.Discount) + " plus tax goes back on the bill)</label></div></div>";
        }

        private static string DropPromo(SiteData site, Booking b)
        {
            Offer? o = site.Offers.FirstOrDefault(x => x.Code == b.Promo);
            if (b.Promo == string.Empty || o == null || Book.Fits(o, b.Type))
            {
                return string.Empty;
            }
            string code = b.Promo;
            b.Discount = 0;
            b.Promo = string.Empty;
            b.Bonus = 1;
            decimal taxable = b.RoomTotal - b.MemberOff + b.Breakfast - b.PointsValue;
            b.Tax = Math.Round(taxable * site.Settings.Tax / 100m, 2);
            b.Total = taxable + b.Tax;
            return code;
        }

        private static string PayStep(Booking b, string room, decimal due, decimal dep)
        {
            StringBuilder sb = new StringBuilder("<h2>Payment before check-in &middot; room " + Ui.Enc(room) + "</h2><div class=\"fd-paybox\">");
            if (due > 0)
            {
                sb.Append("<div class=\"fd-paysec\"><h3>Balance due now</h3><p>" + (b.PayAtFront ? "Pay at front desk booking. " : string.Empty) + "Take <b>" + Ui.Cash(due) + "</b> before handing over the key.</p><div class=\"fd-pays\">");
                bool card = b.Card != string.Empty && b.Last4 != string.Empty;
                if (card)
                {
                    sb.Append("<label><input type=\"radio\" name=\"fd-inpay\" value=\"card\" checked>Card on file &middot; " + Ui.Enc(b.Card) + " &bull;&bull;&bull;&bull; " + Ui.Enc(b.Last4) + " (demo)</label>");
                }
                sb.Append("<label><input type=\"radio\" name=\"fd-inpay\" value=\"desk\"" + (card ? string.Empty : " checked") + ">Card at the desk (demo)</label><label><input type=\"radio\" name=\"fd-inpay\" value=\"cash\">Cash</label></div></div>");
            }
            else
            {
                sb.Append("<div class=\"fd-paysec\"><h3>Room</h3><p>Paid in full" + (b.Card != string.Empty ? " on " + Ui.Enc(b.Card) + " &bull;&bull;&bull;&bull; " + Ui.Enc(b.Last4) : string.Empty) + ". Nothing more to take for the stay.</p></div>");
            }
            if (dep > 0)
            {
                sb.Append("<div class=\"fd-paysec\"><h3>Security deposit</h3><p>Hold <b>" + Ui.Cash(dep) + "</b> for incidentals. It is used for any extras at check-out and the rest is released.</p><div class=\"fd-pays\"><label><input type=\"radio\" name=\"fd-indep\" value=\"card\" checked>Card hold (demo)</label><label><input type=\"radio\" name=\"fd-indep\" value=\"cash\">Cash deposit</label></div></div>");
            }
            sb.Append("<div class=\"fd-paytot\"><span>" + (due > 0 ? "Payment " + Ui.Cash(due) + (dep > 0 ? " + deposit " + Ui.Cash(dep) : string.Empty) : "Deposit") + "</span><b>" + Ui.Cash(due + dep) + "</b></div></div><div class=\"fd-mfoot\"><button type=\"button\" class=\"fd-btn fd-btn-ghost\" onclick=\"AdminFrontDeskJs.backIn()\">&larr; Back</button><button type=\"button\" class=\"fd-btn fd-btn-accent\" id=\"fd-inbtn\" onclick=\"AdminFrontDeskJs.confirmIn('" + b.Id + "')\">Take payment and check in</button></div>");
            return sb.ToString();
        }

        private static (decimal Covered, decimal Due, decimal Back) Settle(decimal bal, decimal dep)
        {
            decimal covered = Math.Min(Math.Max(bal, 0), dep);
            return (covered, Math.Max(bal, 0) - covered, dep - covered);
        }

        private static string Folio(SiteData site, Booking b, Guest? g, bool can)
        {
            decimal extras = Book.ExtrasTotal(b);
            decimal xtax = Math.Round(extras * site.Settings.Tax / 100m, 2);
            decimal bal = Book.Balance(site, b);
            StringBuilder sb = new StringBuilder("<h2>Check out &middot; room " + Ui.Enc(b.Room) + "</h2>" + Summary(site, b, g) + DetailsForm(site, b, g, can) + "<div class=\"fd-folio\" style=\"margin-top:18px\"><div><h3>Bill</h3><dl class=\"fd-lines\"><dt>Room stay (" + Book.PlanName(b.Plan) + ")</dt><dd>" + Ui.Cash(b.Total - b.Tax) + "</dd><dt>Tax on stay</dt><dd>" + Ui.Cash(b.Tax) + "</dd>");
            foreach (Charge x in b.Extras)
            {
                sb.Append("<dt>" + Ui.Enc(x.What) + " <small class=\"fd-hint\">" + x.When.ToString("d MMM", Ui.Inv) + "</small></dt><dd>" + Ui.Cash(x.Amount) + "</dd>");
            }
            if (extras > 0)
            {
                sb.Append("<dt>Tax on extras</dt><dd>" + Ui.Cash(xtax) + "</dd>");
            }
            sb.Append("<dt>Paid</dt><dd>&minus;" + Ui.Cash(b.Paid) + "</dd><dt class=\"fd-tot\">Balance due</dt><dd class=\"fd-tot\">" + Ui.Cash(bal) + "</dd></dl>");
            if (can)
            {
                sb.Append("<div class=\"fd-charge\"><label class=\"fd-field\"><span>Add a charge</span><select id=\"fd-cwhat\">" + Ui.Opts(new[] { ("Minibar", "Minibar"), ("Late checkout", "Late checkout"), ("Tidewater dinner", "Tidewater dinner"), ("Lantern Bar", "Lantern Bar"), ("Room service", "Room service"), ("Spa treatment", "Spa treatment"), ("Parking", "Parking"), ("Laundry", "Laundry") }, "Minibar") + "</select></label><label class=\"fd-field\"><span>Amount</span><input type=\"number\" id=\"fd-camt\" min=\"1\" max=\"5000\" step=\"0.01\"></label><button type=\"button\" class=\"fd-btn fd-btn-sm fd-btn-ghost\" onclick=\"AdminFrontDeskJs.charge('" + b.Id + "')\">Add</button></div><em class=\"fd-err\" id=\"fd-e-camt\"></em>");
            }
            sb.Append("</div><div><h3>Payment &amp; points</h3><p>" + (bal > 0 ? (b.PayAtFront ? "Pay at front desk. " : string.Empty) + "Balance due: <b>" + Ui.Cash(bal) + "</b>" : bal < 0 ? "<b>" + Ui.Cash(-bal) + "</b> will be refunded to " + Ui.Enc(b.Card) + " &bull;&bull;&bull;&bull; " + Ui.Enc(b.Last4) + " (demo)." : "Nothing is due. The stay was paid in full.") + "</p>");
            (decimal covered, decimal rest, decimal back) = Settle(bal, b.Deposit);
            if (b.Deposit > 0)
            {
                sb.Append("<dl class=\"fd-lines\"><dt>Deposit held (" + (b.DepositBy == "cash" ? "cash" : "card hold") + ")</dt><dd>" + Ui.Cash(b.Deposit) + "</dd>" + (covered > 0 ? "<dt>Used for the balance</dt><dd>&minus;" + Ui.Cash(covered) + "</dd>" : string.Empty) + "<dt class=\"fd-tot\">" + (b.DepositBy == "cash" ? "Cash to return" : "Hold to release") + "</dt><dd class=\"fd-tot\">" + Ui.Cash(back) + "</dd>" + (rest > 0 ? "<dt class=\"fd-tot\">Still to take</dt><dd class=\"fd-tot\">" + Ui.Cash(rest) + "</dd>" : string.Empty) + "</dl>");
            }
            if (rest > 0)
            {
                List<(string Key, string Label)> pays = new List<(string, string)>();
                if (b.Card != string.Empty && b.Last4 != string.Empty)
                {
                    pays.Add(("card", "Card on file &middot; " + Ui.Enc(b.Card) + " &bull;&bull;&bull;&bull; " + Ui.Enc(b.Last4) + " (demo)"));
                }
                pays.Add(("desk", "Card at the desk (demo)"));
                pays.Add(("cash", "Cash"));
                sb.Append("<div class=\"fd-pays\">");
                for (int i = 0; i < pays.Count; i++)
                {
                    sb.Append("<label><input type=\"radio\" name=\"fd-pay\" value=\"" + pays[i].Key + "\"" + (i == 0 ? " checked" : string.Empty) + (can ? string.Empty : " disabled") + ">" + pays[i].Label + "</label>");
                }
                sb.Append("</div>");
            }
            if (g != null && g.Member)
            {
                int pts = Book.EarnOnStay(b);
                (string Key, string Name, int Min, int Pct, string Perks) now = Ui.GradeOf(g.Lifetime);
                (string Key, string Name, int Min, int Pct, string Perks) after = Ui.GradeOf(g.Lifetime + pts);
                sb.Append("<div class=\"fd-ptsprev\"><span>" + Ui.Enc(g.First) + " earns<br><b>+" + Ui.Num(pts) + " pts</b></span><span>" + Ui.GradePill("fd", g) + (after.Key != now.Key ? " &rarr; <span class=\"fd-grade fd-g-" + after.Key + "\">" + after.Name + "</span>" : string.Empty) + "<br><small>Lifetime " + Ui.Num(g.Lifetime) + " &rarr; " + Ui.Num(g.Lifetime + pts) + "</small></span></div>");
            }
            else
            {
                sb.Append("<p class=\"fd-hint\">Not a Circle member, so no points. Invite them to join at checkout.</p>");
            }
            sb.Append("</div></div><div class=\"fd-mfoot\"><button type=\"button\" class=\"fd-btn fd-btn-ghost\" onclick=\"AdminFrontDeskJs.closeModal()\">Close</button>" + (can ? "<button type=\"button\" class=\"fd-btn fd-btn-accent\" onclick=\"AdminFrontDeskJs.doOut('" + b.Id + "','" + Ui.Attr(Ui.Js(b.Room)) + "')\">" + (rest > 0 ? "Take " + Ui.Cash(rest) + " and check out" : "Check out") + "</button>" : string.Empty) + "</div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> OutForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminFrontDeskJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Booking? b = site.Bookings.FirstOrDefault(x => x.Id == GetDataValue("bid"));
            if (b == null || b.Status != "inhouse" || !Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            Guest? g = site.Guests.FirstOrDefault(x => x.Id == b.Guest);
            response.SetElementContents("fd-mbody", Folio(site, b, g, Access.Can(c.U, AreaKey, 'E')));
            response.ExecuteScript("AdminFrontDeskJs.openModal();");
            return response;
        }

        public async Task<ApiResponse> Charge()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminFrontDeskJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "charge", GetDataValue("bid"), "You can't add charges.");
            }
            string what = Ui.Clip(GetDataValue("cwhat"), 40);
            if (!decimal.TryParse(GetDataValue("camt"), System.Globalization.NumberStyles.Number, Ui.Inv, out decimal amt) || amt <= 0 || amt > 5000)
            {
                response.SetElementContents("fd-e-camt", "Enter an amount between $1 and $5,000.");
                return response;
            }
            amt = Math.Round(amt, 2);
            string code = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Booking? b = site.Bookings.FirstOrDefault(x => x.Id == GetDataValue("bid"));
                if (b == null || b.Status != "inhouse" || b.Extras.Count >= 60)
                {
                    return response;
                }
                b.Extras.Add(new Charge { What = what == string.Empty ? "Charge" : what, Amount = amt, When = DateTime.UtcNow, By = c.U.Id });
                code = b.Code;
                await Store.SaveSite(site, c.Sbx);
                Guest? g = site.Guests.FirstOrDefault(x => x.Id == b.Guest);
                response.SetElementContents("fd-mbody", Folio(site, b, g, true));
            }
            await Store.Log(c.Sbx, c.U.Id, "charge", code + " · " + what + " " + Ui.Cash(amt), true);
            response.ExecuteScript("AdminFrontDeskJs.done();");
            return response;
        }

        public async Task<ApiResponse> CheckOut()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminFrontDeskJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "check-out", GetDataValue("bid"), "You can view folios but not check guests out.");
            }
            string id = GetDataValue("bid");
            string err = string.Empty;
            string log = string.Empty;
            int pts = 0;
            string gradeMsg = string.Empty;
            decimal charged = 0;
            decimal taken = 0;
            decimal returned = 0;
            string depBy = string.Empty;
            string guest = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Booking? b = site.Bookings.FirstOrDefault(x => x.Id == id);
                if (b == null || b.Status != "inhouse")
                {
                    err = "This guest isn't in house.";
                }
                else
                {
                    charged = Book.Balance(site, b);
                    (decimal covered, decimal rest, decimal back) = Settle(charged, b.Deposit);
                    taken = rest;
                    returned = back;
                    depBy = b.DepositBy;
                    b.Paid += charged;
                    b.DepositBack = back;
                    if (rest > 0)
                    {
                        b.PaidBy = GetDataValue("pay") is "card" or "desk" or "cash" ? GetDataValue("pay") : "desk";
                    }
                    else if (covered > 0)
                    {
                        b.PaidBy = b.DepositBy == "cash" ? "cash" : "card";
                    }
                    b.Status = "out";
                    b.CheckedOut = DateTime.UtcNow;
                    Guest? g = site.Guests.FirstOrDefault(x => x.Id == b.Guest);
                    guest = Ui.GuestName(g);
                    if (g != null && g.Member)
                    {
                        string before = Ui.GradeOf(g.Lifetime).Name;
                        pts = Book.EarnOnStay(b);
                        b.PointsEarned = pts;
                        Book.AddPoints(site, g, pts, "earn", b.Code, "Stay " + b.Code, c.U.Id);
                        string after = Ui.GradeOf(g.Lifetime).Name;
                        if (after != before)
                        {
                            gradeMsg = g.First + " is now " + after + ".";
                        }
                    }
                    HotelRoom? r = site.Rooms.FirstOrDefault(x => x.No == b.Room);
                    if (r != null)
                    {
                        r.Status = "dirty";
                        r.Changed = DateTime.UtcNow;
                        r.By = c.U.Id;
                    }
                    log = b.Code + " · room " + b.Room + (pts > 0 ? " · +" + pts + " pts" : string.Empty);
                    await Store.SaveSite(site, c.Sbx);
                }
            }
            if (err != string.Empty)
            {
                return await Refuse(response, c, "check-out", id, err);
            }
            await Store.Log(c.Sbx, c.U.Id, "check-out", log, true);
            SiteData fresh = await Store.LoadSite(c.Sbx);
            Chrome(response, c, fresh);
            response.SetElementContents("fd-mbody", "<div class=\"fd-result\"><h2>Checked out</h2><p>" + Ui.Enc(guest) + " &middot; " + (taken > 0 ? Ui.Cash(taken) + (GetDataValue("pay") == "cash" ? " paid in cash" : " charged") : charged < 0 ? Ui.Cash(-charged) + " refunded" : charged > 0 ? Ui.Cash(charged) + " taken from the deposit" : "nothing due") + (returned > 0 ? " &middot; deposit " + Ui.Cash(returned) + (depBy == "cash" ? " cash returned" : " hold released") : string.Empty) + " (demo)</p>" + (pts > 0 ? "<b class=\"fd-big\">+" + Ui.Num(pts) + " pts</b><p>Added to the guest's SkyHotel Circle account.</p>" : "<p class=\"fd-hint\">Not a member, so no points were added.</p>") + (gradeMsg != string.Empty ? "<p class=\"fd-ok-box\">" + Ui.Enc(gradeMsg) + "</p>" : string.Empty) + "<p class=\"fd-hint\">The room is now marked Dirty for housekeeping.</p><button type=\"button\" class=\"fd-btn\" onclick=\"AdminFrontDeskJs.closeModal()\">Done</button></div>");
            response.ExecuteScript("AdminFrontDeskJs.done();");
            return response;
        }
    }
}
