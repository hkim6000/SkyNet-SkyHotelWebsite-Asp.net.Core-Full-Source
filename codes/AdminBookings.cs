using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyHotel.Core;
using SkyHotel.Models;
using SkyNet;

namespace SkyHotel.codes
{
    public class AdminBookings : WebPage
    {
        private const string AreaKey = "bookings";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Bookings");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--bk-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("bk-menu", Auth.Menu("bk", "AdminBookings", c, site));
            response.SetElementContents("bk-who", Auth.Who("bk", c));
            response.SetElementContents("bk-demo", Auth.DemoPill("bk", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminBookingsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"bk-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"bk-panel\"><p>You don't have access to this area. Ask the general manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminBookingsJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminBookingsJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("bk-main", NoAccess("Bookings"));
                return response;
            }
            response.SetElementContents("bk-main", await MainHtml(c, site));
            response.ExecuteScript("AdminBookingsJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"bk-tr bk-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private const int Size = 20;

        private static readonly (string Key, string Name)[] Statuses = { ("", "All statuses"), ("booked", "Booked"), ("inhouse", "In house"), ("out", "Checked out"), ("cancelled", "Cancelled"), ("noshow", "No-show") };

        private static async Task<string> MainHtml(Ctx c, SiteData site)
        {
            StringBuilder sb = new StringBuilder("<div class=\"bk-ptop\"><div><h1>Bookings</h1><p>Every reservation, from the website, phone and travel agents.</p></div></div>");
            sb.Append("<div class=\"bk-bar\"><label>Search<input type=\"search\" id=\"bk-q\" list=\"bk-qlist\" placeholder=\"Code, guest, email or phone\" maxlength=\"60\" oninput=\"AdminBookingsJs.search()\"></label>" + Ui.DataList("bk-qlist", site.Guests.Where(g => site.Bookings.Any(b => b.Guest == g.Id && b.In > DateTime.UtcNow.AddDays(-30))).Select(g => Ui.GuestName(g))));
            sb.Append("<label>Status<select id=\"bk-bst\" onchange=\"AdminBookingsJs.list(1)\">" + Ui.Opts(Statuses.Select(x => (x.Key, x.Name)), string.Empty) + "</select></label>");
            sb.Append("<label>Room type<select id=\"bk-btype\" onchange=\"AdminBookingsJs.list(1)\"><option value=\"\">All room types</option>" + Ui.Opts(site.Types.Select(t => (t.Key, t.Name)), string.Empty) + "</select></label>");
            sb.Append("<label>Source<select id=\"bk-bsrc\" onchange=\"AdminBookingsJs.list(1)\">" + Ui.Opts(new[] { ("", "All sources"), ("web", "Website"), ("phone", "Phone"), ("agency", "Agency"), ("walk-in", "Walk-in") }, string.Empty) + "</select></label>");
            sb.Append("<label>Sort<select id=\"bk-bsort\" onchange=\"AdminBookingsJs.list(1)\">" + Ui.Opts(new[] { ("in", "Check-in, soonest"), ("in-desc", "Check-in, latest"), ("made", "Newest bookings"), ("total", "Highest total") }, "in") + "</select></label></div>");
            sb.Append("<div class=\"bk-bar\" id=\"bk-perbar\">" + Ui.PeriodBar("bk", "AdminBookingsJs.per", "30", string.Empty, string.Empty, true) + "</div>");
            sb.Append("<div class=\"bk-tbl bk-bktbl\" id=\"bk-list\"></div><div id=\"bk-pager\"></div>");
            if (Access.Can(c.U, AreaKey, 'E'))
            {
                sb.Replace("<p>Every reservation, from the website, phone and travel agents.</p></div></div>", "<p>Every reservation, from the website, phone and travel agents.</p></div><div class=\"bk-acts\"><button type=\"button\" class=\"bk-btn bk-btn-accent\" onclick=\"AdminBookingsJs.newForm('phone')\">New phone booking</button><button type=\"button\" class=\"bk-btn bk-btn-ghost\" onclick=\"AdminBookingsJs.newForm('agency')\">New agency booking</button></div></div>");
            }
            return sb.ToString();
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminBookingsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Dictionary<string, Guest> gm = Ui.GuestMap(site);
            string st = GetDataValue("bst");
            string type = Ui.Clip(GetDataValue("btype"), 30);
            string src = Ui.Clip(GetDataValue("bsrc"), 20);
            string term = Ui.Clip(GetDataValue("term"), 60);
            string per = Per(GetDataValue("per"));
            (DateTime? from, DateTime? to) = Ui.Period(per, GetDataValue("pfrom"), GetDataValue("pto"), true);
            IEnumerable<Booking> q = site.Bookings;
            if (st != string.Empty)
            {
                q = q.Where(b => b.Status == st);
            }
            if (type != string.Empty)
            {
                q = q.Where(b => b.Type == type);
            }
            if (src != string.Empty)
            {
                q = q.Where(b => b.Source == src);
            }
            if (from != null || to != null)
            {
                q = q.Where(b => Ui.InPeriod(b.In, from, to));
            }
            if (term != string.Empty)
            {
                string digits = Regex.Replace(term, "[^0-9]", string.Empty);
                q = q.Where(b => b.Code.Contains(term, StringComparison.OrdinalIgnoreCase) || b.Agency.Contains(term, StringComparison.OrdinalIgnoreCase) || (gm.TryGetValue(b.Guest, out Guest? g) && (Ui.GuestName(g).Contains(term, StringComparison.OrdinalIgnoreCase) || g.Email.Contains(term, StringComparison.OrdinalIgnoreCase) || (digits.Length >= 3 && Regex.Replace(g.Phone, "[^0-9]", string.Empty).Contains(digits, StringComparison.Ordinal)))));
            }
            q = GetDataValue("bsort") switch
            {
                "in-desc" => q.OrderByDescending(b => b.In),
                "made" => q.OrderByDescending(b => b.Created),
                "total" => q.OrderByDescending(b => b.Total),
                _ => q.OrderBy(b => b.In).ThenBy(b => b.Code)
            };
            List<Booking> all = q.ToList();
            int page = Ui.PageOf(GetDataValue("pg"), Size, all.Count);
            StringBuilder sb = new StringBuilder(Head(new[] { ("Code", string.Empty), ("Guest", string.Empty), ("Room", string.Empty), ("Stay", string.Empty), ("Total", "bk-num"), ("Status", string.Empty), ("Source", string.Empty), (string.Empty, string.Empty) }));
            if (all.Count == 0)
            {
                sb.Append("<div class=\"bk-tr\"><span class=\"bk-none bk-wide\">No bookings match.</span></div>");
            }
            foreach (Booking b in all.Skip((page - 1) * Size).Take(Size))
            {
                Guest? g = gm.TryGetValue(b.Guest, out Guest? x) ? x : null;
                sb.Append("<div class=\"bk-tr\"><span data-l=\"Code\" class=\"bk-code\">" + Ui.Enc(b.Code) + (b.Promo != string.Empty ? "<small class=\"bk-pcode\">" + Ui.Enc(b.Promo) + "</small>" : string.Empty) + "</span><span data-l=\"Guest\"><b>" + Ui.Enc(Ui.GuestName(g)) + "</b><small>" + (g != null && g.Member ? Ui.GradeOf(g.Lifetime).Name + " member" : Ui.Enc(g?.Email)) + "</small>" + (g != null && g.Phone != string.Empty ? "<small><a href=\"tel:" + Ui.Attr(Regex.Replace(g.Phone, "[^0-9+]", string.Empty)) + "\">" + Ui.Enc(g.Phone) + "</a></small>" : string.Empty) + "</span><span data-l=\"Room\">" + Ui.Enc(Ui.TypeName(site, b.Type)) + "<small>" + (b.Room != string.Empty ? "Room " + Ui.Enc(b.Room) : "No room yet") + "</small></span><span data-l=\"Stay\">" + b.In.ToString("d MMM", Ui.Inv) + " &rarr; " + b.Out.ToString("d MMM yy", Ui.Inv) + "<small>" + Book.Nights(b.In, b.Out) + " nights &middot; " + (b.Adults + b.Children) + " guests</small></span><span data-l=\"Total\" class=\"bk-num\">" + Ui.Cash(b.Total) + "</span><span data-l=\"Status\">" + Auth.StatusPill("bk", b) + "</span><span data-l=\"Source\">" + Ui.Enc(b.Source) + (b.Source == "agency" && b.Agency != string.Empty ? "<small>" + Ui.Enc(b.Agency) + "</small>" : string.Empty) + (b.PayAtFront && Book.Holds(b) ? "<small>pay at desk</small>" : string.Empty) + "</span><span><button type=\"button\" class=\"bk-btn bk-btn-sm bk-btn-ghost\" onclick=\"AdminBookingsJs.open('" + b.Id + "')\">Open</button></span></div>");
            }
            response.SetElementContents("bk-list", sb.ToString());
            response.SetElementContents("bk-pager", Ui.Pager("bk", page, Size, all.Count, "booking", "bookings", "AdminBookingsJs.list"));
            response.SetElementContents("bk-perbar", Ui.PeriodBar("bk", "AdminBookingsJs.per", per, GetDataValue("pfrom"), GetDataValue("pto"), true));
            return response;
        }

        private static string Detail(SiteData site, Booking b, Ctx c, Dictionary<string, Staff> staff)
        {
            Guest? g = site.Guests.FirstOrDefault(x => x.Id == b.Guest);
            bool edit = Access.Can(c.U, AreaKey, 'E');
            StringBuilder sb = new StringBuilder("<h2>Booking <span class=\"bk-code\">" + Ui.Enc(b.Code) + "</span> " + Auth.StatusPill("bk", b) + "</h2><div class=\"bk-dgrid\"><div><dl class=\"bk-dl\">");
            sb.Append("<dt>Guest</dt><dd><b>" + Ui.Enc(Ui.GuestName(g)) + "</b>" + (g != null && g.Member ? " " + Ui.GradePill("bk", g) : string.Empty) + (g != null && g.Vip ? "<span class=\"bk-vip\">VIP</span>" : string.Empty) + "<br><small>" + Ui.Enc(g?.Email) + " &middot; " + Ui.Enc(g?.Phone) + "</small>" + (g != null && g.Address != string.Empty ? "<br><small>" + Ui.Enc(g.Address) + "</small>" : string.Empty) + (g != null && (g.Country != string.Empty || g.Passport != string.Empty) ? "<br><small>" + Ui.Enc(g.Country) + (g.Passport != string.Empty ? " &middot; passport " + Ui.MaskPassport(g.Passport) : string.Empty) + "</small>" : string.Empty) + "</dd>");
            sb.Append("<dt>Stay</dt><dd>" + Ui.Day(b.In) + " &rarr; " + Ui.Day(b.Out) + " &middot; " + Book.Nights(b.In, b.Out) + " nights</dd><dt>Room</dt><dd>" + Ui.Enc(Ui.TypeName(site, b.Type)) + (b.Room != string.Empty ? " &middot; room " + Ui.Enc(b.Room) : " &middot; not assigned") + "</dd><dt>Guests</dt><dd>" + b.Adults + " adults" + (b.Children > 0 ? ", " + b.Children + " children" : string.Empty) + "</dd><dt>Rate</dt><dd>" + Book.PlanName(b.Plan) + (b.Promo != string.Empty ? " &middot; code " + Ui.Enc(b.Promo) : string.Empty) + "</dd><dt>Source</dt><dd>" + Ui.Enc(b.Source) + (b.Source == "agency" && b.Agency != string.Empty ? " &middot; " + Ui.Enc(b.Agency) + (b.AgencyRef != string.Empty ? " (ref " + Ui.Enc(b.AgencyRef) + ")" : string.Empty) : string.Empty) + " &middot; booked " + b.Created.ToString("d MMM yyyy, HH:mm", Ui.Inv) + (b.By != string.Empty && staff.TryGetValue(b.By, out Staff? taker) ? " by " + Ui.Enc(taker.Name) : string.Empty) + "</dd><dt>Check-out time</dt><dd>" + Ui.Enc(Ui.OutTime(site, b)) + (b.LateOut != string.Empty ? " <span class=\"bk-late\">Late checkout</span>" : string.Empty) + (b.LateOut == string.Empty && Ui.LateOk(g) ? Ui.LateBadge("bk", g) : string.Empty) + "</dd>");
            if (b.Plate != string.Empty || b.Valet != string.Empty)
            {
                sb.Append("<dt>Car</dt><dd>" + (b.Plate != string.Empty ? Ui.Enc(b.Plate) : "&ndash;") + (b.Valet != string.Empty ? " &middot; valet ticket " + Ui.Enc(b.Valet) : string.Empty) + "</dd>");
            }
            if (b.Arrival != string.Empty)
            {
                sb.Append("<dt>Arrival</dt><dd>around " + Ui.Enc(b.Arrival) + "</dd>");
            }
            if (b.Requests != string.Empty)
            {
                sb.Append("<dt>Requests</dt><dd>" + Ui.Enc(b.Requests) + "</dd>");
            }
            if (b.CheckedIn != null)
            {
                sb.Append("<dt>Checked in</dt><dd>" + b.CheckedIn.Value.ToString("d MMM, HH:mm", Ui.Inv) + "</dd>");
            }
            if (b.CheckedOut != null)
            {
                sb.Append("<dt>Checked out</dt><dd>" + b.CheckedOut.Value.ToString("d MMM, HH:mm", Ui.Inv) + (b.PointsEarned > 0 ? " &middot; +" + Ui.Num(b.PointsEarned) + " pts" : string.Empty) + "</dd>");
            }
            if (b.Cancelled != null)
            {
                sb.Append("<dt>Cancelled</dt><dd>" + b.Cancelled.Value.ToString("d MMM yyyy, HH:mm", Ui.Inv) + " &middot; refund " + Ui.Cash(b.Refund) + "</dd>");
            }
            sb.Append("</dl><h3>Notes for staff</h3><textarea id=\"bk-bnote\" maxlength=\"500\"" + (edit ? string.Empty : " readonly") + ">" + Ui.Enc(b.Note) + "</textarea>" + (edit ? "<div class=\"bk-acts\" style=\"margin-top:8px\"><button type=\"button\" class=\"bk-btn bk-btn-sm bk-btn-ghost\" onclick=\"AdminBookingsJs.note('" + b.Id + "')\">Save note</button></div>" : string.Empty) + "</div><div><h3 style=\"margin-top:0\">Price</h3><dl class=\"bk-lines\"><dt>Room</dt><dd>" + Ui.Cash(b.RoomTotal) + "</dd>");
            if (b.MemberOff > 0)
            {
                sb.Append("<dt>Member discount</dt><dd>&minus;" + Ui.Cash(b.MemberOff) + "</dd>");
            }
            if (b.Discount > 0)
            {
                sb.Append("<dt>Code " + Ui.Enc(b.Promo) + "</dt><dd>&minus;" + Ui.Cash(b.Discount) + "</dd>");
            }
            if (b.Breakfast > 0)
            {
                sb.Append("<dt>Breakfast</dt><dd>" + Ui.Cash(b.Breakfast) + "</dd>");
            }
            if (b.PointsValue > 0)
            {
                sb.Append("<dt>" + Ui.Num(b.PointsUsed) + " points</dt><dd>&minus;" + Ui.Cash(b.PointsValue) + "</dd>");
            }
            sb.Append("<dt>Tax</dt><dd>" + Ui.Cash(b.Tax) + "</dd><dt class=\"bk-tot\">Total</dt><dd class=\"bk-tot\">" + Ui.Cash(b.Total) + "</dd>");
            foreach (Charge x in b.Extras)
            {
                sb.Append("<dt>" + Ui.Enc(x.What) + "</dt><dd>" + Ui.Cash(x.Amount) + "</dd>");
            }
            sb.Append("<dt>Paid</dt><dd>" + Ui.Cash(b.Paid) + "</dd>" + (Book.Holds(b) && Book.Balance(site, b) > 0 ? "<dt class=\"bk-tot\">Due at the desk</dt><dd class=\"bk-tot\">" + Ui.Cash(Book.Balance(site, b)) + "</dd>" : string.Empty) + "</dl>" + (b.PayAtFront ? "<p class=\"bk-paynote\" style=\"margin-top:8px\">Pays at the front desk" + (b.PaidBy != string.Empty ? " &middot; paid by " + (b.PaidBy == "cash" ? "cash" : "card") : string.Empty) + "</p>" : "<p class=\"bk-hint\" style=\"margin-top:8px\">" + Ui.Enc(b.Card) + " &bull;&bull;&bull;&bull; " + Ui.Enc(b.Last4) + " (demo card)" + (b.PaidBy == "cash" ? " &middot; balance paid in cash" : string.Empty) + "</p>") + (b.Deposit > 0 ? "<p class=\"bk-hint\">Deposit " + Ui.Cash(b.Deposit) + " (" + (b.DepositBy == "cash" ? "cash" : "card hold") + ")" + (b.Status == "out" ? " &middot; " + Ui.Cash(b.DepositBack) + (b.DepositBy == "cash" ? " returned" : " released") : " &middot; held") + "</p>" : string.Empty));
            if (b.Status == "booked")
            {
                decimal refund = Book.RefundFor(site, b);
                if (edit)
                {
                    sb.Append("<div class=\"bk-acts\" style=\"margin-top:14px\"><button type=\"button\" class=\"bk-btn bk-btn-sm\" onclick=\"AdminBookingsJs.editForm('" + b.Id + "')\">Change dates or room</button>" + (b.In.Date < DateTime.UtcNow.Date ? "<button type=\"button\" class=\"bk-btn bk-btn-sm bk-btn-danger\" onclick=\"AdminBookingsJs.noShow('" + b.Id + "')\">No-show</button>" : string.Empty) + "</div>");
                }
                if (Access.Can(c.U, AreaKey, 'X'))
                {
                    sb.Append(b.PayAtFront ? "<div class=\"bk-cbox\"><b>Cancel booking</b><span class=\"bk-hint\">Pays at the front desk: nothing was charged, so there is nothing to refund.</span><div><button type=\"button\" class=\"bk-btn bk-btn-sm bk-btn-danger\" onclick=\"AdminBookingsJs.cancel('" + b.Id + "','" + Ui.Js(Ui.Cash(0)) + "')\">Cancel booking</button></div></div>" : "<div class=\"bk-cbox\"><b>Cancel booking</b><span class=\"bk-hint\">" + (b.Plan == "nonref" ? "Advance saver: no refund by policy." : Book.FreeCancel(site, b) ? "Inside the free cancellation window: full refund." : "Past the free window: first night is kept.") + " Refund " + Ui.Cash(refund) + ".</span><label class=\"bk-chk\"><input type=\"checkbox\" id=\"bk-waive\"> Waive the fee and refund " + Ui.Cash(b.Paid) + "</label><div><button type=\"button\" class=\"bk-btn bk-btn-sm bk-btn-danger\" onclick=\"AdminBookingsJs.cancel('" + b.Id + "','" + Ui.Js(Ui.Cash(refund)) + "')\">Cancel booking</button></div></div>");
                }
            }
            sb.Append("</div></div><div class=\"bk-mfoot\">" + (b.Status == "booked" || b.Status == "inhouse" ? "<a class=\"bk-btn bk-btn-ghost\" href=\"AdminFrontDesk" + (b.Status == "inhouse" ? "?tab=in" : string.Empty) + "\">Front desk</a>" : string.Empty) + "<button type=\"button\" class=\"bk-btn\" onclick=\"AdminBookingsJs.closeModal()\">Close</button></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Open()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminBookingsJs.expired();");
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
            response.SetElementContents("bk-mbody", Detail(site, b, c, Ui.StaffMap(await Store.LoadStaff(c.Sbx))));
            response.ExecuteScript("AdminBookingsJs.openModal();");
            return response;
        }

        public async Task<ApiResponse> Note()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminBookingsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "note", GetDataValue("bid"), "You can't edit bookings.");
            }
            string code = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Booking? b = site.Bookings.FirstOrDefault(x => x.Id == GetDataValue("bid"));
                if (b == null)
                {
                    return response;
                }
                b.Note = Ui.Clip(GetDataValue("bnote"), 500);
                code = b.Code;
                await Store.SaveSite(site, c.Sbx);
            }
            await Store.Log(c.Sbx, c.U.Id, "note", code, true);
            response.ExecuteScript("AdminBookingsJs.toast('Note saved');");
            return response;
        }

        public async Task<ApiResponse> EditForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminBookingsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "change", GetDataValue("bid"), "You can't change bookings.");
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Booking? b = site.Bookings.FirstOrDefault(x => x.Id == GetDataValue("bid") && x.Status == "booked");
            if (b == null)
            {
                return response;
            }
            StringBuilder sb = new StringBuilder("<h2>Change booking <span class=\"bk-code\">" + Ui.Enc(b.Code) + "</span></h2><div class=\"bk-form\"><div class=\"bk-f2\"><label class=\"bk-field\"><span>Check-in</span><input type=\"date\" id=\"bk-ein\" value=\"" + Ui.Ymd(b.In) + "\" min=\"" + Ui.Ymd(DateTime.UtcNow.Date) + "\"></label><label class=\"bk-field\"><span>Check-out</span><input type=\"date\" id=\"bk-eout\" value=\"" + Ui.Ymd(b.Out) + "\"></label></div><div class=\"bk-f3\"><label class=\"bk-field\"><span>Room type</span><select id=\"bk-etype\">" + Ui.Opts(site.Types.Where(t => t.Active).Select(t => (t.Key, t.Name)), b.Type) + "</select></label><label class=\"bk-field\"><span>Adults</span><select id=\"bk-ead\">" + Ui.Opts(new[] { ("1", "1"), ("2", "2"), ("3", "3"), ("4", "4") }, b.Adults.ToString(Ui.Inv)) + "</select></label><label class=\"bk-field\"><span>Children</span><select id=\"bk-ech\">" + Ui.Opts(new[] { ("0", "0"), ("1", "1"), ("2", "2"), ("3", "3") }, b.Children.ToString(Ui.Inv)) + "</select></label></div><div id=\"bk-eprev\" class=\"bk-info-box\">Change anything and press Check price.</div></div><div class=\"bk-mfoot\"><button type=\"button\" class=\"bk-btn bk-btn-ghost\" onclick=\"AdminBookingsJs.open('" + b.Id + "')\">Back</button><button type=\"button\" class=\"bk-btn bk-btn-ghost\" onclick=\"AdminBookingsJs.reprice('" + b.Id + "',false)\">Check price</button><button type=\"button\" class=\"bk-btn bk-btn-accent\" onclick=\"AdminBookingsJs.reprice('" + b.Id + "',true)\">Save changes</button></div>");
            response.SetElementContents("bk-mbody", sb.ToString());
            return response;
        }

        public async Task<ApiResponse> Change()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminBookingsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "change", GetDataValue("bid"), "You can't change bookings.");
            }
            DateTime? din = Ui.ParseDay(GetDataValue("ein"));
            DateTime? dout = Ui.ParseDay(GetDataValue("eout"));
            int ad = int.TryParse(GetDataValue("ead"), out int a) ? Math.Clamp(a, 1, 4) : 2;
            int ch = int.TryParse(GetDataValue("ech"), out int k) ? Math.Clamp(k, 0, 3) : 0;
            string type = Ui.Clip(GetDataValue("etype"), 30);
            bool save = GetDataValue("save") == "1";
            string err = Book.CheckDates(din, dout);
            string msg = string.Empty;
            string code = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Booking? b = site.Bookings.FirstOrDefault(x => x.Id == GetDataValue("bid") && x.Status == "booked");
                if (b == null)
                {
                    return response;
                }
                code = b.Code;
                Guest? g = site.Guests.FirstOrDefault(x => x.Id == b.Guest);
                if (err == string.Empty && Book.Available(site, type, din!.Value, dout!.Value, b.Id) <= 0)
                {
                    err = Ui.TypeName(site, type) + " is full on some of those nights.";
                }
                Quote? q = null;
                if (err == string.Empty)
                {
                    q = Book.Price(site, type, din!.Value, dout!.Value, ad, ch, b.Plan, b.Promo, g != null && g.Member ? g : null, b.PointsUsed);
                    if (q.Error != string.Empty)
                    {
                        err = q.Error;
                    }
                }
                if (err != string.Empty || q == null)
                {
                    response.SetElementContents("bk-eprev", "<b>" + Ui.Enc(err) + "</b>");
                    return response;
                }
                decimal diff = q.Total - b.Total;
                string lost = b.Promo != string.Empty && q.Offer == null ? " Code " + b.Promo + " no longer applies: " + q.PromoNote : string.Empty;
                msg = "New total " + Ui.Cash(q.Total) + " (" + (diff >= 0 ? "+" : "-") + Ui.Cash(Math.Abs(diff)).Replace("-", string.Empty) + (b.PayAtFront ? " · due at the front desk" : diff > 0 ? " to charge" : diff < 0 ? " to refund" : string.Empty) + ")." + lost;
                if (save)
                {
                    bool keepRoom = b.Room != string.Empty && type == b.Type && Book.RoomFree(site, b.Room, din!.Value, dout!.Value, b.Id);
                    b.In = din!.Value;
                    b.Out = dout!.Value;
                    b.Adults = ad;
                    b.Children = ch;
                    b.Type = type;
                    b.Room = keepRoom ? b.Room : string.Empty;
                    b.RoomTotal = q.RoomSub;
                    b.MemberOff = q.MemberOff;
                    b.Discount = q.PromoOff;
                    if (q.Offer == null && b.Promo != string.Empty)
                    {
                        b.Promo = string.Empty;
                        b.Bonus = 1;
                    }
                    b.Breakfast = q.Breakfast;
                    b.Tax = q.Tax;
                    if (!b.PayAtFront)
                    {
                        b.Paid += diff;
                    }
                    b.Total = q.Total;
                    await Store.SaveSite(site, c.Sbx);
                }
                else
                {
                    response.SetElementContents("bk-eprev", Ui.Enc(msg));
                    return response;
                }
            }
            await Store.Log(c.Sbx, c.U.Id, "change", code + " · " + msg, true);
            SiteData fresh = await Store.LoadSite(c.Sbx);
            Booking? nb = fresh.Bookings.First(x => x.Id == GetDataValue("bid"));
            response.SetElementContents("bk-mbody", Detail(fresh, nb, c, Ui.StaffMap(await Store.LoadStaff(c.Sbx))));
            response.ExecuteScript("AdminBookingsJs.list(1);AdminBookingsJs.toast('" + Ui.Js("Booking changed. " + msg) + "');");
            return response;
        }

        private static readonly (string Value, string Text)[] ArrivalOpts = { ("", "Not known"), ("12:00", "12:00 - 15:00"), ("15:00", "15:00 - 18:00"), ("18:00", "18:00 - 21:00"), ("21:00", "After 21:00") };

        public async Task<ApiResponse> NewForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminBookingsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "booking", GetDataValue("nsrc"), "You can view bookings but not take new ones.");
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            string src = GetDataValue("nsrc") == "agency" ? "agency" : "phone";
            DateTime today = DateTime.UtcNow.Date;
            StringBuilder sb = new StringBuilder("<h2>New booking</h2><div class=\"bk-form\">");
            sb.Append("<div class=\"bk-nsec\"><h3>Booked by</h3><div class=\"bk-nsrc\"><label><input type=\"radio\" name=\"bk-nsrcr\" value=\"phone\" onchange=\"AdminBookingsJs.srcPicked()\"" + (src == "phone" ? " checked" : string.Empty) + "> Phone</label><label><input type=\"radio\" name=\"bk-nsrcr\" value=\"agency\" onchange=\"AdminBookingsJs.srcPicked()\"" + (src == "agency" ? " checked" : string.Empty) + "> Agency</label></div><div class=\"bk-f2\" id=\"bk-nagbox\"" + (src == "agency" ? string.Empty : " hidden") + "><label class=\"bk-field\"><span>Agency</span><input type=\"text\" id=\"bk-nagency\" list=\"bk-naglist\" maxlength=\"60\" placeholder=\"e.g. Bluewater Travel\"></label><label class=\"bk-field\"><span>Agency reference</span><input type=\"text\" id=\"bk-nagref\" maxlength=\"30\"></label></div>" + Ui.DataList("bk-naglist", site.Bookings.Select(b => b.Agency).Where(a => a != string.Empty).Distinct()) + "</div>");
            sb.Append("<div class=\"bk-nsec\"><h3>Guest</h3><label class=\"bk-field\"><span>Find a guest or member</span><input type=\"search\" id=\"bk-nfind\" maxlength=\"60\" placeholder=\"Name, email, phone or member no.\" oninput=\"AdminBookingsJs.findGuest()\" autocomplete=\"off\"></label><div class=\"bk-found\" id=\"bk-nfound\"></div><div class=\"bk-picked\" id=\"bk-npicked\"></div><input type=\"hidden\" id=\"bk-ngid\"><div class=\"bk-f2\"><label class=\"bk-field\"><span>First name</span><input type=\"text\" id=\"bk-nfirst\" maxlength=\"40\"></label><label class=\"bk-field\"><span>Last name</span><input type=\"text\" id=\"bk-nlast\" maxlength=\"40\"></label></div><div class=\"bk-f2\"><label class=\"bk-field\"><span>Email</span><input type=\"email\" id=\"bk-nmail\" maxlength=\"100\"></label><label class=\"bk-field\"><span>Phone</span><input type=\"tel\" id=\"bk-nphone\" maxlength=\"30\"></label></div><p class=\"bk-hint\">A new guest needs a last name and an email or phone number.</p></div>");
            sb.Append("<div class=\"bk-nsec\"><h3>Stay</h3><div class=\"bk-f2\"><label class=\"bk-field\"><span>Check-in</span><input type=\"date\" id=\"bk-nin\" min=\"" + Ui.Ymd(today) + "\" value=\"" + Ui.Ymd(today.AddDays(1)) + "\"></label><label class=\"bk-field\"><span>Check-out</span><input type=\"date\" id=\"bk-nout\" min=\"" + Ui.Ymd(today.AddDays(1)) + "\" value=\"" + Ui.Ymd(today.AddDays(3)) + "\"></label></div><div class=\"bk-f2\"><label class=\"bk-field\"><span>Room type</span><select id=\"bk-ntype\" onchange=\"AdminBookingsJs.typeChanged()\">" + Ui.Opts(site.Types.Where(t => t.Active).OrderBy(t => t.Order).Select(t => (t.Key, t.Name)), string.Empty) + "</select></label><label class=\"bk-field\"><span>Rate</span><select id=\"bk-nplan\">" + Ui.Opts(Book.Plans.Where(x => x.Key != "nonref").Select(x => (x.Key, x.Name)), "flex") + "</select></label></div><div class=\"bk-f4\"><label class=\"bk-field\"><span>Adults</span><select id=\"bk-nad\">" + Ui.Opts(new[] { ("1", "1"), ("2", "2"), ("3", "3"), ("4", "4") }, "2") + "</select></label><label class=\"bk-field\"><span>Children</span><select id=\"bk-nch\">" + Ui.Opts(new[] { ("0", "0"), ("1", "1"), ("2", "2"), ("3", "3") }, "0") + "</select></label><div class=\"bk-npw\"><label class=\"bk-field\"><span>Promo code</span><input type=\"text\" id=\"bk-npromo\" maxlength=\"20\" style=\"text-transform:uppercase\" autocomplete=\"off\" placeholder=\"Pick or type\" onfocus=\"AdminBookingsJs.promoOpen()\" onclick=\"AdminBookingsJs.promoOpen()\" oninput=\"AdminBookingsJs.promoFilter()\" onblur=\"AdminBookingsJs.promoClose()\"></label><div class=\"bk-nplist\" id=\"bk-nplist\" hidden></div></div><label class=\"bk-field\"><span>Arrival</span><select id=\"bk-narr\">" + Ui.Opts(ArrivalOpts, string.Empty) + "</select></label></div><label class=\"bk-field\"><span>Requests</span><input type=\"text\" id=\"bk-nreq\" maxlength=\"300\"></label></div>");
            sb.Append("<p class=\"bk-paynote\">Payment: pay at the front desk. Nothing is charged now; the full amount is due at check-out.</p><div id=\"bk-nprev\" class=\"bk-info-box\">Fill in the stay and press Check price.</div><em class=\"bk-err\" id=\"bk-e-new\"></em></div><div class=\"bk-mfoot\"><button type=\"button\" class=\"bk-btn bk-btn-ghost\" onclick=\"AdminBookingsJs.closeModal()\">Cancel</button><button type=\"button\" class=\"bk-btn bk-btn-ghost\" onclick=\"AdminBookingsJs.newPrice()\">Check price</button><button type=\"button\" class=\"bk-btn bk-btn-accent\" id=\"bk-nbtn\" onclick=\"AdminBookingsJs.create()\">Book</button></div>");
            response.SetElementContents("bk-mbody", sb.ToString());
            response.ExecuteScript("AdminBookingsJs.openModal();");
            return response;
        }

        public async Task<ApiResponse> FindGuest()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminBookingsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            string t = Ui.Clip(GetDataValue("nfind"), 60).Trim();
            string digits = Regex.Replace(t, "[^0-9]", string.Empty);
            StringBuilder sb = new StringBuilder();
            if (t.Length >= 2)
            {
                List<Guest> hits = site.Guests.Where(g => Ui.GuestName(g).Contains(t, StringComparison.OrdinalIgnoreCase) || g.Email.Contains(t, StringComparison.OrdinalIgnoreCase) || (g.MemberNo != string.Empty && g.MemberNo.Contains(t, StringComparison.OrdinalIgnoreCase)) || (digits.Length >= 4 && Regex.Replace(g.Phone, "[^0-9]", string.Empty).Contains(digits, StringComparison.Ordinal))).OrderByDescending(g => g.Member).ThenBy(g => g.Last).Take(6).ToList();
                foreach (Guest g in hits)
                {
                    string label = "<b>" + Ui.Enc(Ui.GuestName(g)) + "</b>" + (g.Member ? " " + Ui.GradePill("bk", g) + " " + Ui.Enc(g.MemberNo) : " Guest") + Ui.LateBadge("bk", g) + " <button type=\"button\" class=\"bk-btn bk-btn-sm bk-btn-ghost\" onclick=\"AdminBookingsJs.unpick()\">Clear</button>";
                    sb.Append("<button type=\"button\" onclick=\"AdminBookingsJs.pickGuest(this)\" data-id=\"" + Ui.Attr(g.Id) + "\" data-first=\"" + Ui.Attr(g.First) + "\" data-last=\"" + Ui.Attr(g.Last) + "\" data-mail=\"" + Ui.Attr(g.Email) + "\" data-phone=\"" + Ui.Attr(g.Phone) + "\" data-label=\"" + Ui.Attr(label) + "\"><b>" + Ui.Enc(Ui.GuestName(g)) + "</b>" + (g.Member ? " &middot; " + Ui.GradeOf(g.Lifetime).Name : string.Empty) + "<small>" + Ui.Enc(g.Email) + (g.Phone != string.Empty ? " &middot; " + Ui.Enc(g.Phone) : string.Empty) + "</small></button>");
                }
                if (hits.Count == 0)
                {
                    sb.Append("<span class=\"bk-hint\">No match. Fill in the details below for a new guest.</span>");
                }
            }
            response.SetElementContents("bk-nfound", sb.ToString());
            return response;
        }

        private sealed class NewIn
        {
            public string Src { get; set; } = "phone";
            public string Agency { get; set; } = string.Empty;
            public string AgencyRef { get; set; } = string.Empty;
            public string Gid { get; set; } = string.Empty;
            public string First { get; set; } = string.Empty;
            public string Last { get; set; } = string.Empty;
            public string Mail { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public DateTime? In { get; set; }
            public DateTime? Out { get; set; }
            public string Type { get; set; } = string.Empty;
            public string Plan { get; set; } = "flex";
            public int Adults { get; set; } = 2;
            public int Children { get; set; }
            public string Promo { get; set; } = string.Empty;
            public string Arrival { get; set; } = string.Empty;
            public string Requests { get; set; } = string.Empty;
        }

        private NewIn ReadNew()
        {
            return new NewIn
            {
                Src = GetDataValue("nsrc") == "agency" ? "agency" : "phone",
                Agency = Regex.Replace(Ui.Clip(GetDataValue("nagency"), 60), "\\s+", " ").Trim(),
                AgencyRef = Ui.Clip(GetDataValue("nagref"), 30).Trim(),
                Gid = Ui.Clip(GetDataValue("ngid"), 40),
                First = Regex.Replace(Ui.Clip(GetDataValue("nfirst"), 40), "\\s+", " ").Trim(),
                Last = Regex.Replace(Ui.Clip(GetDataValue("nlast"), 40), "\\s+", " ").Trim(),
                Mail = Ui.Clip(GetDataValue("nmail"), 100).Trim().ToLowerInvariant(),
                Phone = Ui.CleanPhone(GetDataValue("nphone")),
                In = Ui.ParseDay(GetDataValue("nin")),
                Out = Ui.ParseDay(GetDataValue("nout")),
                Type = Ui.Clip(GetDataValue("ntype"), 30),
                Plan = GetDataValue("nplan") == "breakfast" ? "breakfast" : "flex",
                Adults = int.TryParse(GetDataValue("nad"), out int a) ? Math.Clamp(a, 1, 4) : 2,
                Children = int.TryParse(GetDataValue("nch"), out int k) ? Math.Clamp(k, 0, 3) : 0,
                Promo = Regex.Replace(Ui.Clip(GetDataValue("npromo"), 20).ToUpperInvariant(), "[^A-Z0-9]", string.Empty),
                Arrival = ArrivalOpts.Any(x => x.Value == GetDataValue("narr")) ? GetDataValue("narr") : string.Empty,
                Requests = Ui.Clip(GetDataValue("nreq"), 300).Trim()
            };
        }

        private static (string Err, Quote? Q, Guest? Who) QuoteNew(SiteData site, NewIn n)
        {
            string err = Book.CheckDates(n.In, n.Out);
            Guest? who = n.Gid != string.Empty ? site.Guests.FirstOrDefault(g => g.Id == n.Gid) : null;
            if (who == null && n.Mail != string.Empty)
            {
                who = site.Guests.FirstOrDefault(g => g.Email.Equals(n.Mail, StringComparison.OrdinalIgnoreCase));
            }
            if (err != string.Empty)
            {
                return (err, null, who);
            }
            if (Book.Available(site, n.Type, n.In!.Value, n.Out!.Value) <= 0)
            {
                return (Ui.TypeName(site, n.Type) + " is full on some of those nights.", null, who);
            }
            Quote q = Book.Price(site, n.Type, n.In!.Value, n.Out!.Value, n.Adults, n.Children, n.Plan, n.Promo, who != null && who.Member ? who : null, 0);
            if (q.Error != string.Empty)
            {
                return (q.Error, null, who);
            }
            if (n.Promo != string.Empty && q.Offer == null)
            {
                return (q.PromoNote != string.Empty ? q.PromoNote : "That code isn't valid right now.", null, who);
            }
            return (string.Empty, q, who);
        }

        private static string PriceHtml(SiteData site, Quote q, Guest? who)
        {
            StringBuilder sb = new StringBuilder("<b>Total " + Ui.Cash(q.Total) + "</b> &middot; " + q.Nights + (q.Nights == 1 ? " night" : " nights") + " &middot; room " + Ui.Cash(q.RoomSub));
            if (q.MemberOff > 0)
            {
                sb.Append(" &middot; member &minus;" + Ui.Cash(q.MemberOff));
            }
            if (q.PromoOff > 0)
            {
                sb.Append(" &middot; code &minus;" + Ui.Cash(q.PromoOff));
            }
            if (q.Breakfast > 0 || q.FreeBreakfast)
            {
                sb.Append(" &middot; breakfast " + (q.FreeBreakfast ? "free" : Ui.Cash(q.Breakfast)));
            }
            sb.Append(" &middot; tax " + Ui.Cash(q.Tax) + (who != null && who.Member ? "<br>" + Ui.Enc(Ui.GuestName(who)) + " is " + Ui.GradeOf(who.Lifetime).Name + ": " + q.MemberPct + "% member rate, +" + Ui.Num(q.Earn) + " pts at check-out." : string.Empty));
            return sb.ToString();
        }

        public async Task<ApiResponse> NewPrice()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminBookingsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            (string err, Quote? q, Guest? who) = QuoteNew(site, ReadNew());
            response.SetElementContents("bk-nprev", err != string.Empty || q == null ? "<b>" + Ui.Enc(err) + "</b>" : PriceHtml(site, q, who));
            return response;
        }

        public async Task<ApiResponse> Create()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminBookingsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "booking", GetDataValue("nsrc"), "You can view bookings but not take new ones.");
            }
            NewIn n = ReadNew();
            string err = n.Src == "agency" && n.Agency.Length < 2 ? "Enter the agency name." : string.Empty;
            Booking? bk = null;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                (string qerr, Quote? q, Guest? who) = QuoteNew(site, n);
                if (err == string.Empty)
                {
                    err = qerr;
                }
                if (err == string.Empty && who == null && (n.Last.Length < 1 || n.First.Length < 1))
                {
                    err = "Enter the guest's first and last name.";
                }
                if (err == string.Empty && who == null && n.Mail == string.Empty && n.Phone == string.Empty)
                {
                    err = "Enter an email or phone number for the guest.";
                }
                if (err == string.Empty && n.Mail != string.Empty && !Regex.IsMatch(n.Mail, "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$"))
                {
                    err = "Check the email address.";
                }
                if (err == string.Empty && q != null)
                {
                    Guest g = who ?? new Guest { Id = "g" + Book.NewId(), Email = n.Mail, Created = DateTime.UtcNow };
                    if (who == null)
                    {
                        g.First = n.First;
                        g.Last = n.Last;
                        g.Phone = n.Phone;
                        site.Guests.Add(g);
                    }
                    else if (n.Phone != string.Empty && g.Phone == string.Empty)
                    {
                        g.Phone = n.Phone;
                    }
                    bk = new Booking
                    {
                        Id = Book.NewId(), Code = Book.NewCode(site), Guest = g.Id, Type = n.Type, In = n.In!.Value, Out = n.Out!.Value, Adults = n.Adults, Children = n.Children, Plan = n.Plan,
                        RoomTotal = q.RoomSub, MemberOff = q.MemberOff, Discount = q.PromoOff, Promo = q.Offer?.Code ?? string.Empty, Bonus = q.Offer?.Bonus ?? 1, Breakfast = q.Breakfast,
                        Tax = q.Tax, Total = q.Total, Paid = 0, PayAtFront = true, Status = "booked", Source = n.Src, Agency = n.Src == "agency" ? n.Agency : string.Empty,
                        AgencyRef = n.Src == "agency" ? n.AgencyRef : string.Empty, Requests = n.Requests, Arrival = n.Arrival, By = c.U.Id, Created = DateTime.UtcNow
                    };
                    site.Bookings.Add(bk);
                    if (q.Offer != null)
                    {
                        q.Offer.Used++;
                    }
                    await Store.SaveSite(site, c.Sbx);
                }
            }
            if (err != string.Empty || bk == null)
            {
                response.SetElementContents("bk-e-new", err);
                response.ExecuteScript("AdminBookingsJs.createFail();");
                return response;
            }
            await Store.Log(c.Sbx, c.U.Id, "booking", bk.Code + " · " + bk.Source + (bk.Agency != string.Empty ? " " + bk.Agency : string.Empty) + " · " + Ui.Cash(bk.Total) + " at desk", true);
            SiteData fresh = await Store.LoadSite(c.Sbx);
            Booking nb = fresh.Bookings.First(x => x.Id == bk.Id);
            response.SetElementContents("bk-mbody", Detail(fresh, nb, c, Ui.StaffMap(await Store.LoadStaff(c.Sbx))));
            response.ExecuteScript("AdminBookingsJs.list(1);AdminBookingsJs.toast('" + Ui.Js("Booked " + bk.Code + " · pay at front desk") + "');");
            return response;
        }

        public async Task<ApiResponse> PromoList()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminBookingsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            string type = Ui.Clip(GetDataValue("ntype"), 30);
            DateTime? din = Ui.ParseDay(GetDataValue("nin"));
            DateTime? dout = Ui.ParseDay(GetDataValue("nout"));
            int nights = din != null && dout != null && dout > din ? Book.Nights(din.Value, dout.Value) : 0;
            Guest? who = site.Guests.FirstOrDefault(g => g.Id == Ui.Clip(GetDataValue("ngid"), 40));
            bool member = who != null && who.Member;
            DateTime today = DateTime.UtcNow.Date;
            List<Offer> list = site.Offers.Where(o => o.Active && today >= o.From.Date && today <= o.To.Date && Book.Fits(o, type) && (!o.Members || member) && (nights == 0 || nights >= o.MinNights)).OrderByDescending(o => o.Pct).ThenByDescending(o => o.Bonus).ToList();
            StringBuilder sb = new StringBuilder();
            foreach (Offer o in list)
            {
                sb.Append("<button type=\"button\" class=\"bk-npi\" data-code=\"" + Ui.Attr(o.Code) + "\" onmousedown=\"event.preventDefault()\" onclick=\"AdminBookingsJs.pickPromo('" + Ui.Attr(Ui.Js(o.Code)) + "')\"><b>" + Ui.Enc(o.Code) + "</b><span>" + Ui.Enc(o.Title) + " &middot; " + (o.Pct > 0 ? o.Pct + "% off" : string.Empty) + (o.Pct > 0 && o.Bonus > 1 ? ", " : string.Empty) + (o.Bonus > 1 ? o.Bonus + "&times; points" : string.Empty) + "</span><small>" + Ui.Enc(Book.TypesText(site, o)) + (o.MinNights > 1 ? " &middot; " + o.MinNights + "+ nights" : string.Empty) + (o.Members ? " &middot; members" : string.Empty) + "</small></button>");
            }
            if (list.Count == 0)
            {
                sb.Append("<p class=\"bk-npnone\">No offers for " + Ui.Enc(Ui.TypeName(site, type)) + (nights > 0 ? " on these dates" : string.Empty) + ".</p>");
            }
            response.SetElementContents("bk-nplist", sb.ToString());
            response.ExecuteScript("AdminBookingsJs.promoShown();");
            return response;
        }

        public async Task<ApiResponse> Cancel()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminBookingsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'X'))
            {
                return await Refuse(response, c, "cancel", GetDataValue("bid"), "Cancelling needs the Cancel & refund right.");
            }
            bool waive = GetDataValue("waive") == "1";
            string log = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Booking? b = site.Bookings.FirstOrDefault(x => x.Id == GetDataValue("bid") && x.Status == "booked");
                if (b == null)
                {
                    return response;
                }
                decimal refund = b.PayAtFront ? 0 : waive ? b.Paid : Book.RefundFor(site, b);
                b.Status = "cancelled";
                b.Cancelled = DateTime.UtcNow;
                b.Refund = refund;
                b.Room = string.Empty;
                Guest? g = site.Guests.FirstOrDefault(x => x.Id == b.Guest);
                if (g != null && g.Member && b.PointsUsed > 0)
                {
                    Book.AddPoints(site, g, b.PointsUsed, "return", b.Code, "Returned from cancelled " + b.Code, c.U.Id);
                }
                log = b.Code + " · refund " + Ui.Cash(refund) + (waive ? " (fee waived)" : string.Empty);
                await Store.SaveSite(site, c.Sbx);
            }
            await Store.Log(c.Sbx, c.U.Id, "cancel", log, true);
            response.ExecuteScript("AdminBookingsJs.closeModal();AdminBookingsJs.list(1);AdminBookingsJs.toast('" + Ui.Js("Cancelled: " + log) + "');");
            return response;
        }

        public async Task<ApiResponse> NoShow()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminBookingsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "no-show", GetDataValue("bid"), "You can't change bookings.");
            }
            string code = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Booking? b = site.Bookings.FirstOrDefault(x => x.Id == GetDataValue("bid") && x.Status == "booked" && x.In.Date < DateTime.UtcNow.Date);
                if (b == null)
                {
                    return await Refuse(response, c, "no-show", GetDataValue("bid"), "Only past arrivals can be marked as no-show.");
                }
                b.Status = "noshow";
                b.Room = string.Empty;
                code = b.Code;
                await Store.SaveSite(site, c.Sbx);
            }
            await Store.Log(c.Sbx, c.U.Id, "no-show", code, true);
            response.ExecuteScript("AdminBookingsJs.closeModal();AdminBookingsJs.list(1);AdminBookingsJs.toast('Marked as no-show');");
            return response;
        }
    }
}
