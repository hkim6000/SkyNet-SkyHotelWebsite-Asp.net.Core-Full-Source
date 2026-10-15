using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyHotel.Core;
using SkyHotel.Models;
using SkyNet;

namespace SkyHotel.codes
{
    public class Offers : WebPage
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
                .Replace("{plhd_style}", SafeColor(site.Settings.Accent) ? "--of-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_nav}", Nav("of", "Offers", false))
                .Replace("{plhd_drawer}", Nav("of", "Offers", true))
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
                response.ExecuteScript("OffersJs.hydrated(false);");
                return response;
            }
            SiteData site = await Store.LoadSite(sbx);
            View v = Render(site, Ui.Clip(GetDataValue("argA"), 80), Ui.Clip(GetDataValue("argB"), 80));
            response.SetElementContents("of-main", v.Main);
            response.SetElementContents("of-brand", Ui.Enc(site.Settings.Name));
            response.SetElementContents("of-town", Ui.Enc(site.Settings.Town));
            response.SetElementContents("of-ftag", Ui.Enc(site.Settings.Tagline));
            response.SetElementContents("of-faddr", Ui.Enc(site.Settings.Address) + "<br><a href=\"tel:" + Ui.Attr(Regex.Replace(site.Settings.Phone, "[^0-9+]", string.Empty)) + "\">" + Ui.Enc(site.Settings.Phone) + "</a> &middot; <a href=\"mailto:" + Ui.Attr(site.Settings.Email) + "\">" + Ui.Enc(site.Settings.Email) + "</a>");
            string accent = SafeColor(site.Settings.Accent) ? site.Settings.Accent : string.Empty;
            response.ExecuteScript("OffersJs.hydrated(true,'" + accent + "','" + (site.Settings.Theme == "dark" ? "dark" : "light") + "');");
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
                response.ExecuteScript("OffersJs.sandboxed('" + sbx + "');");
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
                response.ExecuteScript("OffersJs.who('', '');");
                return response;
            }
            SiteData site = await Store.LoadSite(m.Sbx);
            Guest? g = site.Guests.FirstOrDefault(x => x.Id == m.Guest && x.Member);
            if (g == null)
            {
                response.ExecuteScript("OffersJs.who('', '');");
                return response;
            }
            response.ExecuteScript("OffersJs.who('" + Ui.Js(Ui.Enc(g.First)) + "','" + Ui.GradeOf(g.Lifetime).Key + "');");
            return response;
        }

        private static string Stars(int n)
        {
            int k = Math.Clamp(n, 0, 5);
            return "<span class=\"of-stars\" aria-label=\"" + k + " out of 5\">" + new string('★', k) + "<span style=\"opacity:0.3\">" + new string('★', 5 - k) + "</span></span>";
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
            return "<article class=\"of-card\"><a class=\"of-card-img\" href=\"Room?t=" + Uri.EscapeDataString(t.Key) + "\">" + Ui.Pic(img, t.Name, string.Empty, true, !lazy) + "</a><div class=\"of-card-body\"><h3><a href=\"Room?t=" + Uri.EscapeDataString(t.Key) + "\">" + Ui.Enc(t.Name) + "</a></h3><div class=\"of-facts\"><span>" + t.Size + " m&sup2;</span><span>" + Ui.Enc(t.Beds) + "</span><span>" + Ui.Enc(t.View) + " view</span><span>Up to " + t.MaxGuests + " guests</span></div><p>" + Ui.Enc(t.Short) + "</p><div class=\"of-from\"><div><span>From</span> <b>" + Ui.Money(FromPrice(site, t)) + "</b> <span>/ night</span></div><a class=\"of-btn of-btn-sm of-btn-ghost\" href=\"Reservation?t=" + Uri.EscapeDataString(t.Key) + "\">Book</a></div></div></article>";
        }

        private static string Hero(ContentBlock c, string fallbackTitle)
        {
            return "<section class=\"of-phero\">" + (c.Image != string.Empty ? Ui.Pic(c.Image, string.Empty, string.Empty, false, true) : string.Empty) + "<div class=\"of-wrap\"><h1>" + Ui.Enc(c.Title != string.Empty ? c.Title : fallbackTitle) + "</h1>" + (c.Text != string.Empty ? "<p>" + Ui.Enc(c.Text) + "</p>" : string.Empty) + "</div></section>";
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
            List<Offer> live = site.Offers.Where(OfferLive).OrderBy(o => o.Members).ThenByDescending(o => o.Pct).ToList();
            StringBuilder sb = new StringBuilder("<div class=\"of-wrap\"><div class=\"of-phead\"><span class=\"of-kick\">Offers</span><h1>Special offers</h1><p class=\"of-lead\">Enter the code when you book, or tap Book with this code. Codes marked Circle are for SkyHotel Circle members.</p></div>");
            if (live.Count == 0)
            {
                sb.Append("<p class=\"of-none\">No offers right now. Check back soon.</p>");
            }
            sb.Append("<div class=\"of-cards of-olist\">");
            foreach (Offer o in live)
            {
                string cond = (o.MinNights > 1 ? "Minimum " + o.MinNights + " nights. " : string.Empty) + "Book by " + o.To.ToString("d MMMM yyyy", Ui.Inv) + ".";
                sb.Append("<article class=\"of-card\"><div class=\"of-card-img\">" + (o.Members ? "<span class=\"of-tag of-tag-navy\">Circle members</span>" : o.Pct > 0 ? "<span class=\"of-tag\">" + o.Pct + "% off</span>" : string.Empty) + Ui.Pic(o.Image, string.Empty, string.Empty, true) + "</div><div class=\"of-card-body\"><h3>" + Ui.Enc(o.Title) + "</h3><p>" + Ui.Enc(o.Text) + "</p><p class=\"of-hint\">" + Ui.Enc(cond) + (o.Bonus > 1 ? " Earns " + o.Bonus + "&times; points." : string.Empty) + " Valid on " + Ui.Enc(Book.TypesText(site, o)) + ".</p><div class=\"of-from\"><span>Code <b class=\"of-ocode\">" + Ui.Enc(o.Code) + "</b></span><a class=\"of-btn of-btn-sm of-btn-gold\" href=\"Reservation?promo=" + Uri.EscapeDataString(o.Code) + "\">Book with this code</a></div></div></article>");
            }
            sb.Append("</div><section class=\"of-sec\"><div class=\"of-band\"><div><span class=\"of-kick\">SkyHotel Circle</span><h2>Members always pay less</h2><p>Every member saves 5% or more on every stay, earns a point for every dollar and unlocks member-only offers.</p><div class=\"of-band-acts\"><a class=\"of-btn of-btn-gold\" href=\"Membership\">See membership</a></div></div><div class=\"of-band-grades\">");
            foreach ((string key, string name, int min, int pct, string perks) in Ui.Grades)
            {
                sb.Append("<div><span class=\"of-grade of-g-" + key + "\">" + name + "</span><span>" + Ui.Enc(perks) + "</span></div>");
            }
            sb.Append("</div></div></section></div>");
            return new View { Title = "Offers", Main = sb.ToString(), Desc = "Special offers at SkyHotel." };
        }
    }
}
