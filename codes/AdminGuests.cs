using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyHotel.Core;
using SkyHotel.Models;
using SkyNet;

namespace SkyHotel.codes
{
    public class AdminGuests : WebPage
    {
        private const string AreaKey = "guests";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Guests");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--gs-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("gs-menu", Auth.Menu("gs", "AdminGuests", c, site));
            response.SetElementContents("gs-who", Auth.Who("gs", c));
            response.SetElementContents("gs-demo", Auth.DemoPill("gs", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminGuestsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"gs-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"gs-panel\"><p>You don't have access to this area. Ask the general manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminGuestsJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminGuestsJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("gs-main", NoAccess("Guests"));
                return response;
            }
            response.SetElementContents("gs-main", await MainHtml(c, site));
            response.ExecuteScript("AdminGuestsJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"gs-tr gs-th\">");
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
            StringBuilder sb = new StringBuilder("<div class=\"gs-ptop\"><div><h1>Guests</h1><p>Everyone who has booked with us, " + site.Guests.Count + " guests.</p></div></div>");
            sb.Append("<div class=\"gs-bar\"><label>Search<input type=\"search\" id=\"gs-q\" list=\"gs-qlist\" placeholder=\"Name, email or phone\" maxlength=\"60\" oninput=\"AdminGuestsJs.search()\"></label>" + Ui.DataList("gs-qlist", site.Guests.Select(g => Ui.GuestName(g))) + "<label>Membership<select id=\"gs-gmem\" onchange=\"AdminGuestsJs.list(1)\">" + Ui.Opts(new[] { ("", "Everyone"), ("yes", "Circle members"), ("no", "Not members"), ("vip", "VIP guests") }, string.Empty) + "</select></label><label>Country<input type=\"text\" id=\"gs-gcountry\" list=\"gs-clist\" placeholder=\"Any country\" maxlength=\"40\" oninput=\"AdminGuestsJs.search()\"></label>" + Ui.DataList("gs-clist", site.Guests.Select(g => g.Country)) + "<label>Sort<select id=\"gs-gsort\" onchange=\"AdminGuestsJs.list(1)\">" + Ui.Opts(new[] { ("last", "Most recent stay"), ("spent", "Most spent"), ("stays", "Most stays"), ("name", "Name A-Z") }, "last") + "</select></label></div>");
            sb.Append("<div class=\"gs-bar\" id=\"gs-perbar\">" + Ui.PeriodBar("gs", "AdminGuestsJs.per", "all", string.Empty, string.Empty) + "</div><p class=\"gs-hint\" style=\"margin:-6px 0 10px\">The period filters by each guest's most recent stay.</p><div class=\"gs-tbl gs-gstbl\" id=\"gs-list\"></div><div id=\"gs-pager\"></div>");
            return sb.ToString();
        }

