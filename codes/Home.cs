using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyHotel.Core;
using SkyHotel.Models;
using SkyNet;

namespace SkyHotel.codes
{
    public class Home : WebPage
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
                .Replace("{plhd_style}", SafeColor(site.Settings.Accent) ? "--hm-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_nav}", Nav("hm", "Home", false))
                .Replace("{plhd_drawer}", Nav("hm", "Home", true))
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
                response.ExecuteScript("HomeJs.hydrated(false);");
                return response;
            }
            SiteData site = await Store.LoadSite(sbx);
            View v = Render(site, Ui.Clip(GetDataValue("argA"), 80), Ui.Clip(GetDataValue("argB"), 80));
            response.SetElementContents("hm-main", v.Main);
            response.SetElementContents("hm-brand", Ui.Enc(site.Settings.Name));
            response.SetElementContents("hm-town", Ui.Enc(site.Settings.Town));
            response.SetElementContents("hm-ftag", Ui.Enc(site.Settings.Tagline));
            response.SetElementContents("hm-faddr", Ui.Enc(site.Settings.Address) + "<br><a href=\"tel:" + Ui.Attr(Regex.Replace(site.Settings.Phone, "[^0-9+]", string.Empty)) + "\">" + Ui.Enc(site.Settings.Phone) + "</a> &middot; <a href=\"mailto:" + Ui.Attr(site.Settings.Email) + "\">" + Ui.Enc(site.Settings.Email) + "</a>");
            string accent = SafeColor(site.Settings.Accent) ? site.Settings.Accent : string.Empty;
            response.ExecuteScript("HomeJs.hydrated(true,'" + accent + "','" + (site.Settings.Theme == "dark" ? "dark" : "light") + "');");
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
                response.ExecuteScript("HomeJs.sandboxed('" + sbx + "');");
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
                response.ExecuteScript("HomeJs.who('', '');");
                return response;
            }
            SiteData site = await Store.LoadSite(m.Sbx);
            Guest? g = site.Guests.FirstOrDefault(x => x.Id == m.Guest && x.Member);
            if (g == null)
            {
                response.ExecuteScript("HomeJs.who('', '');");
                return response;
            }
            response.ExecuteScript("HomeJs.who('" + Ui.Js(Ui.Enc(g.First)) + "','" + Ui.GradeOf(g.Lifetime).Key + "');");
            return response;
        }

        private static string Stars(int n)
        {
            int k = Math.Clamp(n, 0, 5);
            return "<span class=\"hm-stars\" aria-label=\"" + k + " out of 5\">" + new string('★', k) + "<span style=\"opacity:0.3\">" + new string('★', 5 - k) + "</span></span>";
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
            return "<article class=\"hm-card\"><a class=\"hm-card-img\" href=\"Room?t=" + Uri.EscapeDataString(t.Key) + "\">" + Ui.Pic(img, t.Name, string.Empty, true, !lazy) + "</a><div class=\"hm-card-body\"><h3><a href=\"Room?t=" + Uri.EscapeDataString(t.Key) + "\">" + Ui.Enc(t.Name) + "</a></h3><div class=\"hm-facts\"><span>" + t.Size + " m&sup2;</span><span>" + Ui.Enc(t.Beds) + "</span><span>" + Ui.Enc(t.View) + " view</span><span>Up to " + t.MaxGuests + " guests</span></div><p>" + Ui.Enc(t.Short) + "</p><div class=\"hm-from\"><div><span>From</span> <b>" + Ui.Money(FromPrice(site, t)) + "</b> <span>/ night</span></div><a class=\"hm-btn hm-btn-sm hm-btn-ghost\" href=\"Reservation?t=" + Uri.EscapeDataString(t.Key) + "\">Book</a></div></div></article>";
        }

        private static string Hero(ContentBlock c, string fallbackTitle)
        {
            return "<section class=\"hm-phero\">" + (c.Image != string.Empty ? Ui.Pic(c.Image, string.Empty, string.Empty, false, true) : string.Empty) + "<div class=\"hm-wrap\"><h1>" + Ui.Enc(c.Title != string.Empty ? c.Title : fallbackTitle) + "</h1>" + (c.Text != string.Empty ? "<p>" + Ui.Enc(c.Text) + "</p>" : string.Empty) + "</div></section>";
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
            ContentBlock home = Block(site, "home");
            ContentBlock intro = Block(site, "intro");
            ContentBlock ms = Block(site, "membership");
            DateTime today = DateTime.UtcNow.Date;
            StringBuilder sb = new StringBuilder();
            string hvid = home.Video != string.Empty ? "<video class=\"hm-hero-img hm-hero-vid\" id=\"hm-hvid\" src=\"" + Ui.Attr(home.Video) + "\" autoplay muted loop playsinline preload=\"auto\" aria-hidden=\"true\"></video><button type=\"button\" class=\"hm-hplay\" id=\"hm-hplay\" onclick=\"HomeJs.heroToggle()\" aria-label=\"Pause background video\"></button>" : string.Empty;
            sb.Append("<section class=\"hm-hero\">" + (home.Image != string.Empty ? Ui.Pic(home.Image, string.Empty, "hm-hero-img", false, true) : string.Empty) + hvid + "<div class=\"hm-wrap hm-hero-in\"><span class=\"hm-kick\">" + Ui.Enc(site.Settings.Town) + "</span><h1>" + Ui.Enc(home.Title) + "</h1><p>" + Ui.Enc(home.Text) + "</p></div></section>");
            sb.Append("<div class=\"hm-wrap\"><form class=\"hm-bbar\" action=\"Reservation\" method=\"get\" aria-label=\"Check availability\">");
            sb.Append("<label class=\"hm-field\"><span>Check-in</span><input type=\"date\" name=\"ci\" id=\"hm-ci\" min=\"" + Ui.Ymd(today) + "\" value=\"" + Ui.Ymd(today.AddDays(1)) + "\" required></label>");
            sb.Append("<label class=\"hm-field\"><span>Check-out</span><input type=\"date\" name=\"co\" id=\"hm-co\" min=\"" + Ui.Ymd(today.AddDays(1)) + "\" value=\"" + Ui.Ymd(today.AddDays(3)) + "\" required></label>");
            sb.Append("<label class=\"hm-field\"><span>Adults</span><select name=\"ad\">" + Ui.Opts(new[] { ("1", "1 adult"), ("2", "2 adults"), ("3", "3 adults"), ("4", "4 adults") }, "2") + "</select></label>");
            sb.Append("<label class=\"hm-field\"><span>Children</span><select name=\"ch\">" + Ui.Opts(new[] { ("0", "No children"), ("1", "1 child"), ("2", "2 children"), ("3", "3 children") }, "0") + "</select></label>");
            sb.Append("<button type=\"submit\" class=\"hm-btn hm-btn-gold\">Check availability</button></form></div>");
            sb.Append("<section class=\"hm-sec\"><div class=\"hm-wrap hm-intro\"><div><span class=\"hm-kick\">Welcome</span><h2 class=\"hm-h2\">" + Ui.Enc(intro.Title) + "</h2><p class=\"hm-lead\">" + Ui.Enc(intro.Text) + "</p><div class=\"hm-stats\">");
            foreach (ContentItem it in intro.Items)
            {
                sb.Append("<div><b>" + Ui.Enc(it.Name) + "</b><span>" + Ui.Enc(it.Meta) + "</span></div>");
            }
            sb.Append("</div></div>" + (intro.Image != string.Empty ? "<div class=\"hm-intro-img\">" + Ui.Pic(intro.Image, string.Empty, string.Empty) + "</div>" : string.Empty) + "</div></section>");
            sb.Append("<section class=\"hm-sec\"><div class=\"hm-wrap\"><div class=\"hm-shead\"><div><span class=\"hm-kick\">Rooms &amp; suites</span><h2 class=\"hm-h2\">Choose your view</h2></div><a class=\"hm-more\" href=\"Rooms\">All rooms &rarr;</a></div><div class=\"hm-cards\">");
            foreach (RoomType t in site.Types.Where(x => x.Active).OrderBy(x => x.Order))
            {
                sb.Append(RoomCard(site, t));
            }
            sb.Append("</div></div></section>");
            (string Href, string Img, string Title, string Text)[] tiles =
            {
                ("Dining", "images/hotel/full/restaurant.webp", "Tidewater & Lantern Bar", "Harbour seafood, local wine and late cocktails."),
                ("Spa", "images/hotel/full/spa-pool.webp", "Spa & pool", "An indoor pool, a wooden sauna and three treatment rooms."),
                ("Spa", "images/hotel/full/rooftop-pool.webp", "Rooftop pool", "Swim at sunset above the boats, May to September.")
            };
            sb.Append("<section class=\"hm-sec\"><div class=\"hm-wrap\"><div class=\"hm-shead\"><div><span class=\"hm-kick\">At the hotel</span><h2 class=\"hm-h2\">Eat, unwind, swim</h2></div></div><div class=\"hm-tiles\">");
            foreach ((string href, string img, string title, string text) in tiles)
            {
                sb.Append("<a class=\"hm-tile\" href=\"" + href + "\">" + Ui.Pic(img, string.Empty, string.Empty, true) + "<span><b>" + Ui.Enc(title) + "</b><small>" + Ui.Enc(text) + "</small></span></a>");
            }
            sb.Append("</div></div></section>");
            List<Offer> offers = site.Offers.Where(o => OfferLive(o) && !o.Members).Take(3).ToList();
            if (offers.Count > 0)
            {
                sb.Append("<section class=\"hm-sec\"><div class=\"hm-wrap\"><div class=\"hm-shead\"><div><span class=\"hm-kick\">Offers</span><h2 class=\"hm-h2\">Stay longer, pay less</h2></div><a class=\"hm-more\" href=\"Offers\">All offers &rarr;</a></div><div class=\"hm-cards\">");
                foreach (Offer o in offers)
                {
                    sb.Append("<article class=\"hm-card\"><div class=\"hm-card-img\">" + (o.Pct > 0 ? "<span class=\"hm-tag\">" + o.Pct + "% off</span>" : string.Empty) + Ui.Pic(o.Image, string.Empty, string.Empty, true) + "</div><div class=\"hm-card-body\"><h3>" + Ui.Enc(o.Title) + "</h3><p>" + Ui.Enc(o.Text) + "</p><div class=\"hm-from\"><span>Code <b class=\"hm-code\">" + Ui.Enc(o.Code) + "</b></span><a class=\"hm-btn hm-btn-sm hm-btn-ghost\" href=\"Reservation?promo=" + Uri.EscapeDataString(o.Code) + "\">Book</a></div></div></article>");
                }
                sb.Append("</div></div></section>");
            }
            sb.Append("<section class=\"hm-sec\"><div class=\"hm-wrap\"><div class=\"hm-band\"><div><span class=\"hm-kick\">SkyHotel Circle</span><h2>" + Ui.Enc(ms.Title) + "</h2><p>" + Ui.Enc(ms.Text) + "</p><div class=\"hm-band-acts\"><a class=\"hm-btn hm-btn-gold\" href=\"Membership\">Join for free</a><a class=\"hm-btn hm-btn-ghost hm-btn-light\" href=\"MyCircle\">Member sign in</a></div></div><div class=\"hm-band-grades\">");
            foreach ((string key, string name, int min, int pct, string perks) in Ui.Grades.Skip(1))
            {
                sb.Append("<div><span class=\"hm-grade hm-g-" + key + "\">" + name + "</span><span>" + Ui.Num(min) + " pts &middot; " + pct + "% off</span></div>");
            }
            sb.Append("</div></div></div></section>");
            List<Review> revs = site.Reviews.Where(r => r.Status == "approved").OrderByDescending(r => r.When).ToList();
            if (revs.Count > 0)
            {
                double avg = revs.Average(r => r.Rating);
                sb.Append("<section class=\"hm-sec\"><div class=\"hm-wrap\"><div class=\"hm-shead\"><div><span class=\"hm-kick\">Guest reviews</span><h2 class=\"hm-h2\">" + avg.ToString("0.0", Ui.Inv) + " out of 5</h2><p class=\"hm-lead\">From " + revs.Count + " verified stays at the hotel.</p></div></div><div class=\"hm-cards\">");
                foreach (Review r in revs.Take(3))
                {
                    sb.Append("<article class=\"hm-box hm-review\">" + Stars(r.Rating) + "<h3>" + Ui.Enc(r.Title) + "</h3><q>" + Ui.Enc(r.Text) + "</q><small>" + Ui.Enc(r.Name) + " &middot; " + r.When.ToString("MMMM yyyy", Ui.Inv) + "</small></article>");
                }
                sb.Append("</div></div></section>");
            }
            ContentBlock loc = Block(site, "location");
            sb.Append("<section class=\"hm-sec\"><div class=\"hm-wrap hm-intro hm-intro-r\">" + (loc.Image != string.Empty ? "<div class=\"hm-intro-img\">" + Ui.Pic(loc.Image, string.Empty, string.Empty, true) + "</div>" : string.Empty) + "<div><span class=\"hm-kick\">Location</span><h2 class=\"hm-h2\">" + Ui.Enc(loc.Title) + "</h2><p class=\"hm-lead\">" + Ui.Enc(loc.Text) + "</p><p style=\"margin-top:20px\"><a class=\"hm-btn hm-btn-ghost\" href=\"Contact\">Directions &amp; contact</a></p></div></div></section>");
            return new View { Main = sb.ToString(), Desc = home.Text };
        }
    }
}
