using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyHotel.Core;
using SkyHotel.Models;
using SkyNet;

namespace SkyHotel.codes
{
    public class Rooms : WebPage
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
                .Replace("{plhd_style}", SafeColor(site.Settings.Accent) ? "--rm-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_nav}", Nav("rm", "Rooms", false))
                .Replace("{plhd_drawer}", Nav("rm", "Rooms", true))
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
                response.ExecuteScript("RoomsJs.hydrated(false);");
                return response;
            }
            SiteData site = await Store.LoadSite(sbx);
            View v = Render(site, Ui.Clip(GetDataValue("argA"), 80), Ui.Clip(GetDataValue("argB"), 80));
            response.SetElementContents("rm-main", v.Main);
            response.SetElementContents("rm-brand", Ui.Enc(site.Settings.Name));
            response.SetElementContents("rm-town", Ui.Enc(site.Settings.Town));
            response.SetElementContents("rm-ftag", Ui.Enc(site.Settings.Tagline));
            response.SetElementContents("rm-faddr", Ui.Enc(site.Settings.Address) + "<br><a href=\"tel:" + Ui.Attr(Regex.Replace(site.Settings.Phone, "[^0-9+]", string.Empty)) + "\">" + Ui.Enc(site.Settings.Phone) + "</a> &middot; <a href=\"mailto:" + Ui.Attr(site.Settings.Email) + "\">" + Ui.Enc(site.Settings.Email) + "</a>");
            string accent = SafeColor(site.Settings.Accent) ? site.Settings.Accent : string.Empty;
            response.ExecuteScript("RoomsJs.hydrated(true,'" + accent + "','" + (site.Settings.Theme == "dark" ? "dark" : "light") + "');");
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
                response.ExecuteScript("RoomsJs.sandboxed('" + sbx + "');");
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
                response.ExecuteScript("RoomsJs.who('', '');");
                return response;
            }
            SiteData site = await Store.LoadSite(m.Sbx);
            Guest? g = site.Guests.FirstOrDefault(x => x.Id == m.Guest && x.Member);
            if (g == null)
            {
                response.ExecuteScript("RoomsJs.who('', '');");
                return response;
            }
            response.ExecuteScript("RoomsJs.who('" + Ui.Js(Ui.Enc(g.First)) + "','" + Ui.GradeOf(g.Lifetime).Key + "');");
            return response;
        }

        private static string Stars(int n)
        {
            int k = Math.Clamp(n, 0, 5);
            return "<span class=\"rm-stars\" aria-label=\"" + k + " out of 5\">" + new string('★', k) + "<span style=\"opacity:0.3\">" + new string('★', 5 - k) + "</span></span>";
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
            return "<article class=\"rm-card\"><a class=\"rm-card-img\" href=\"Room?t=" + Uri.EscapeDataString(t.Key) + "\">" + Ui.Pic(img, t.Name, string.Empty, true, !lazy) + "</a><div class=\"rm-card-body\"><h3><a href=\"Room?t=" + Uri.EscapeDataString(t.Key) + "\">" + Ui.Enc(t.Name) + "</a></h3><div class=\"rm-facts\"><span>" + t.Size + " m&sup2;</span><span>" + Ui.Enc(t.Beds) + "</span><span>" + Ui.Enc(t.View) + " view</span><span>Up to " + t.MaxGuests + " guests</span></div><p>" + Ui.Enc(t.Short) + "</p><div class=\"rm-from\"><div><span>From</span> <b>" + Ui.Money(FromPrice(site, t)) + "</b> <span>/ night</span></div><a class=\"rm-btn rm-btn-sm rm-btn-ghost\" href=\"Reservation?t=" + Uri.EscapeDataString(t.Key) + "\">Book</a></div></div></article>";
        }

        private static string Hero(ContentBlock c, string fallbackTitle)
        {
            return "<section class=\"rm-phero\">" + (c.Image != string.Empty ? Ui.Pic(c.Image, string.Empty, string.Empty, false, true) : string.Empty) + "<div class=\"rm-wrap\"><h1>" + Ui.Enc(c.Title != string.Empty ? c.Title : fallbackTitle) + "</h1>" + (c.Text != string.Empty ? "<p>" + Ui.Enc(c.Text) + "</p>" : string.Empty) + "</div></section>";
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
            StringBuilder sb = new StringBuilder("<div class=\"rm-wrap\"><div class=\"rm-phead\"><span class=\"rm-kick\">Rooms &amp; suites</span><h1>Forty rooms above the harbour</h1><p class=\"rm-lead\">Six room types, from cosy Classic Rooms in the old-town wing to two Penthouse Suites with private roof terraces.</p></div>");
            sb.Append("<div class=\"rm-rfilter\"><div class=\"rm-chips\" id=\"rm-rchips\"><button type=\"button\" class=\"rm-chip rm-act\" data-f=\"all\" onclick=\"RoomsJs.filter('all')\">All rooms</button><button type=\"button\" class=\"rm-chip\" data-f=\"harbour\" onclick=\"RoomsJs.filter('harbour')\">Harbour view</button><button type=\"button\" class=\"rm-chip\" data-f=\"family\" onclick=\"RoomsJs.filter('family')\">Sleeps 3 or more</button><button type=\"button\" class=\"rm-chip\" data-f=\"suite\" onclick=\"RoomsJs.filter('suite')\">Suites</button></div><label class=\"rm-sortl\"><span>Sort</span><select id=\"rm-rsort\" onchange=\"RoomsJs.sortRooms()\"><option value=\"\">Recommended</option><option value=\"low\">Price: low to high</option><option value=\"high\">Price: high to low</option><option value=\"size\">Largest first</option></select></label></div>");
            sb.Append("<div class=\"rm-cards\" id=\"rm-rlist\">");
            foreach (RoomType t in site.Types.Where(x => x.Active).OrderBy(x => x.Order))
            {
                List<string> tags = new List<string>();
                if (t.View == "Harbour" || t.View == "Panoramic")
                {
                    tags.Add("harbour");
                }
                if (t.MaxGuests >= 3)
                {
                    tags.Add("family");
                }
                if (t.Key == "junior" || t.Key == "penthouse")
                {
                    tags.Add("suite");
                }
                sb.Append("<div class=\"rm-ritem\" data-tags=\"" + string.Join(" ", tags) + "\" data-price=\"" + FromPrice(site, t).ToString(Ui.Inv) + "\" data-size=\"" + t.Size + "\" data-order=\"" + t.Order + "\">" + RoomCard(site, t) + "</div>");
            }
            sb.Append("</div><p class=\"rm-none\" id=\"rm-rnone\" hidden>No rooms match. Try another filter.</p>");
            sb.Append("<div class=\"rm-info-box rm-rnote\">Prices shown are the lowest nightly rate in the next 30 days for the flexible rate, before tax. Weekend nights (Friday and Saturday) and festival dates cost more. SkyHotel Circle members save 5&ndash;15%.</div></div>");
            return new View { Title = "Rooms & suites", Main = sb.ToString(), Desc = "Rooms and suites at SkyHotel, Port Aster." };
        }
    }
}
