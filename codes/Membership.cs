using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyHotel.Core;
using SkyHotel.Models;
using SkyNet;

namespace SkyHotel.codes
{
    public class Membership : WebPage
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
                .Replace("{plhd_style}", SafeColor(site.Settings.Accent) ? "--ms-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_nav}", Nav("ms", "Membership", false))
                .Replace("{plhd_drawer}", Nav("ms", "Membership", true))
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
                response.ExecuteScript("MembershipJs.hydrated(false);");
                return response;
            }
            SiteData site = await Store.LoadSite(sbx);
            View v = Render(site, Ui.Clip(GetDataValue("argA"), 80), Ui.Clip(GetDataValue("argB"), 80));
            response.SetElementContents("ms-main", v.Main);
            response.SetElementContents("ms-brand", Ui.Enc(site.Settings.Name));
            response.SetElementContents("ms-town", Ui.Enc(site.Settings.Town));
            response.SetElementContents("ms-ftag", Ui.Enc(site.Settings.Tagline));
            response.SetElementContents("ms-faddr", Ui.Enc(site.Settings.Address) + "<br><a href=\"tel:" + Ui.Attr(Regex.Replace(site.Settings.Phone, "[^0-9+]", string.Empty)) + "\">" + Ui.Enc(site.Settings.Phone) + "</a> &middot; <a href=\"mailto:" + Ui.Attr(site.Settings.Email) + "\">" + Ui.Enc(site.Settings.Email) + "</a>");
            string accent = SafeColor(site.Settings.Accent) ? site.Settings.Accent : string.Empty;
            response.ExecuteScript("MembershipJs.hydrated(true,'" + accent + "','" + (site.Settings.Theme == "dark" ? "dark" : "light") + "');");
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
                response.ExecuteScript("MembershipJs.sandboxed('" + sbx + "');");
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
                response.ExecuteScript("MembershipJs.who('', '');");
                return response;
            }
            SiteData site = await Store.LoadSite(m.Sbx);
            Guest? g = site.Guests.FirstOrDefault(x => x.Id == m.Guest && x.Member);
            if (g == null)
            {
                response.ExecuteScript("MembershipJs.who('', '');");
                return response;
            }
            response.ExecuteScript("MembershipJs.who('" + Ui.Js(Ui.Enc(g.First)) + "','" + Ui.GradeOf(g.Lifetime).Key + "');");
            return response;
        }

        private static string Stars(int n)
        {
            int k = Math.Clamp(n, 0, 5);
            return "<span class=\"ms-stars\" aria-label=\"" + k + " out of 5\">" + new string('★', k) + "<span style=\"opacity:0.3\">" + new string('★', 5 - k) + "</span></span>";
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
            return "<article class=\"ms-card\"><a class=\"ms-card-img\" href=\"Room?t=" + Uri.EscapeDataString(t.Key) + "\">" + Ui.Pic(img, t.Name, string.Empty, true, !lazy) + "</a><div class=\"ms-card-body\"><h3><a href=\"Room?t=" + Uri.EscapeDataString(t.Key) + "\">" + Ui.Enc(t.Name) + "</a></h3><div class=\"ms-facts\"><span>" + t.Size + " m&sup2;</span><span>" + Ui.Enc(t.Beds) + "</span><span>" + Ui.Enc(t.View) + " view</span><span>Up to " + t.MaxGuests + " guests</span></div><p>" + Ui.Enc(t.Short) + "</p><div class=\"ms-from\"><div><span>From</span> <b>" + Ui.Money(FromPrice(site, t)) + "</b> <span>/ night</span></div><a class=\"ms-btn ms-btn-sm ms-btn-ghost\" href=\"Reservation?t=" + Uri.EscapeDataString(t.Key) + "\">Book</a></div></div></article>";
        }

        private static string Hero(ContentBlock c, string fallbackTitle)
        {
            return "<section class=\"ms-phero\">" + (c.Image != string.Empty ? Ui.Pic(c.Image, string.Empty, string.Empty, false, true) : string.Empty) + "<div class=\"ms-wrap\"><h1>" + Ui.Enc(c.Title != string.Empty ? c.Title : fallbackTitle) + "</h1>" + (c.Text != string.Empty ? "<p>" + Ui.Enc(c.Text) + "</p>" : string.Empty) + "</div></section>";
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
            ContentBlock c = Block(site, "membership");
            StringBuilder sb = new StringBuilder(Hero(c, "SkyHotel Circle"));
            sb.Append("<div class=\"ms-wrap\"><section class=\"ms-sec\"><div class=\"ms-how\"><div><b>1</b><h3>Join free</h3><p>Sign up in a minute and get member rates straight away, from 5% off.</p></div><div><b>2</b><h3>Earn 1 point per $1</h3><p>Points for rooms, dining and spa land in your account when you check out.</p></div><div><b>3</b><h3>Spend and climb</h3><p>Use 1,000 points for $10 off a stay. Your grade rises with lifetime points and never drops when you spend.</p></div></div></section>");
            sb.Append("<section class=\"ms-sec\"><span class=\"ms-kick\">Grades</span><h2 class=\"ms-h2\">The more you stay, the more you get</h2><div class=\"ms-gtable\"><div class=\"ms-gt-head\"><span>Grade</span><span>Lifetime points</span><span>Room discount</span><span>Benefits</span></div>");
            foreach ((string key, string name, int min, int pct, string perks) in Ui.Grades)
            {
                sb.Append("<div class=\"ms-gt-row\"><span><span class=\"ms-grade ms-g-" + key + "\">" + name + "</span></span><span>" + (min == 0 ? "From joining" : Ui.Num(min) + "+") + "</span><span><b>" + pct + "%</b></span><span>" + Ui.Enc(perks) + "</span></div>");
            }
            sb.Append("</div><p class=\"ms-hint\" style=\"margin-top:10px\">$1 spent = 1 point. 1,000 points = $10 off a future stay, up to half the room price.</p></section>");
            List<Offer> mo = site.Offers.Where(o => OfferLive(o) && o.Members).ToList();
            if (mo.Count > 0)
            {
                sb.Append("<section class=\"ms-sec\"><span class=\"ms-kick\">Member promotions</span><h2 class=\"ms-h2\">Only for Circle members</h2><div class=\"ms-cards\" style=\"margin-top:24px\">");
                foreach (Offer o in mo)
                {
                    sb.Append("<article class=\"ms-card\"><div class=\"ms-card-img\"><span class=\"ms-tag ms-tag-navy\">" + (o.Pct > 0 ? o.Pct + "% off" : o.Bonus + "&times; points") + "</span>" + Ui.Pic(o.Image, string.Empty, string.Empty, true) + "</div><div class=\"ms-card-body\"><h3>" + Ui.Enc(o.Title) + "</h3><p>" + Ui.Enc(o.Text) + "</p><p class=\"ms-hint\">Valid on " + Ui.Enc(Book.TypesText(site, o)) + ".</p><div class=\"ms-from\"><span>Code <b>" + Ui.Enc(o.Code) + "</b></span><a class=\"ms-btn ms-btn-sm ms-btn-ghost\" href=\"Reservation?promo=" + Uri.EscapeDataString(o.Code) + "\">Book</a></div></div></article>");
                }
                sb.Append("</div></section>");
            }
            sb.Append("<section class=\"ms-sec\" id=\"join\"><div class=\"ms-grid2 ms-msgrid\" id=\"ms-msforms\"><div class=\"ms-box\"><h2>Member sign in</h2><form class=\"ms-form\" onsubmit=\"return MembershipJs.signIn()\" novalidate><label class=\"ms-field\"><span>Email</span><input type=\"email\" id=\"ms-memail\" maxlength=\"100\" autocomplete=\"username\" value=\"" + (Store.Demo ? "mia@example.com" : string.Empty) + "\"></label><label class=\"ms-field\"><span>Password</span><input type=\"password\" id=\"ms-mpass\" maxlength=\"100\" autocomplete=\"current-password\" value=\"" + (Store.Demo ? "demo" : string.Empty) + "\"></label><em class=\"ms-err\" id=\"ms-e-signin\"></em><button type=\"submit\" class=\"ms-btn ms-btn-wide\">Sign in</button>" + (Store.Demo ? "<p class=\"ms-hint\">Demo member: mia@example.com / demo (Gold)</p>" : string.Empty) + "</form></div>");
            sb.Append("<div class=\"ms-box\"><h2>Join SkyHotel Circle</h2><form class=\"ms-form\" onsubmit=\"return MembershipJs.join()\" novalidate><div class=\"ms-row2\"><label class=\"ms-field\"><span>First name</span><input type=\"text\" id=\"ms-jfirst\" maxlength=\"40\" autocomplete=\"given-name\"><em class=\"ms-err\" id=\"ms-e-jfirst\"></em></label><label class=\"ms-field\"><span>Last name</span><input type=\"text\" id=\"ms-jlast\" maxlength=\"40\" autocomplete=\"family-name\"><em class=\"ms-err\" id=\"ms-e-jlast\"></em></label></div><label class=\"ms-field\"><span>Email</span><input type=\"email\" id=\"ms-jmail\" maxlength=\"100\" autocomplete=\"email\"><em class=\"ms-err\" id=\"ms-e-jmail\"></em></label><div class=\"ms-row2\"><label class=\"ms-field\"><span>Password</span><input type=\"password\" id=\"ms-jpass\" maxlength=\"100\" autocomplete=\"new-password\"><em class=\"ms-err\" id=\"ms-e-jpass\"></em></label><label class=\"ms-field\"><span>Country</span><input type=\"text\" id=\"ms-jcountry\" maxlength=\"40\" list=\"ms-countries\"></label></div>" + Ui.DataList("ms-countries", site.Guests.Select(g => g.Country)) + "<button type=\"submit\" class=\"ms-btn ms-btn-gold ms-btn-wide\">Join free</button><p class=\"ms-hint\">Already stayed with us? Join with the same email and your future stays will earn points.</p></form></div></div></section></div>");
            return new View { Title = "SkyHotel Circle membership", Main = sb.ToString(), Desc = c.Text };
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

        public async Task<ApiResponse> SignIn()
        {
            ApiResponse response = new ApiResponse();
            string email = Ui.Clip(GetDataValue("memail"), 100).ToLowerInvariant();
            string pass = GetDataValue("mpass");
            if (Auth.Locked("m:" + email))
            {
                response.SetElementContents("ms-e-signin", "Too many attempts. Wait five minutes and try again.");
                return response;
            }
            string sbx = Readable(GetDataValue("sbx"));
            SiteData site = await Store.LoadSite(sbx);
            Guest? g = site.Guests.FirstOrDefault(x => x.Member && x.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (g == null || pass.Length > 100 || g.Hash == string.Empty || !Access.Verify(pass, g.Hash))
            {
                Auth.Fail("m:" + email);
                response.SetElementContents("ms-e-signin", "That email and password don't match a member account.");
                return response;
            }
            if (Store.Demo)
            {
                sbx = await Writable(response, sbx);
            }
            string tok = await Auth.Open(sbx, g.Id, DateTime.UtcNow.AddHours(8), "member");
            await Store.Log(sbx, "web", "member-signin", g.MemberNo, true);
            response.ExecuteScript("MembershipJs.signedIn('" + tok + "');location.href='MyCircle';");
            return response;
        }

        public async Task<ApiResponse> Join()
        {
            ApiResponse response = new ApiResponse();
            string first = Regex.Replace(Ui.Clip(GetDataValue("jfirst"), 40), "\\s+", " ");
            string last = Regex.Replace(Ui.Clip(GetDataValue("jlast"), 40), "\\s+", " ");
            string email = Ui.Clip(GetDataValue("jmail"), 100).ToLowerInvariant();
            string pass = GetDataValue("jpass");
            string country = Ui.Clip(GetDataValue("jcountry"), 40);
            string eF = first == string.Empty ? "Enter your first name." : string.Empty;
            string eL = last == string.Empty ? "Enter your last name." : string.Empty;
            string eM = !IsEmail(email) ? "Enter a valid email address." : string.Empty;
            string eP = pass.Length < 8 || pass.Length > 100 ? "Use at least 8 characters." : string.Empty;
            response.SetElementContents("ms-e-jfirst", eF);
            response.SetElementContents("ms-e-jlast", eL);
            response.SetElementContents("ms-e-jmail", eM);
            response.SetElementContents("ms-e-jpass", eP);
            if (eF + eL + eM + eP != string.Empty)
            {
                return response;
            }
            string sbx = await Writable(response, GetDataValue("sbx"));
            string gid = string.Empty;
            string err = string.Empty;
            string no = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(sbx);
                Guest? g = site.Guests.FirstOrDefault(x => x.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
                if (g != null && g.Member)
                {
                    err = "That email is already a member. Sign in instead.";
                }
                else if (site.Guests.Count(x => x.Joined > DateTime.UtcNow.AddHours(-1)) > 200)
                {
                    err = "We're busy right now. Please try again later.";
                }
                else
                {
                    if (g == null)
                    {
                        g = new Guest { Id = "g" + Book.NewId(), Email = email, Created = DateTime.UtcNow };
                        site.Guests.Add(g);
                    }
                    g.First = first;
                    g.Last = last;
                    if (country != string.Empty)
                    {
                        g.Country = country;
                    }
                    g.Member = true;
                    g.Joined = DateTime.UtcNow;
                    g.Hash = Access.HashPassword(pass);
                    int next = site.Guests.Where(x => x.MemberNo.StartsWith("SC", StringComparison.Ordinal)).Select(x => int.TryParse(x.MemberNo.Substring(2), out int n) ? n : 0).DefaultIfEmpty(204000).Max() + 37;
                    g.MemberNo = "SC" + next.ToString(Ui.Inv);
                    gid = g.Id;
                    no = g.MemberNo;
                    await Store.SaveSite(site, sbx);
                }
            }
            if (err != string.Empty)
            {
                response.SetElementContents("ms-e-jmail", err);
                return response;
            }
            await Store.Log(sbx, "web", "member-join", no, true);
            string tok = await Auth.Open(sbx, gid, DateTime.UtcNow.AddHours(8), "member");
            response.ExecuteScript("MembershipJs.signedIn('" + tok + "');location.href='MyCircle?welcome=1';");
            return response;
        }
    }
}
