using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyHotel.Core;
using SkyHotel.Models;
using SkyNet;

namespace SkyHotel.codes
{
    public class Contact : WebPage
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
                .Replace("{plhd_style}", SafeColor(site.Settings.Accent) ? "--ct-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_nav}", Nav("ct", "Contact", false))
                .Replace("{plhd_drawer}", Nav("ct", "Contact", true))
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
                response.ExecuteScript("ContactJs.hydrated(false);");
                return response;
            }
            SiteData site = await Store.LoadSite(sbx);
            View v = Render(site, Ui.Clip(GetDataValue("argA"), 80), Ui.Clip(GetDataValue("argB"), 80));
            response.SetElementContents("ct-main", v.Main);
            response.SetElementContents("ct-brand", Ui.Enc(site.Settings.Name));
            response.SetElementContents("ct-town", Ui.Enc(site.Settings.Town));
            response.SetElementContents("ct-ftag", Ui.Enc(site.Settings.Tagline));
            response.SetElementContents("ct-faddr", Ui.Enc(site.Settings.Address) + "<br><a href=\"tel:" + Ui.Attr(Regex.Replace(site.Settings.Phone, "[^0-9+]", string.Empty)) + "\">" + Ui.Enc(site.Settings.Phone) + "</a> &middot; <a href=\"mailto:" + Ui.Attr(site.Settings.Email) + "\">" + Ui.Enc(site.Settings.Email) + "</a>");
            string accent = SafeColor(site.Settings.Accent) ? site.Settings.Accent : string.Empty;
            response.ExecuteScript("ContactJs.hydrated(true,'" + accent + "','" + (site.Settings.Theme == "dark" ? "dark" : "light") + "');");
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
                response.ExecuteScript("ContactJs.sandboxed('" + sbx + "');");
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
                response.ExecuteScript("ContactJs.who('', '');");
                return response;
            }
            SiteData site = await Store.LoadSite(m.Sbx);
            Guest? g = site.Guests.FirstOrDefault(x => x.Id == m.Guest && x.Member);
            if (g == null)
            {
                response.ExecuteScript("ContactJs.who('', '');");
                return response;
            }
            response.ExecuteScript("ContactJs.who('" + Ui.Js(Ui.Enc(g.First)) + "','" + Ui.GradeOf(g.Lifetime).Key + "');");
            return response;
        }

        private static string Stars(int n)
        {
            int k = Math.Clamp(n, 0, 5);
            return "<span class=\"ct-stars\" aria-label=\"" + k + " out of 5\">" + new string('★', k) + "<span style=\"opacity:0.3\">" + new string('★', 5 - k) + "</span></span>";
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
            return "<article class=\"ct-card\"><a class=\"ct-card-img\" href=\"Room?t=" + Uri.EscapeDataString(t.Key) + "\">" + Ui.Pic(img, t.Name, string.Empty, true, !lazy) + "</a><div class=\"ct-card-body\"><h3><a href=\"Room?t=" + Uri.EscapeDataString(t.Key) + "\">" + Ui.Enc(t.Name) + "</a></h3><div class=\"ct-facts\"><span>" + t.Size + " m&sup2;</span><span>" + Ui.Enc(t.Beds) + "</span><span>" + Ui.Enc(t.View) + " view</span><span>Up to " + t.MaxGuests + " guests</span></div><p>" + Ui.Enc(t.Short) + "</p><div class=\"ct-from\"><div><span>From</span> <b>" + Ui.Money(FromPrice(site, t)) + "</b> <span>/ night</span></div><a class=\"ct-btn ct-btn-sm ct-btn-ghost\" href=\"Reservation?t=" + Uri.EscapeDataString(t.Key) + "\">Book</a></div></div></article>";
        }

        private static string Hero(ContentBlock c, string fallbackTitle)
        {
            return "<section class=\"ct-phero\">" + (c.Image != string.Empty ? Ui.Pic(c.Image, string.Empty, string.Empty, false, true) : string.Empty) + "<div class=\"ct-wrap\"><h1>" + Ui.Enc(c.Title != string.Empty ? c.Title : fallbackTitle) + "</h1>" + (c.Text != string.Empty ? "<p>" + Ui.Enc(c.Text) + "</p>" : string.Empty) + "</div></section>";
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
            ContentBlock c = Block(site, "location");
            StringBuilder sb = new StringBuilder(Hero(c, "Location & contact"));
            sb.Append("<div class=\"ct-wrap\"><section class=\"ct-sec ct-cgrid\"><div class=\"ct-box\"><h2>Contact</h2><dl class=\"ct-dl\"><dt>Address</dt><dd>" + Ui.Enc(site.Settings.Address) + "</dd><dt>Phone</dt><dd><a href=\"tel:" + Ui.Attr(Regex.Replace(site.Settings.Phone, "[^0-9+]", string.Empty)) + "\">" + Ui.Enc(site.Settings.Phone) + "</a> &middot; front desk, 24 hours</dd><dt>Email</dt><dd><a href=\"mailto:" + Ui.Attr(site.Settings.Email) + "\">" + Ui.Enc(site.Settings.Email) + "</a></dd><dt>Check-in</dt><dd>From " + Ui.Enc(site.Settings.CheckIn) + "</dd><dt>Check-out</dt><dd>Until " + Ui.Enc(site.Settings.CheckOut) + "</dd></dl></div><div><h2 class=\"ct-h2\">Getting here</h2><div class=\"ct-dirs\">");
            foreach (ContentItem it in c.Items)
            {
                sb.Append("<div class=\"ct-dir\"><b>" + Ui.Enc(it.Name) + "</b><span>" + Ui.Enc(it.Meta) + "</span><p>" + Ui.Enc(it.Text) + "</p></div>");
            }
            sb.Append("</div></div></section><section class=\"ct-sec\"><h2 class=\"ct-h2\">Good to know</h2><div class=\"ct-faq\">");
            (string Q, string A)[] faq =
            {
                ("Can I check in early?", "Rooms are ready from " + site.Settings.CheckIn + ". If you arrive earlier, we'll keep your bags and you can use the spa while you wait."),
                ("Is breakfast included?", "Breakfast is included on the Bed & breakfast rate and for Gold and Diamond members. Otherwise it's " + Ui.Money(site.Settings.Breakfast) + " per person."),
                ("How do I cancel?", "Use Manage a booking with your booking code and last name. Flexible rates are free to cancel until " + site.Settings.CancelHours + " hours before arrival."),
                ("Do you allow pets?", "Well-behaved dogs are welcome in Classic and Family Rooms for $30 per night."),
                ("When do I get my Circle points?", "Points are added when you check out, one point for every dollar spent on rooms, food and spa.")
            };
            foreach ((string q, string ans) in faq)
            {
                sb.Append("<details class=\"ct-qa\"><summary>" + Ui.Enc(q) + "</summary><p>" + Ui.Enc(ans) + "</p></details>");
            }
            sb.Append("</div></section></div>");
            return new View { Title = "Location & contact", Main = sb.ToString(), Desc = c.Text };
        }
    }
}