        private sealed class Row
        {
            public Guest G { get; set; } = new Guest();
            public int Stays { get; set; }
            public int Nights { get; set; }
            public decimal Spent { get; set; }
            public DateTime? Last { get; set; }
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminGuestsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            string term = Ui.Clip(GetDataValue("term"), 60);
            string mem = GetDataValue("gmem");
            string country = Ui.Clip(GetDataValue("gcountry"), 40);
            string per = Per(GetDataValue("per"));
            (DateTime? from, DateTime? to) = Ui.Period(per, GetDataValue("pfrom"), GetDataValue("pto"));
            ILookup<string, Booking> byGuest = site.Bookings.Where(b => b.Status != "cancelled").ToLookup(b => b.Guest);
            IEnumerable<Row> q = site.Guests.Select(g =>
            {
                List<Booking> bs = byGuest[g.Id].ToList();
                List<Booking> done = bs.Where(b => b.Status == "out" || b.Status == "inhouse").ToList();
                return new Row { G = g, Stays = done.Count, Nights = done.Sum(b => Book.Nights(b.In, b.Out)), Spent = done.Sum(b => b.Paid), Last = bs.Count > 0 ? bs.Max(b => b.In) : null };
            });
            if (mem == "yes" || mem == "no")
            {
                q = q.Where(r => r.G.Member == (mem == "yes"));
            }
            else if (mem == "vip")
            {
                q = q.Where(r => r.G.Vip);
            }
            if (country != string.Empty)
            {
                q = q.Where(r => r.G.Country.Contains(country, StringComparison.OrdinalIgnoreCase));
            }
            if (term != string.Empty)
            {
                q = q.Where(r => Ui.GuestName(r.G).Contains(term, StringComparison.OrdinalIgnoreCase) || r.G.Email.Contains(term, StringComparison.OrdinalIgnoreCase) || r.G.Phone.Contains(term, StringComparison.OrdinalIgnoreCase));
            }
            if (from != null || to != null)
            {
                q = q.Where(r => r.Last != null && Ui.InPeriod(r.Last.Value, from, to));
            }
            q = GetDataValue("gsort") switch
            {
                "spent" => q.OrderByDescending(r => r.Spent),
                "stays" => q.OrderByDescending(r => r.Stays),
                "name" => q.OrderBy(r => r.G.Last).ThenBy(r => r.G.First),
                _ => q.OrderByDescending(r => r.Last ?? DateTime.MinValue)
            };
            List<Row> all = q.ToList();
            int page = Ui.PageOf(GetDataValue("pg"), Size, all.Count);
            StringBuilder sb = new StringBuilder(Head(new[] { ("Guest", string.Empty), ("Country", string.Empty), ("Stays", string.Empty), ("Nights", string.Empty), ("Spent", "gs-num"), ("Latest stay", string.Empty), ("Circle", string.Empty), (string.Empty, string.Empty) }));
            if (all.Count == 0)
            {
                sb.Append("<div class=\"gs-tr\"><span class=\"gs-none gs-wide\">No guests match.</span></div>");
            }
            foreach (Row r in all.Skip((page - 1) * Size).Take(Size))
            {
                sb.Append("<div class=\"gs-tr\"><span data-l=\"Guest\" class=\"gs-wide\"><b>" + Ui.Enc(Ui.GuestName(r.G)) + (r.G.Vip ? "<span class=\"gs-vip\">VIP</span>" : string.Empty) + "</b><small>" + Ui.Enc(r.G.Email) + "</small></span><span data-l=\"Country\">" + Ui.Enc(r.G.Country) + "</span><span data-l=\"Stays\">" + r.Stays + "</span><span data-l=\"Nights\">" + r.Nights + "</span><span data-l=\"Spent\" class=\"gs-num\">" + Ui.Money(r.Spent) + "</span><span data-l=\"Latest stay\">" + (r.Last?.ToString("d MMM yyyy", Ui.Inv) ?? "&ndash;") + "</span><span data-l=\"Circle\">" + (r.G.Member ? Ui.GradePill("gs", r.G) : "&ndash;") + "</span><span><button type=\"button\" class=\"gs-btn gs-btn-sm gs-btn-ghost\" onclick=\"AdminGuestsJs.open('" + Ui.Attr(r.G.Id) + "')\">Open</button></span></div>");
            }
            response.SetElementContents("gs-list", sb.ToString());
            response.SetElementContents("gs-pager", Ui.Pager("gs", page, Size, all.Count, "guest", "guests", "AdminGuestsJs.list"));
            response.SetElementContents("gs-perbar", Ui.PeriodBar("gs", "AdminGuestsJs.per", per, GetDataValue("pfrom"), GetDataValue("pto")));
            return response;
        }

