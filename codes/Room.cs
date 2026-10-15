using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyHotel.Core;
using SkyHotel.Models;
using SkyNet;

namespace SkyHotel.codes
{
    public class Room : WebPage
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
            View v = Render(site, Ui.Clip(QueryValue("t"), 80), Ui.Clip(string.Empty, 80));
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
                .Replace("{plhd_style}", SafeColor(site.Settings.Accent) ? "--ro-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_nav}", Nav("ro", "Room", false))
                .Replace("{plhd_drawer}", Nav("ro", "Room", true))
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
                response.ExecuteScript("RoomJs.hydrated(false);");
                return response;
            }
            SiteData site = await Store.LoadSite(sbx);
            View v = Render(site, Ui.Clip(GetDataValue("argA"), 80), Ui.Clip(GetDataValue("argB"), 80));
            response.SetElementContents("ro-main", v.Main);
            response.SetElementContents("ro-brand", Ui.Enc(site.Settings.Name));
            response.SetElementContents("ro-town", Ui.Enc(site.Settings.Town));
            response.SetElementContents("ro-ftag", Ui.Enc(site.Settings.Tagline));
            response.SetElementContents("ro-faddr", Ui.Enc(site.Settings.Address) + "<br><a href=\"tel:" + Ui.Attr(Regex.Replace(site.Settings.Phone, "[^0-9+]", string.Empty)) + "\">" + Ui.Enc(site.Settings.Phone) + "</a> &middot; <a href=\"mailto:" + Ui.Attr(site.Settings.Email) + "\">" + Ui.Enc(site.Settings.Email) + "</a>");
            string accent = SafeColor(site.Settings.Accent) ? site.Settings.Accent : string.Empty;
            response.ExecuteScript("RoomJs.hydrated(true,'" + accent + "','" + (site.Settings.Theme == "dark" ? "dark" : "light") + "');");
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
                response.ExecuteScript("RoomJs.sandboxed('" + sbx + "');");
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
                response.ExecuteScript("RoomJs.who('', '');");
                return response;
            }
            SiteData site = await Store.LoadSite(m.Sbx);
            Guest? g = site.Guests.FirstOrDefault(x => x.Id == m.Guest && x.Member);
            if (g == null)
            {
                response.ExecuteScript("RoomJs.who('', '');");
                return response;
            }
            response.ExecuteScript("RoomJs.who('" + Ui.Js(Ui.Enc(g.First)) + "','" + Ui.GradeOf(g.Lifetime).Key + "');");
            return response;
        }

        private static string Stars(int n)
        {
            int k = Math.Clamp(n, 0, 5);
            return "<span class=\"ro-stars\" aria-label=\"" + k + " out of 5\">" + new string('★', k) + "<span style=\"opacity:0.3\">" + new string('★', 5 - k) + "</span></span>";
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
            return "<article class=\"ro-card\"><a class=\"ro-card-img\" href=\"Room?t=" + Uri.EscapeDataString(t.Key) + "\">" + Ui.Pic(img, t.Name, string.Empty, true, !lazy) + "</a><div class=\"ro-card-body\"><h3><a href=\"Room?t=" + Uri.EscapeDataString(t.Key) + "\">" + Ui.Enc(t.Name) + "</a></h3><div class=\"ro-facts\"><span>" + t.Size + " m&sup2;</span><span>" + Ui.Enc(t.Beds) + "</span><span>" + Ui.Enc(t.View) + " view</span><span>Up to " + t.MaxGuests + " guests</span></div><p>" + Ui.Enc(t.Short) + "</p><div class=\"ro-from\"><div><span>From</span> <b>" + Ui.Money(FromPrice(site, t)) + "</b> <span>/ night</span></div><a class=\"ro-btn ro-btn-sm ro-btn-ghost\" href=\"Reservation?t=" + Uri.EscapeDataString(t.Key) + "\">Book</a></div></div></article>";
        }

        private static string Hero(ContentBlock c, string fallbackTitle)
        {
            return "<section class=\"ro-phero\">" + (c.Image != string.Empty ? Ui.Pic(c.Image, string.Empty, string.Empty, false, true) : string.Empty) + "<div class=\"ro-wrap\"><h1>" + Ui.Enc(c.Title != string.Empty ? c.Title : fallbackTitle) + "</h1>" + (c.Text != string.Empty ? "<p>" + Ui.Enc(c.Text) + "</p>" : string.Empty) + "</div></section>";
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
            RoomType? t = site.Types.FirstOrDefault(x => x.Key == a && x.Active);
            if (t == null)
            {
                return new View { Title = "Room not found", Main = "<div class=\"ro-wrap ro-phead\"><h1>We couldn&rsquo;t find that room</h1><p class=\"ro-lead\">It may have been renamed.</p><p style=\"margin-top:20px\"><a class=\"ro-btn\" href=\"Rooms\">See all rooms</a></p></div>" };
            }
            DateTime today = DateTime.UtcNow.Date;
            MediaItem? tour = t.Video != string.Empty ? Ui.MediaOf(site, t.Video) : null;
            string tourBtn = t.Video != string.Empty ? "<button type=\"button\" class=\"ro-tour\" id=\"ro-tour\" onclick=\"RoomJs.tour()\" data-src=\"" + Ui.Attr(t.Video) + "\" data-poster=\"" + Ui.Attr(Ui.Poster(site, t.Video)) + "\"><i></i>Watch the room tour" + (tour != null && tour.Seconds > 0 ? "<small>" + Ui.Clock(tour.Seconds) + "</small>" : string.Empty) + "</button>" : string.Empty;
            StringBuilder sb = new StringBuilder("<div class=\"ro-wrap\"><a class=\"ro-back\" href=\"Rooms\">&larr; All rooms</a><div class=\"ro-gal\"><div class=\"ro-gmain\" id=\"ro-gmain\">" + (t.Images.Count > 0 ? Ui.Pic(t.Images[0], t.Name, string.Empty, false, true).Replace("<img ", "<img id=\"ro-gimg\" ") : string.Empty) + tourBtn + "</div><div class=\"ro-gthumbs\">");
            for (int i = 0; i < t.Images.Count; i++)
            {
                sb.Append("<button type=\"button\" class=\"ro-gth" + (i == 0 ? " ro-act" : string.Empty) + "\" onclick=\"RoomJs.pic(this,'" + Ui.Attr(t.Images[i]) + "')\" aria-label=\"Photo " + (i + 1) + "\">" + Ui.Pic(t.Images[i], string.Empty, string.Empty, true) + "</button>");
            }
            sb.Append("</div></div><div class=\"ro-rgrid\"><div><span class=\"ro-kick\">" + Ui.Enc(t.View) + " view</span><h1 class=\"ro-rtitle\">" + Ui.Enc(t.Name) + "</h1><div class=\"ro-facts ro-rfacts\"><span>" + t.Size + " m&sup2;</span><span>" + Ui.Enc(t.Beds) + "</span><span>Up to " + t.MaxGuests + " guests</span><span>Check-in " + Ui.Enc(site.Settings.CheckIn) + " &middot; Check-out " + Ui.Enc(site.Settings.CheckOut) + "</span></div><p class=\"ro-lead\">" + Ui.Enc(t.Text) + "</p><h2 class=\"ro-sub\">In the room</h2><div class=\"ro-amen\">");
            foreach (string am in t.Amenities)
            {
                sb.Append("<span>" + Ui.Enc(am) + "</span>");
            }
            sb.Append("</div><h2 class=\"ro-sub\">Nightly rates</h2><p class=\"ro-hint\">Flexible rate per night before tax for the next five weeks. Full means no " + Ui.Enc(t.Name) + " is free that night.</p><div class=\"ro-cal\">");
            foreach (string dn in new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" })
            {
                sb.Append("<b>" + dn + "</b>");
            }
            int lead = ((int)today.DayOfWeek + 6) % 7;
            for (int i = 0; i < lead; i++)
            {
                sb.Append("<span class=\"ro-cday ro-cblank\"></span>");
            }
            for (int i = 0; i < 35; i++)
            {
                DateTime d = today.AddDays(i);
                int free = Book.Free(site, t.Key, d);
                bool wk = d.DayOfWeek == DayOfWeek.Friday || d.DayOfWeek == DayOfWeek.Saturday;
                sb.Append("<span class=\"ro-cday" + (free <= 0 ? " ro-cfull" : wk ? " ro-cwk" : string.Empty) + "\"><i>" + (d.Day == 1 || i == 0 ? d.ToString("d MMM", Ui.Inv) : d.Day.ToString(Ui.Inv)) + "</i><b>" + (free <= 0 ? "Full" : Ui.Money(Book.Rate(site, t, d))) + "</b>" + (free > 0 && free <= 2 ? "<em>" + free + " left</em>" : string.Empty) + "</span>");
            }
            sb.Append("</div></div><aside class=\"ro-bookbox\"><div class=\"ro-box\"><span class=\"ro-hint\">From</span><div class=\"ro-bprice\"><b>" + Ui.Money(FromPrice(site, t)) + "</b> / night</div><form class=\"ro-form\" action=\"Reservation\" method=\"get\"><input type=\"hidden\" name=\"t\" value=\"" + Ui.Attr(t.Key) + "\"><div class=\"ro-row2\"><label class=\"ro-field\"><span>Check-in</span><input type=\"date\" name=\"ci\" min=\"" + Ui.Ymd(today) + "\" value=\"" + Ui.Ymd(today.AddDays(1)) + "\"></label><label class=\"ro-field\"><span>Check-out</span><input type=\"date\" name=\"co\" min=\"" + Ui.Ymd(today.AddDays(1)) + "\" value=\"" + Ui.Ymd(today.AddDays(3)) + "\"></label></div><div class=\"ro-row2\"><label class=\"ro-field\"><span>Adults</span><select name=\"ad\">" + Ui.Opts(Enumerable.Range(1, t.MaxGuests).Select(n => (n.ToString(Ui.Inv), n + (n == 1 ? " adult" : " adults"))), "2") + "</select></label><label class=\"ro-field\"><span>Children</span><select name=\"ch\">" + Ui.Opts(Enumerable.Range(0, t.MaxGuests).Select(n => (n.ToString(Ui.Inv), n == 0 ? "None" : n + (n == 1 ? " child" : " children"))), "0") + "</select></label></div><button type=\"submit\" class=\"ro-btn ro-btn-gold ro-btn-wide\">Check availability</button></form><p class=\"ro-hint\" style=\"margin-top:12px\">Free cancellation until " + site.Settings.CancelHours + " hours before arrival on flexible rates. Members save up to 15%.</p></div></aside></div>");
            sb.Append("<section class=\"ro-sec\"><div class=\"ro-shead\"><h2 class=\"ro-h2\">Other rooms</h2><a class=\"ro-more\" href=\"Rooms\">All rooms &rarr;</a></div><div class=\"ro-cards\">");
            foreach (RoomType o in site.Types.Where(x => x.Active && x.Key != t.Key).OrderBy(x => Math.Abs(x.Order - t.Order)).Take(3))
            {
                sb.Append(RoomCard(site, o));
            }
            sb.Append("</div></section></div>");
            return new View { Title = t.Name, Main = sb.ToString(), Desc = t.Short };
        }
    }
}
