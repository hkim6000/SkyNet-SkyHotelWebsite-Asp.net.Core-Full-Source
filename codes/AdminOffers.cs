using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyHotel.Core;
using SkyHotel.Models;
using SkyNet;

namespace SkyHotel.codes
{
    public class AdminOffers : WebPage
    {
        private const string AreaKey = "offers";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Offers");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--po-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("po-menu", Auth.Menu("po", "AdminOffers", c, site));
            response.SetElementContents("po-who", Auth.Who("po", c));
            response.SetElementContents("po-demo", Auth.DemoPill("po", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminOffersJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"po-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"po-panel\"><p>You don't have access to this area. Ask the general manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminOffersJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminOffersJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("po-main", NoAccess("Offers"));
                return response;
            }
            response.SetElementContents("po-main", await MainHtml(c, site));
            response.ExecuteScript("AdminOffersJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"po-tr po-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string Input(string label, string key, string value, string type = "text", string extra = "", bool ro = false)
        {
            return "<label class=\"po-field\"><span>" + label + "</span><input type=\"" + type + "\" data-k=\"" + key + "\" value=\"" + Ui.Attr(value) + "\" " + extra + (ro ? " readonly" : string.Empty) + "></label>";
        }

        private const int Size = 20;

        private static string State(Offer o)
        {
            DateTime today = DateTime.UtcNow.Date;
            return !o.Active ? "off" : today < o.From.Date ? "upcoming" : today > o.To.Date ? "expired" : "live";
        }

        private static async Task<string> MainHtml(Ctx c, SiteData site)
        {
            StringBuilder sb = new StringBuilder("<div class=\"po-ptop\"><div><h1>Offers</h1><p>Promo codes for the booking page. Members-only codes need a signed-in SkyHotel Circle member; bonus codes multiply the points a stay earns.</p></div>" + (Access.Can(c.U, AreaKey, 'E') ? "<div class=\"po-acts\"><button type=\"button\" class=\"po-btn po-btn-accent\" onclick=\"AdminOffersJs.form('')\">New offer</button></div>" : string.Empty) + "</div>");
            sb.Append("<div class=\"po-bar\"><label>Search<input type=\"search\" id=\"po-q\" list=\"po-qlist\" placeholder=\"Code or title\" maxlength=\"40\" oninput=\"AdminOffersJs.search()\"></label>" + Ui.DataList("po-qlist", site.Offers.Select(o => o.Code)) + "<label>Status<select id=\"po-ost\" onchange=\"AdminOffersJs.list(1)\">" + Ui.Opts(new[] { ("", "All"), ("live", "Running now"), ("upcoming", "Upcoming"), ("expired", "Expired"), ("off", "Switched off") }, string.Empty) + "</select></label><label>For<select id=\"po-oaud\" onchange=\"AdminOffersJs.list(1)\">" + Ui.Opts(new[] { ("", "Everyone and members"), ("public", "Everyone"), ("members", "Members only") }, string.Empty) + "</select></label></div><div class=\"po-tbl po-potbl\" id=\"po-list\"></div><div id=\"po-pager\"></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminOffersJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            string st = GetDataValue("ost");
            string aud = GetDataValue("oaud");
            string term = Ui.Clip(GetDataValue("term"), 40);
            List<Offer> all = site.Offers.Where(o => (st == string.Empty || State(o) == st) && (aud == string.Empty || o.Members == (aud == "members")) && (term == string.Empty || o.Code.Contains(term, StringComparison.OrdinalIgnoreCase) || o.Title.Contains(term, StringComparison.OrdinalIgnoreCase))).OrderBy(o => State(o) == "live" ? 0 : State(o) == "upcoming" ? 1 : 2).ThenBy(o => o.Code).ToList();
            int page = Ui.PageOf(GetDataValue("pg"), Size, all.Count);
            bool edit = Access.Can(c.U, AreaKey, 'E');
            StringBuilder sb = new StringBuilder(Head(new[] { ("Code", string.Empty), ("Offer", string.Empty), ("Rooms", string.Empty), ("Discount", string.Empty), ("Points", string.Empty), ("Valid", string.Empty), ("Used", string.Empty), ("Status", string.Empty), (string.Empty, string.Empty) }));
            if (all.Count == 0)
            {
                sb.Append("<div class=\"po-tr\"><span class=\"po-none po-wide\">No offers match.</span></div>");
            }
            foreach (Offer o in all.Skip((page - 1) * Size).Take(Size))
            {
                string state = State(o);
                sb.Append("<div class=\"po-tr\"><span data-l=\"Code\" class=\"po-code\">" + Ui.Enc(o.Code) + "</span><span data-l=\"Offer\"><b>" + Ui.Enc(o.Title) + "</b><small>" + (o.Members ? "Members only" : "Everyone") + (o.MinNights > 1 ? " &middot; " + o.MinNights + "+ nights" : string.Empty) + "</small></span><span data-l=\"Rooms\">" + (o.Types.Count == 0 ? "All room types" : "<small style=\"white-space:normal\">" + Ui.Enc(Book.TypesText(site, o)) + "</small>") + "</span><span data-l=\"Discount\">" + (o.Pct > 0 ? o.Pct + "%" : "&ndash;") + "</span><span data-l=\"Points\">" + (o.Bonus > 1 ? o.Bonus + "&times;" : "&ndash;") + "</span><span data-l=\"Valid\">" + o.From.ToString("d MMM", Ui.Inv) + " &rarr; " + o.To.ToString("d MMM yyyy", Ui.Inv) + "</span><span data-l=\"Used\">" + o.Used + "</span><span data-l=\"Status\"><span class=\"po-st po-st-" + state + "\">" + (state == "live" ? "Running" : state == "upcoming" ? "Upcoming" : state == "expired" ? "Expired" : "Off") + "</span></span><span><button type=\"button\" class=\"po-btn po-btn-sm po-btn-ghost\" onclick=\"AdminOffersJs.form('" + Ui.Attr(o.Code) + "')\">" + (edit ? "Edit" : "View") + "</button></span></div>");
            }
            response.SetElementContents("po-list", sb.ToString());
            response.SetElementContents("po-pager", Ui.Pager("po", page, Size, all.Count, "offer", "offers", "AdminOffersJs.list"));
            return response;
        }

        public async Task<ApiResponse> Form()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminOffersJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            string code = Ui.Clip(GetDataValue("ocode"), 20);
            Offer? o = site.Offers.FirstOrDefault(x => x.Code == code);
            bool ro = !Access.Can(c.U, AreaKey, 'E');
            DateTime today = DateTime.UtcNow.Date;
            StringBuilder sb = new StringBuilder("<h2>" + (o == null ? "New offer" : Ui.Enc(o.Code)) + "</h2><div class=\"po-form\"><div class=\"po-f2\">" + Input("Code", "ocode2", o?.Code ?? string.Empty, "text", "maxlength=\"20\" style=\"text-transform:uppercase\"", ro || o != null) + Input("Title", "otitle", o?.Title ?? string.Empty, "text", "maxlength=\"60\"", ro) + "</div><label class=\"po-field\"><span>Description</span><textarea data-k=\"otext\" rows=\"2\" maxlength=\"240\"" + (ro ? " readonly" : string.Empty) + ">" + Ui.Enc(o?.Text) + "</textarea></label><div class=\"po-f4\">" + Input("Discount (%)", "opct", (o?.Pct ?? 10).ToString(Ui.Inv), "number", "min=\"0\" max=\"60\"", ro) + "<label class=\"po-field\"><span>Points</span><select data-k=\"obonus\"" + (ro ? " disabled" : string.Empty) + ">" + Ui.Opts(new[] { ("1", "Normal"), ("2", "Double (2×)"), ("3", "Triple (3×)") }, (o?.Bonus ?? 1).ToString(Ui.Inv)) + "</select></label>" + Input("Minimum nights", "omin", (o?.MinNights ?? 1).ToString(Ui.Inv), "number", "min=\"1\" max=\"14\"", ro) + Input("Times used", "oused", (o?.Used ?? 0).ToString(Ui.Inv), "number", string.Empty, true) + "</div><div class=\"po-f2\">" + Input("Valid from", "ofrom", Ui.Ymd(o?.From ?? today), "date", string.Empty, ro) + Input("Valid to", "oto", Ui.Ymd(o?.To ?? today.AddDays(60)), "date", string.Empty, ro) + "</div><div class=\"po-f2\"><label class=\"po-chk\"><input type=\"checkbox\" data-k=\"omembers\"" + (o?.Members == true ? " checked" : string.Empty) + (ro ? " disabled" : string.Empty) + "> SkyHotel Circle members only</label><label class=\"po-chk\"><input type=\"checkbox\" data-k=\"oactive\"" + (o == null || o.Active ? " checked" : string.Empty) + (ro ? " disabled" : string.Empty) + "> Switched on</label></div><div class=\"po-otypes\"><span class=\"po-hint\">Room types</span><div class=\"po-orad\"><label class=\"po-chk\"><input type=\"radio\" name=\"po-oall\" value=\"all\" onchange=\"AdminOffersJs.typesMode()\"" + (o == null || o.Types.Count == 0 ? " checked" : string.Empty) + (ro ? " disabled" : string.Empty) + "> All room types</label><label class=\"po-chk\"><input type=\"radio\" name=\"po-oall\" value=\"some\" onchange=\"AdminOffersJs.typesMode()\"" + (o != null && o.Types.Count > 0 ? " checked" : string.Empty) + (ro ? " disabled" : string.Empty) + "> Specific types</label></div><div class=\"po-otlist\" id=\"po-otlist\"" + (o != null && o.Types.Count > 0 ? string.Empty : " hidden") + ">" + string.Concat(site.Types.OrderBy(t => t.Order).Select(t => "<label class=\"po-chk\"><input type=\"checkbox\" name=\"po-otype\" value=\"" + Ui.Attr(t.Key) + "\"" + (o != null && o.Types.Contains(t.Key) ? " checked" : string.Empty) + (ro ? " disabled" : string.Empty) + "> " + Ui.Enc(t.Name) + "</label>")) + "</div></div><div><span class=\"po-hint\">Picture from the media library</span>" + Ui.Picker("po", site, "po-oimg", "photo", new[] { o?.Image ?? site.Media.FirstOrDefault(m => m.Kind == "photo")?.Full ?? string.Empty }, false, ro) + "</div><em class=\"po-err\" id=\"po-e-form\"></em></div><div class=\"po-mfoot\">" + (!ro && o != null ? "<button type=\"button\" class=\"po-btn po-btn-danger\" onclick=\"AdminOffersJs.del('" + Ui.Attr(o.Code) + "')\">Delete</button><span class=\"po-grow\"></span>" : string.Empty) + "<button type=\"button\" class=\"po-btn po-btn-ghost\" onclick=\"AdminOffersJs.closeModal()\">" + (ro ? "Close" : "Cancel") + "</button>" + (ro ? string.Empty : "<button type=\"button\" class=\"po-btn po-btn-accent\" onclick=\"AdminOffersJs.save('" + Ui.Attr(o?.Code ?? string.Empty) + "')\">Save</button>") + "</div>");
            response.SetElementContents("po-mbody", sb.ToString());
            response.ExecuteScript("AdminOffersJs.openModal();");
            return response;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminOffersJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "offer", GetDataValue("ocode"), "You can view offers but not change them.");
            }
            string code = Ui.Clip(GetDataValue("ocode"), 20);
            string newCode = Regex.Replace(Ui.Clip(GetDataValue("ocode2"), 20).ToUpperInvariant(), "[^A-Z0-9]", string.Empty);
            string title = Ui.Clip(GetDataValue("otitle"), 60);
            DateTime? from = Ui.ParseDay(GetDataValue("ofrom"));
            DateTime? to = Ui.ParseDay(GetDataValue("oto"));
            int pct = int.TryParse(GetDataValue("opct"), out int p) ? Math.Clamp(p, 0, 60) : 0;
            int bonus = int.TryParse(GetDataValue("obonus"), out int bo) ? Math.Clamp(bo, 1, 3) : 1;
            List<string> otypes = GetDataValue("oall") == "some" ? GetDataValue("otypes").Split('|').Where(x => x != string.Empty).Distinct().ToList() : new List<string>();
            string err = GetDataValue("oall") == "some" && otypes.Count == 0 ? "Choose at least one room type, or All room types." : code == string.Empty && newCode.Length < 4 ? "Use a code of 4 to 20 letters or numbers." : title.Length < 3 ? "Give the offer a title." : from == null || to == null || to < from ? "Check the dates." : pct == 0 && bonus == 1 ? "Give a discount or bonus points." : string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Offer? o = site.Offers.FirstOrDefault(x => x.Code == code);
                if (err == string.Empty && o == null && site.Offers.Any(x => x.Code == newCode))
                {
                    err = "That code already exists.";
                }
                if (err == string.Empty)
                {
                    if (o == null)
                    {
                        o = new Offer { Code = newCode };
                        site.Offers.Add(o);
                    }
                    o.Title = title;
                    o.Text = Ui.Clip(GetDataValue("otext"), 240);
                    o.Pct = pct;
                    o.Bonus = bonus;
                    o.MinNights = int.TryParse(GetDataValue("omin"), out int mn) ? Math.Clamp(mn, 1, 14) : 1;
                    o.From = from!.Value;
                    o.To = to!.Value.AddHours(23).AddMinutes(59);
                    o.Members = GetDataValue("omembers") == "1";
                    o.Types = otypes.Where(x => site.Types.Any(t => t.Key == x)).ToList();
                    o.Active = GetDataValue("oactive") == "1";
                    o.Image = Access.SafeImage(GetDataValue("oimg")) && site.Media.Any(m => m.Full == GetDataValue("oimg") && m.Kind == "photo") ? GetDataValue("oimg") : o.Image;
                    code = o.Code;
                    await Store.SaveSite(site, c.Sbx);
                }
            }
            if (err != string.Empty)
            {
                response.SetElementContents("po-e-form", err);
                return response;
            }
            await Store.Log(c.Sbx, c.U.Id, "offer", code, true);
            response.ExecuteScript("AdminOffersJs.closeModal();AdminOffersJs.list(1);AdminOffersJs.toast('Offer saved');");
            return response;
        }

        public async Task<ApiResponse> Remove()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminOffersJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "offer-delete", GetDataValue("ocode"), "You can't delete offers.");
            }
            string code = Ui.Clip(GetDataValue("ocode"), 20);
            bool off = false;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Offer? o = site.Offers.FirstOrDefault(x => x.Code == code);
                if (o == null)
                {
                    return response;
                }
                if (o.Used > 0 || site.Bookings.Any(b => b.Promo == code))
                {
                    o.Active = false;
                    off = true;
                }
                else
                {
                    site.Offers.Remove(o);
                }
                await Store.SaveSite(site, c.Sbx);
            }
            await Store.Log(c.Sbx, c.U.Id, off ? "offer-off" : "offer-delete", code, true);
            response.ExecuteScript("AdminOffersJs.closeModal();AdminOffersJs.list(1);AdminOffersJs.toast('" + (off ? "Used on bookings, so it was switched off" : "Deleted") + "');");
            return response;
        }
    }
}