        public async Task<ApiResponse> Open()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminGuestsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Guest? g = site.Guests.FirstOrDefault(x => x.Id == GetDataValue("gid"));
            if (g == null)
            {
                return response;
            }
            bool edit = Access.Can(c.U, AreaKey, 'E');
            StringBuilder sb = new StringBuilder("<h2>" + Ui.Enc(Ui.GuestName(g)) + (g.Member ? " " + Ui.GradePill("gs", g) : string.Empty) + (g.Vip ? "<span class=\"gs-vip\">VIP</span>" : string.Empty) + "</h2><dl class=\"gs-dl\"><dt>Email</dt><dd>" + Ui.Enc(g.Email) + "</dd><dt>Country</dt><dd>" + Ui.Enc(g.Country) + "</dd><dt>Guest since</dt><dd>" + g.Created.ToString("d MMM yyyy", Ui.Inv) + "</dd>" + (g.Member ? "<dt>Circle</dt><dd>" + Ui.Enc(g.MemberNo) + " &middot; " + Ui.Num(g.Lifetime) + " lifetime points" + (Access.Can(c.U, "members", 'V') ? " &middot; <a href=\"AdminMembers\">Members</a>" : string.Empty) + "</dd>" : string.Empty) + "</dl><h3>Contact &amp; notes</h3><div class=\"gs-form\"><label class=\"gs-field\"><span>Phone</span><input type=\"tel\" id=\"gs-gphone\" maxlength=\"30\" value=\"" + Ui.Attr(g.Phone) + "\"" + (edit ? string.Empty : " readonly") + "></label><label class=\"gs-field\"><span>Notes for staff</span><textarea id=\"gs-gnotes\" rows=\"2\" maxlength=\"300\"" + (edit ? string.Empty : " readonly") + ">" + Ui.Enc(g.Notes) + "</textarea></label><label class=\"gs-chk\"><input type=\"checkbox\" id=\"gs-gvip\"" + (g.Vip ? " checked" : string.Empty) + (edit ? string.Empty : " disabled") + "> VIP guest</label></div><h3>Bookings</h3>");
            List<Booking> bs = site.Bookings.Where(b => b.Guest == g.Id).OrderByDescending(b => b.In).ToList();
            if (bs.Count == 0)
            {
                sb.Append("<p class=\"gs-none\">No bookings.</p>");
            }
            foreach (Booking b in bs.Take(10))
            {
                sb.Append("<div class=\"gs-row\"><div><b>" + Ui.Enc(Ui.TypeName(site, b.Type)) + (b.Room != string.Empty ? " &middot; room " + Ui.Enc(b.Room) : string.Empty) + "</b><small>" + Ui.Enc(b.Code) + " &middot; " + b.In.ToString("d MMM yyyy", Ui.Inv) + " &rarr; " + b.Out.ToString("d MMM", Ui.Inv) + " &middot; " + Ui.Cash(b.Total) + "</small></div>" + Auth.StatusPill("gs", b) + "</div>");
            }
            sb.Append("<div class=\"gs-mfoot\"><button type=\"button\" class=\"gs-btn gs-btn-ghost\" onclick=\"AdminGuestsJs.closeModal()\">Close</button>" + (edit ? "<button type=\"button\" class=\"gs-btn gs-btn-accent\" onclick=\"AdminGuestsJs.saveGuest('" + Ui.Attr(g.Id) + "')\">Save</button>" : string.Empty) + "</div>");
            response.SetElementContents("gs-mbody", sb.ToString());
            response.ExecuteScript("AdminGuestsJs.openModal();");
            return response;
        }

        public async Task<ApiResponse> SaveGuest()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminGuestsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "guest", GetDataValue("gid"), "You can view guests but not edit them.");
            }
            string name;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Guest? g = site.Guests.FirstOrDefault(x => x.Id == GetDataValue("gid"));
                if (g == null)
                {
                    return response;
                }
                g.Phone = Ui.Clip(GetDataValue("gphone"), 30);
                g.Notes = Ui.Clip(GetDataValue("gnotes"), 300);
                g.Vip = GetDataValue("gvip") == "1";
                name = Ui.GuestName(g);
                await Store.SaveSite(site, c.Sbx);
            }
            await Store.Log(c.Sbx, c.U.Id, "guest", name, true);
            response.ExecuteScript("AdminGuestsJs.closeModal();AdminGuestsJs.list(1);AdminGuestsJs.toast('Saved');");
            return response;
        }
    }
}
