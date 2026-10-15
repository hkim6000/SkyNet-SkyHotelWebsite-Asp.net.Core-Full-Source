using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyHotel.Core;
using SkyHotel.Models;
using SkyNet;

namespace SkyHotel.codes
{
    public class MyCircle : WebPage
    {
        private sealed class View
        {
            public string Title { get; set; } = string.Empty;
            public string Desc { get; set; } = string.Empty;
            public string Main { get; set; } = string.Empty;
        }

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            View v = Render(site, Ui.Clip(string.Empty, 80), Ui.Clip(string.Empty, 80));
            HtmlDoc.SetTitle((v.Title != string.Empty ? v.Title + " | " : string.Empty) + site.Settings.Name + " " + site.Settings.Town);
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("description", v.Desc != string.Empty ? v.Desc : site.Settings.Tagline);
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_name}", Ui.Enc(site.Settings.Name))
                .Replace("{plhd_town}", Ui.Enc(site.Settings.Town))
                .Replace("{plhd_tagline}", Ui.Enc(site.Settings.Tagline))
                .Replace("{plhd_addr}", Ui.Enc(site.Settings.Address))
                .Replace("{plhd_phone}", Ui.Enc(site.Settings.Phone))
                .Replace("{plhd_tel}", Ui.Attr(Regex.Replace(site.Settings.Phone, "[^0-9+]", string.Empty)))
                .Replace("{plhd_email}", Ui.Enc(site.Settings.Email))
                .Replace("{plhd_theme}", site.Settings.Theme == "dark" ? "dark" : "light")
                .Replace("{plhd_style}", SafeColor(site.Settings.Accent) ? "--mc-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_nav}", Nav("mc", "MyCircle", false))
                .Replace("{plhd_drawer}", Nav("mc", "MyCircle", true))
                .Replace("{plhd_main}", v.Main);
        }

        private static readonly (string Page, string Name)[] Links = { ("Rooms", "Rooms"), ("Offers", "Offers"), ("Dining", "Dining"), ("Spa", "Spa"), ("Membership", "Membership"), ("Gallery", "Gallery"), ("Contact", "Location") };

        private static string Nav(string p, string page, bool drawer)
        {
            StringBuilder sb = new StringBuilder();
            if (drawer)
            {
                sb.Append("<a href=\"Home\">Home</a>");
            }
            foreach ((string k, string n) in Links)
            {
                sb.Append("<a href=\"" + k + "\"" + (k == page || (k == "Rooms" && page == "Room") || (k == "Membership" && page == "MyCircle") ? " class=\"" + p + "-on\"" : string.Empty) + ">" + n + "</a>");
            }
            if (drawer)
            {
                sb.Append("<a href=\"MyBooking\">My booking</a><a href=\"MyCircle\">My Circle</a>");
            }
            return sb.ToString();
        }

        private static bool SafeColor(string v)
        {
            return Regex.IsMatch(v ?? string.Empty, "^#[0-9a-fA-F]{6}$");
        }

        public async Task<ApiResponse> Hydrate()
        {
            ApiResponse response = new ApiResponse();
            string sbx = Store.Sandbox(GetDataValue("sbx"));
            if (sbx == string.Empty)
            {
                response.ExecuteScript("MyCircleJs.hydrated(false);");
                return response;
            }
            SiteData site = await Store.LoadSite(sbx);
            View v = Render(site, Ui.Clip(GetDataValue("argA"), 80), Ui.Clip(GetDataValue("argB"), 80));
            response.SetElementContents("mc-main", v.Main);
            response.SetElementContents("mc-brand", Ui.Enc(site.Settings.Name));
            response.SetElementContents("mc-town", Ui.Enc(site.Settings.Town));
            response.SetElementContents("mc-ftag", Ui.Enc(site.Settings.Tagline));
            response.SetElementContents("mc-faddr", Ui.Enc(site.Settings.Address) + "<br><a href=\"tel:" + Ui.Attr(Regex.Replace(site.Settings.Phone, "[^0-9+]", string.Empty)) + "\">" + Ui.Enc(site.Settings.Phone) + "</a> &middot; <a href=\"mailto:" + Ui.Attr(site.Settings.Email) + "\">" + Ui.Enc(site.Settings.Email) + "</a>");
            string accent = SafeColor(site.Settings.Accent) ? site.Settings.Accent : string.Empty;
            response.ExecuteScript("MyCircleJs.hydrated(true,'" + accent + "','" + (site.Settings.Theme == "dark" ? "dark" : "light") + "');");
            return response;
        }

        private static async Task<string> Writable(ApiResponse response, string requested)
        {
            if (!Store.Demo)
            {
                return string.Empty;
            }
            string sbx = Store.Sandbox(requested);
            if (sbx == string.Empty)
            {
                sbx = await Store.NewSandbox();
                response.ExecuteScript("MyCircleJs.sandboxed('" + sbx + "');");
            }
            return sbx;
        }

        private static string Readable(string requested)
        {
            return Store.Demo ? Store.Sandbox(requested) : string.Empty;
        }

        public async Task<ApiResponse> Who()
        {
            ApiResponse response = new ApiResponse();
            MCtx? m = await Auth.Member(GetDataValue("mtok"));
            if (m == null)
            {
                response.ExecuteScript("MyCircleJs.who('', '');");
                return response;
            }
            SiteData site = await Store.LoadSite(m.Sbx);
            Guest? g = site.Guests.FirstOrDefault(x => x.Id == m.Guest && x.Member);
            if (g == null)
            {
                response.ExecuteScript("MyCircleJs.who('', '');");
                return response;
            }
            response.ExecuteScript("MyCircleJs.who('" + Ui.Js(Ui.Enc(g.First)) + "','" + Ui.GradeOf(g.Lifetime).Key + "');");
            return response;
        }

        private static string Stars(int n)
        {
            int k = Math.Clamp(n, 0, 5);
            return "<span class=\"mc-stars\" aria-label=\"" + k + " out of 5\">" + new string('★', k) + "<span style=\"opacity:0.3\">" + new string('★', 5 - k) + "</span></span>";
        }

        private static decimal FromPrice(SiteData site, RoomType t)
        {
            DateTime today = DateTime.UtcNow.Date;
            decimal min = decimal.MaxValue;
            for (int i = 0; i < 30; i++)
            {
                min = Math.Min(min, Book.Rate(site, t, today.AddDays(i)));
            }
            return min;
        }

        private static string RoomCard(SiteData site, RoomType t, bool lazy = true)
        {
            string img = t.Images.Count > 0 ? t.Images[0] : string.Empty;
            return "<article class=\"mc-card\"><a class=\"mc-card-img\" href=\"Room?t=" + Uri.EscapeDataString(t.Key) + "\">" + Ui.Pic(img, t.Name, string.Empty, true, !lazy) + "</a><div class=\"mc-card-body\"><h3><a href=\"Room?t=" + Uri.EscapeDataString(t.Key) + "\">" + Ui.Enc(t.Name) + "</a></h3><div class=\"mc-facts\"><span>" + t.Size + " m&sup2;</span><span>" + Ui.Enc(t.Beds) + "</span><span>" + Ui.Enc(t.View) + " view</span><span>Up to " + t.MaxGuests + " guests</span></div><p>" + Ui.Enc(t.Short) + "</p><div class=\"mc-from\"><div><span>From</span> <b>" + Ui.Money(FromPrice(site, t)) + "</b> <span>/ night</span></div><a class=\"mc-btn mc-btn-sm mc-btn-ghost\" href=\"Reservation?t=" + Uri.EscapeDataString(t.Key) + "\">Book</a></div></div></article>";
        }

        private static string Hero(ContentBlock c, string fallbackTitle)
        {
            return "<section class=\"mc-phero\">" + (c.Image != string.Empty ? Ui.Pic(c.Image, string.Empty, string.Empty, false, true) : string.Empty) + "<div class=\"mc-wrap\"><h1>" + Ui.Enc(c.Title != string.Empty ? c.Title : fallbackTitle) + "</h1>" + (c.Text != string.Empty ? "<p>" + Ui.Enc(c.Text) + "</p>" : string.Empty) + "</div></section>";
        }

        private static ContentBlock Block(SiteData site, string key)
        {
            return site.Content.TryGetValue(key, out ContentBlock? c) && c != null ? c : new ContentBlock();
        }

        private static bool OfferLive(Offer o)
        {
            DateTime today = DateTime.UtcNow.Date;
            return o.Active && today >= o.From.Date && today <= o.To.Date;
        }

        private View Render(SiteData site, string a, string b)
        {
            return new View { Title = "My Circle", Main = "<div class=\"mc-wrap\" id=\"mc-circle\"><p class=\"mc-none\" style=\"padding:60px 0\">Loading your account&hellip;</p></div>", Desc = "Your SkyHotel Circle account." };
        }

        private static async Task<(SiteData Site, string Sbx, Guest? Member)> Context(string sbxValue, string mtok)
        {
            string sbx = Readable(sbxValue);
            SiteData site = await Store.LoadSite(sbx);
            MCtx? m = await Auth.Member(mtok);
            Guest? g = m != null && m.Sbx == sbx ? site.Guests.FirstOrDefault(x => x.Id == m.Guest && x.Member) : null;
            return (site, sbx, g);
        }

        private static bool IsEmail(string v)
        {
            return v.Length <= 100 && Regex.IsMatch(v, "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$");
        }

        private const int HistSize = 10;

        private static string Out(string msg)
        {
            return "<div class=\"mc-phead\"><span class=\"mc-kick\">SkyHotel Circle</span><h1>My Circle</h1><p class=\"mc-lead\">" + msg + "</p><p style=\"margin-top:22px\"><a class=\"mc-btn mc-btn-gold\" href=\"Membership#join\">Sign in or join</a></p></div>";
        }

        private static string History(SiteData site, Guest g, int page, string kind)
        {
            List<PointEntry> all = site.Points.Where(p => p.Guest == g.Id && (kind == string.Empty || p.Kind == kind || (kind == "redeem" && p.Kind == "return"))).OrderByDescending(p => p.When).ToList();
            if (all.Count == 0)
            {
                return "<p class=\"mc-none\">No points activity yet.</p>";
            }
            int pg = Ui.PageOf(page.ToString(Ui.Inv), HistSize, all.Count);
            StringBuilder sb = new StringBuilder("<div class=\"mc-ptab\">");
            foreach (PointEntry p in all.Skip((pg - 1) * HistSize).Take(HistSize))
            {
                string label = p.Kind switch { "earn" => "Earned", "redeem" => "Used", "return" => "Returned", _ => "Adjusted" };
                sb.Append("<div class=\"mc-prow\"><span>" + p.When.ToString("d MMM yyyy", Ui.Inv) + "</span><span><b>" + label + "</b><small>" + Ui.Enc(p.Note) + "</small></span><span class=\"" + (p.Pts >= 0 ? "mc-plus" : "mc-neg") + "\">" + (p.Pts >= 0 ? "+" : "&minus;") + Ui.Num(Math.Abs(p.Pts)) + "</span></div>");
            }
            sb.Append("</div>" + Ui.Pager("mc", pg, HistSize, all.Count, "entry", "entries", "MyCircleJs.hist"));
            return sb.ToString();
        }

        private static string Dash(SiteData site, Guest g, bool welcome)
        {
            (string key, string name, int min, int pct, string perks) = Ui.GradeOf(g.Lifetime);
            (string Key, string Name, int Min, int Pct, string Perks)? next = Ui.NextGrade(g.Lifetime);
            int pending = Book.Pending(site, g);
            DateTime today = DateTime.UtcNow.Date;
            List<Booking> mine = site.Bookings.Where(x => x.Guest == g.Id).ToList();
            List<Booking> up = mine.Where(x => Book.Holds(x)).OrderBy(x => x.In).ToList();
            List<Booking> past = mine.Where(x => x.Status == "out").OrderByDescending(x => x.Out).ToList();
            StringBuilder sb = new StringBuilder();
            if (welcome)
            {
                sb.Append("<div class=\"mc-ok-box mc-welcome\"><b>Welcome to SkyHotel Circle, " + Ui.Enc(g.First) + ".</b> Your member rates are ready. Book a stay to start earning points.</div>");
            }
            sb.Append("<div class=\"mc-mchead\"><div><span class=\"mc-kick\">SkyHotel Circle &middot; " + Ui.Enc(g.MemberNo) + "</span><h1>Hello, " + Ui.Enc(g.First) + "</h1></div><button type=\"button\" class=\"mc-btn mc-btn-ghost mc-btn-sm\" onclick=\"MyCircleJs.signOut()\">Sign out</button></div>");
            sb.Append("<div class=\"mc-mcard mc-mc-" + key + "\"><div class=\"mc-mc-top\"><span class=\"mc-grade mc-g-" + key + "\">" + name + "</span><span class=\"mc-mc-brand\">SkyHotel Circle</span></div><div class=\"mc-mc-nums\"><div><small>Points to spend</small><b>" + Ui.Num(g.Balance) + "</b><span>worth " + Ui.Money(g.Balance / 1000 * 10) + "</span></div><div><small>Lifetime points</small><b>" + Ui.Num(g.Lifetime) + "</b><span>sets your grade</span></div><div><small>Pending</small><b>" + Ui.Num(pending) + "</b><span>added at check-out</span></div></div>");
            if (next != null)
            {
                int span = next.Value.Min - min;
                int done = g.Lifetime - min;
                int w = span > 0 ? Math.Clamp(done * 100 / span, 2, 100) : 100;
                sb.Append("<div class=\"mc-mc-prog\"><div class=\"mc-mc-bar\"><i style=\"width:" + w + "%\"></i></div><span>" + Ui.Num(next.Value.Min - g.Lifetime) + " points to <b>" + next.Value.Name + "</b></span></div>");
            }
            else
            {
                sb.Append("<div class=\"mc-mc-prog\"><span>You have reached our highest grade. Thank you.</span></div>");
            }
            sb.Append("<p class=\"mc-mc-perks\">Your benefits: " + Ui.Enc(perks) + ".</p></div>");
            sb.Append("<div class=\"mc-mcgrid\"><section class=\"mc-box\"><div class=\"mc-boxhead\"><h2>Upcoming stays</h2><a class=\"mc-more\" href=\"Reservation\">Book a stay &rarr;</a></div>");
            if (up.Count == 0)
            {
                sb.Append("<p class=\"mc-none\">No upcoming stays.</p>");
            }
            foreach (Booking x in up)
            {
                sb.Append("<div class=\"mc-stay\"><div><b>" + Ui.Enc(Ui.TypeName(site, x.Type)) + "</b><small>" + Ui.Day(x.In, false) + " &rarr; " + Ui.Day(x.Out, false) + " &middot; " + Ui.Enc(x.Code) + "</small><small>+" + Ui.Num(Book.EarnOnStay(x)) + " points at check-out</small></div><a class=\"mc-btn mc-btn-sm mc-btn-ghost\" href=\"MyBooking?c=" + Uri.EscapeDataString(x.Code) + "\">Manage</a></div>");
            }
            sb.Append("</section><section class=\"mc-box\"><h2>Past stays</h2>");
            if (past.Count == 0)
            {
                sb.Append("<p class=\"mc-none\">Your stays will appear here after check-out.</p>");
            }
            foreach (Booking x in past.Take(6))
            {
                sb.Append("<div class=\"mc-stay\"><div><b>" + Ui.Enc(Ui.TypeName(site, x.Type)) + "</b><small>" + Ui.Day(x.In, false) + " &rarr; " + Ui.Day(x.Out, false) + " &middot; " + Ui.Cash(x.Paid) + "</small></div><span class=\"mc-plus\">+" + Ui.Num(x.PointsEarned) + "</span></div>");
            }
            sb.Append("</section></div>");
            sb.Append("<section class=\"mc-box mc-mcsec\"><div class=\"mc-boxhead\"><h2>Points history</h2><select id=\"mc-hkind\" onchange=\"MyCircleJs.hist(1)\" aria-label=\"Type\"><option value=\"\">All activity</option><option value=\"earn\">Earned</option><option value=\"redeem\">Used</option><option value=\"adjust\">Adjusted</option></select></div><div id=\"mc-hist\">" + History(site, g, 1, string.Empty) + "</div></section>");
            List<Offer> offers = site.Offers.Where(OfferLive).OrderByDescending(o => o.Members).ToList();
            if (offers.Count > 0)
            {
                sb.Append("<section class=\"mc-box mc-mcsec\"><h2>Your promotions</h2><div class=\"mc-promos\">");
                foreach (Offer o in offers)
                {
                    sb.Append("<div class=\"mc-promo\">" + (o.Members ? "<span class=\"mc-tag mc-tag-navy\">Members</span>" : string.Empty) + "<b>" + Ui.Enc(o.Title) + "</b><small>" + Ui.Enc(o.Text) + (o.Types.Count > 0 ? " Valid on " + Ui.Enc(Book.TypesText(site, o)) + "." : string.Empty) + "</small><div><code>" + Ui.Enc(o.Code) + "</code><a class=\"mc-more\" href=\"Reservation?promo=" + Uri.EscapeDataString(o.Code) + "\">Use code &rarr;</a></div></div>");
                }
                sb.Append("</div></section>");
            }
            sb.Append("<section class=\"mc-box mc-mcsec\"><h2>Profile</h2><form class=\"mc-form\" onsubmit=\"return MyCircleJs.profile()\" novalidate><div class=\"mc-row3\"><label class=\"mc-field\"><span>Name</span><input type=\"text\" value=\"" + Ui.Attr(g.First + " " + g.Last) + "\" readonly></label><label class=\"mc-field\"><span>Email</span><input type=\"text\" value=\"" + Ui.Attr(g.Email) + "\" readonly></label><label class=\"mc-field\"><span>Member since</span><input type=\"text\" value=\"" + (g.Joined?.ToString("d MMM yyyy", Ui.Inv) ?? string.Empty) + "\" readonly></label></div><div class=\"mc-row3\"><label class=\"mc-field\"><span>Phone</span><input type=\"tel\" id=\"mc-pphone\" maxlength=\"30\" value=\"" + Ui.Attr(g.Phone) + "\"><em class=\"mc-err\" id=\"mc-e-pphone\"></em></label><label class=\"mc-field\"><span>Country</span><input type=\"text\" id=\"mc-pcountry\" maxlength=\"40\" value=\"" + Ui.Attr(g.Country) + "\"></label><label class=\"mc-field\"><span>New password (optional)</span><input type=\"password\" id=\"mc-ppass\" maxlength=\"100\" autocomplete=\"new-password\"><em class=\"mc-err\" id=\"mc-e-ppass\"></em></label></div><div class=\"mc-acts\"><button type=\"submit\" class=\"mc-btn mc-btn-sm\">Save profile</button></div></form></section>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            MCtx? m = await Auth.Member(GetDataValue("mtok"));
            if (m == null)
            {
                response.SetElementContents("mc-circle", Out("Sign in to see your points, grade and stays."));
                response.ExecuteScript("MyCircleJs.signedOut();");
                return response;
            }
            SiteData site = await Store.LoadSite(m.Sbx);
            Guest? g = site.Guests.FirstOrDefault(x => x.Id == m.Guest && x.Member);
            if (g == null)
            {
                response.SetElementContents("mc-circle", Out("Your session has ended. Please sign in again."));
                response.ExecuteScript("MyCircleJs.signedOut();");
                return response;
            }
            response.SetElementContents("mc-circle", Dash(site, g, GetDataValue("welcome") == "1"));
            response.ExecuteScript("MyCircleJs.who('" + Ui.Js(Ui.Enc(g.First)) + "','" + Ui.GradeOf(g.Lifetime).Key + "');");
            return response;
        }

        public async Task<ApiResponse> Hist()
        {
            ApiResponse response = new ApiResponse();
            MCtx? m = await Auth.Member(GetDataValue("mtok"));
            if (m == null)
            {
                return response;
            }
            SiteData site = await Store.LoadSite(m.Sbx);
            Guest? g = site.Guests.FirstOrDefault(x => x.Id == m.Guest && x.Member);
            if (g == null)
            {
                return response;
            }
            string kind = GetDataValue("hkind") is "earn" or "redeem" or "adjust" ? GetDataValue("hkind") : string.Empty;
            response.SetElementContents("mc-hist", History(site, g, int.TryParse(GetDataValue("pg"), out int pg) ? pg : 1, kind));
            return response;
        }

        public async Task<ApiResponse> Profile()
        {
            ApiResponse response = new ApiResponse();
            MCtx? m = await Auth.Member(GetDataValue("mtok"));
            if (m == null)
            {
                response.ExecuteScript("location.href='Membership';");
                return response;
            }
            string phone = Ui.Clip(GetDataValue("pphone"), 30);
            string country = Ui.Clip(GetDataValue("pcountry"), 40);
            string pass = GetDataValue("ppass");
            string eP = phone != string.Empty && !Regex.IsMatch(phone, "^[+0-9 ()-]{6,30}$") ? "Enter a valid phone number." : string.Empty;
            string eW = pass != string.Empty && (pass.Length < 8 || pass.Length > 100) ? "Use at least 8 characters." : string.Empty;
            response.SetElementContents("mc-e-pphone", eP);
            response.SetElementContents("mc-e-ppass", eW);
            if (eP + eW != string.Empty)
            {
                return response;
            }
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(m.Sbx);
                Guest? g = site.Guests.FirstOrDefault(x => x.Id == m.Guest && x.Member);
                if (g == null)
                {
                    return response;
                }
                g.Phone = phone;
                g.Country = country;
                if (pass != string.Empty)
                {
                    g.Hash = Access.HashPassword(pass);
                }
                await Store.SaveSite(site, m.Sbx);
            }
            response.ExecuteScript("document.getElementById('mc-ppass').value='';MyCircleJs.toast('Profile saved');");
            return response;
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("mtok"));
            response.ExecuteScript("MyCircleJs.signedOut();location.href='Membership';");
            return response;
        }
    }
}
