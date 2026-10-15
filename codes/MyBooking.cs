using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyHotel.Core;
using SkyHotel.Models;
using SkyNet;

namespace SkyHotel.codes
{
    public class MyBooking : WebPage
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
            View v = Render(site, Ui.Clip(QueryValue("c"), 80), Ui.Clip(string.Empty, 80));
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
                .Replace("{plhd_style}", SafeColor(site.Settings.Accent) ? "--mb-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_nav}", Nav("mb", "MyBooking", false))
                .Replace("{plhd_drawer}", Nav("mb", "MyBooking", true))
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
                response.ExecuteScript("MyBookingJs.hydrated(false);");
                return response;
            }
            SiteData site = await Store.LoadSite(sbx);
            View v = Render(site, Ui.Clip(GetDataValue("argA"), 80), Ui.Clip(GetDataValue("argB"), 80));
            response.SetElementContents("mb-main", v.Main);
            response.SetElementContents("mb-brand", Ui.Enc(site.Settings.Name));
            response.SetElementContents("mb-town", Ui.Enc(site.Settings.Town));
            response.SetElementContents("mb-ftag", Ui.Enc(site.Settings.Tagline));
            response.SetElementContents("mb-faddr", Ui.Enc(site.Settings.Address) + "<br><a href=\"tel:" + Ui.Attr(Regex.Replace(site.Settings.Phone, "[^0-9+]", string.Empty)) + "\">" + Ui.Enc(site.Settings.Phone) + "</a> &middot; <a href=\"mailto:" + Ui.Attr(site.Settings.Email) + "\">" + Ui.Enc(site.Settings.Email) + "</a>");
            string accent = SafeColor(site.Settings.Accent) ? site.Settings.Accent : string.Empty;
            response.ExecuteScript("MyBookingJs.hydrated(true,'" + accent + "','" + (site.Settings.Theme == "dark" ? "dark" : "light") + "');");
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
                response.ExecuteScript("MyBookingJs.sandboxed('" + sbx + "');");
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
                response.ExecuteScript("MyBookingJs.who('', '');");
                return response;
            }
            SiteData site = await Store.LoadSite(m.Sbx);
            Guest? g = site.Guests.FirstOrDefault(x => x.Id == m.Guest && x.Member);
            if (g == null)
            {
                response.ExecuteScript("MyBookingJs.who('', '');");
                return response;
            }
            response.ExecuteScript("MyBookingJs.who('" + Ui.Js(Ui.Enc(g.First)) + "','" + Ui.GradeOf(g.Lifetime).Key + "');");
            return response;
        }

        private static string Stars(int n)
        {
            int k = Math.Clamp(n, 0, 5);
            return "<span class=\"mb-stars\" aria-label=\"" + k + " out of 5\">" + new string('★', k) + "<span style=\"opacity:0.3\">" + new string('★', 5 - k) + "</span></span>";
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
            return "<article class=\"mb-card\"><a class=\"mb-card-img\" href=\"Room?t=" + Uri.EscapeDataString(t.Key) + "\">" + Ui.Pic(img, t.Name, string.Empty, true, !lazy) + "</a><div class=\"mb-card-body\"><h3><a href=\"Room?t=" + Uri.EscapeDataString(t.Key) + "\">" + Ui.Enc(t.Name) + "</a></h3><div class=\"mb-facts\"><span>" + t.Size + " m&sup2;</span><span>" + Ui.Enc(t.Beds) + "</span><span>" + Ui.Enc(t.View) + " view</span><span>Up to " + t.MaxGuests + " guests</span></div><p>" + Ui.Enc(t.Short) + "</p><div class=\"mb-from\"><div><span>From</span> <b>" + Ui.Money(FromPrice(site, t)) + "</b> <span>/ night</span></div><a class=\"mb-btn mb-btn-sm mb-btn-ghost\" href=\"Reservation?t=" + Uri.EscapeDataString(t.Key) + "\">Book</a></div></div></article>";
        }

        private static string Hero(ContentBlock c, string fallbackTitle)
        {
            return "<section class=\"mb-phero\">" + (c.Image != string.Empty ? Ui.Pic(c.Image, string.Empty, string.Empty, false, true) : string.Empty) + "<div class=\"mb-wrap\"><h1>" + Ui.Enc(c.Title != string.Empty ? c.Title : fallbackTitle) + "</h1>" + (c.Text != string.Empty ? "<p>" + Ui.Enc(c.Text) + "</p>" : string.Empty) + "</div></section>";
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
            string code = Regex.IsMatch(a ?? string.Empty, "^[A-Za-z0-9-]{4,12}$") ? a!.ToUpperInvariant() : string.Empty;
            StringBuilder sb = new StringBuilder("<div class=\"mb-wrap mb-mbwrap\"><div class=\"mb-phead\"><span class=\"mb-kick\">Manage a booking</span><h1>Find your booking</h1><p class=\"mb-lead\">Enter the booking code from your confirmation and the last name on the booking.</p></div>");
            sb.Append("<form class=\"mb-box mb-lookup\" onsubmit=\"return MyBookingJs.lookup()\" novalidate><label class=\"mb-field\"><span>Booking code</span><input type=\"text\" id=\"mb-bcode\" maxlength=\"12\" autocomplete=\"off\" placeholder=\"SKH-XXXXXX\" value=\"" + Ui.Attr(code) + "\"></label><label class=\"mb-field\"><span>Last name</span><input type=\"text\" id=\"mb-lname\" maxlength=\"40\" autocomplete=\"family-name\"></label><button type=\"submit\" class=\"mb-btn mb-btn-gold\">Find booking</button></form><em class=\"mb-err\" id=\"mb-e-lookup\"></em>");
            if (Store.Demo)
            {
                Booking? sample = site.Bookings.FirstOrDefault(x => x.Guest == "g000" && x.Status == "booked");
                sb.Append("<p class=\"mb-hint mb-demohint\">Demo: try code <b>" + Ui.Enc(sample?.Code ?? "SKH-") + "</b> with last name <b>Chen</b>, or book a stay first.</p>");
            }
            sb.Append("<div id=\"mb-mbres\"></div></div>");
            return new View { Title = "Manage a booking", Main = sb.ToString(), Desc = "View or cancel your SkyHotel booking." };
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

        private static readonly Dictionary<string, List<DateTime>> Tries = new Dictionary<string, List<DateTime>>();

        private static bool TooMany(string code)
        {
            lock (Tries)
            {
                if (Tries.Count > 5000)
                {
                    Tries.Clear();
                }
                if (!Tries.TryGetValue(code, out List<DateTime>? list))
                {
                    list = new List<DateTime>();
                    Tries[code] = list;
                }
                list.RemoveAll(x => x < DateTime.UtcNow.AddMinutes(-10));
                list.Add(DateTime.UtcNow);
                return list.Count > 12;
            }
        }

        private static Booking? Match(SiteData site, string code, string last)
        {
            Booking? b = site.Bookings.FirstOrDefault(x => x.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
            if (b == null)
            {
                return null;
            }
            Guest? g = site.Guests.FirstOrDefault(x => x.Id == b.Guest);
            return g != null && g.Last.Trim().Equals(last.Trim(), StringComparison.OrdinalIgnoreCase) ? b : null;
        }

        private static string Card(SiteData site, Booking b)
        {
            Guest? g = site.Guests.FirstOrDefault(x => x.Id == b.Guest);
            RoomType? t = Ui.TypeOf(site, b.Type);
            int n = Book.Nights(b.In, b.Out);
            StringBuilder sb = new StringBuilder("<article class=\"mb-box mb-bcard\"><div class=\"mb-bc-head\"><div><small>Booking</small><b class=\"mb-bcode\">" + Ui.Enc(b.Code) + "</b></div>" + Auth.StatusPill("mb", b) + "</div>");
            sb.Append("<div class=\"mb-bc-grid\">" + (t != null && t.Images.Count > 0 ? Ui.Pic(t.Images[0], t.Name, "mb-bc-img", true) : string.Empty) + "<dl class=\"mb-bc-dl\"><dt>Guest</dt><dd>" + Ui.Enc(Ui.GuestName(g)) + "</dd><dt>Room</dt><dd>" + Ui.Enc(Ui.TypeName(site, b.Type)) + (b.Room != string.Empty && b.Status == "inhouse" ? " &middot; room " + Ui.Enc(b.Room) : string.Empty) + "</dd><dt>Dates</dt><dd>" + Ui.Day(b.In) + " &rarr; " + Ui.Day(b.Out) + " &middot; " + n + (n == 1 ? " night" : " nights") + "</dd><dt>Guests</dt><dd>" + b.Adults + (b.Adults == 1 ? " adult" : " adults") + (b.Children > 0 ? ", " + b.Children + (b.Children == 1 ? " child" : " children") : string.Empty) + "</dd><dt>Rate</dt><dd>" + Book.PlanName(b.Plan) + (b.Promo != string.Empty ? " &middot; code " + Ui.Enc(b.Promo) : string.Empty) + "</dd><dt>Check-out</dt><dd>" + Ui.Day(b.Out) + " by " + Ui.Enc(Ui.OutTime(site, b)) + (b.LateOut != string.Empty ? " (late check-out)" : string.Empty) + "</dd><dt>Payment</dt><dd>" + (b.PayAtFront && b.Status != "out" ? Ui.Cash(b.Total) + " to pay at the hotel" : Ui.Cash(b.Paid > 0 ? b.Paid : b.Total) + (b.PayAtFront ? " paid at the hotel" : " on " + Ui.Enc(b.Card) + " &bull;&bull;&bull;&bull; " + Ui.Enc(b.Last4))) + (b.PointsUsed > 0 ? " &middot; " + Ui.Num(b.PointsUsed) + " points used" : string.Empty) + "</dd>" + (b.Requests != string.Empty ? "<dt>Requests</dt><dd>" + Ui.Enc(b.Requests) + "</dd>" : string.Empty) + "</dl></div>");
            if (b.Status == "booked")
            {
                decimal refund = Book.RefundFor(site, b);
                string policy = b.Plan == "nonref" ? "This is an Advance saver rate, so it can't be refunded." : Book.FreeCancel(site, b) ? "Free cancellation until " + Book.ArrivalTime(site, b).AddHours(-site.Settings.CancelHours).ToString("ddd d MMM, HH:mm", Ui.Inv) + ". Cancelling now refunds the full " + Ui.Cash(b.Paid) + "." : "The free cancellation period has passed. Cancelling now refunds " + Ui.Cash(refund) + " (the first night is charged).";
                sb.Append("<div class=\"mb-info-box\">" + Ui.Enc(policy) + "</div><div class=\"mb-bc-acts\"><a class=\"mb-btn mb-btn-ghost mb-btn-sm\" href=\"Contact\">Contact the hotel</a><button type=\"button\" class=\"mb-btn mb-btn-danger mb-btn-sm\" onclick=\"MyBookingJs.cancel('" + Ui.Js(Ui.Cash(refund)) + "')\">Cancel booking</button></div>");
            }
            else if (b.Status == "inhouse")
            {
                sb.Append("<div class=\"mb-ok-box\">Enjoy your stay" + (b.Room != string.Empty ? " in room " + Ui.Enc(b.Room) : string.Empty) + ". Check-out is until " + Ui.Enc(site.Settings.CheckOut) + " on " + Ui.Day(b.Out, false) + ".</div>");
            }
            else if (b.Status == "cancelled")
            {
                sb.Append("<div class=\"mb-warn-box\">Cancelled" + (b.Cancelled != null ? " on " + b.Cancelled.Value.ToString("d MMM yyyy", Ui.Inv) : string.Empty) + ". Refund: " + Ui.Cash(b.Refund) + (b.PointsUsed > 0 ? ", plus " + Ui.Num(b.PointsUsed) + " points returned" : string.Empty) + ".</div>");
            }
            else if (b.Status == "out")
            {
                sb.Append("<div class=\"mb-ok-box\">Thank you for staying with us." + (b.PointsEarned > 0 ? " You earned <b>" + Ui.Num(b.PointsEarned) + " points</b>." : string.Empty) + "</div>");
                Review? r = site.Reviews.FirstOrDefault(x => x.Booking == b.Code);
                if (r == null)
                {
                    sb.Append("<form class=\"mb-form mb-rvw\" onsubmit=\"return MyBookingJs.review()\" novalidate><h3>How was your stay?</h3><div class=\"mb-rate\" role=\"radiogroup\" aria-label=\"Rating\">");
                    for (int i = 5; i >= 1; i--)
                    {
                        sb.Append("<input type=\"radio\" name=\"mb-stars\" id=\"mb-star" + i + "\" value=\"" + i + "\"" + (i == 5 ? " checked" : string.Empty) + "><label for=\"mb-star" + i + "\" title=\"" + i + " stars\">&#9733;</label>");
                    }
                    sb.Append("</div><label class=\"mb-field\"><span>Title</span><input type=\"text\" id=\"mb-rtitle\" maxlength=\"80\"><em class=\"mb-err\" id=\"mb-e-rtitle\"></em></label><label class=\"mb-field\"><span>Your review</span><textarea id=\"mb-rtext\" rows=\"4\" maxlength=\"800\"></textarea><em class=\"mb-err\" id=\"mb-e-rtext\"></em></label><div class=\"mb-bc-acts\"><button type=\"submit\" class=\"mb-btn mb-btn-sm\">Send review</button></div></form>");
                }
                else
                {
                    sb.Append("<p class=\"mb-hint\">You reviewed this stay: " + Ui.Enc(r.Title) + " (" + r.Rating + "/5)" + (r.Status == "pending" ? ", waiting for our team to publish it." : ".") + "</p>");
                }
            }
            sb.Append("<details class=\"mb-bc-price\"><summary>Price details</summary><dl class=\"mb-bc-dl\"><dt>Room</dt><dd>" + Ui.Cash(b.RoomTotal) + "</dd>" + (b.MemberOff > 0 ? "<dt>Member discount</dt><dd>&minus;" + Ui.Cash(b.MemberOff) + "</dd>" : string.Empty) + (b.Discount > 0 ? "<dt>Code " + Ui.Enc(b.Promo) + "</dt><dd>&minus;" + Ui.Cash(b.Discount) + "</dd>" : string.Empty) + (b.Breakfast > 0 ? "<dt>Breakfast</dt><dd>" + Ui.Cash(b.Breakfast) + "</dd>" : string.Empty) + (b.PointsValue > 0 ? "<dt>Points</dt><dd>&minus;" + Ui.Cash(b.PointsValue) + "</dd>" : string.Empty) + "<dt>Tax</dt><dd>" + Ui.Cash(b.Tax) + "</dd><dt>Total</dt><dd><b>" + Ui.Cash(b.Total) + "</b></dd></dl></details></article>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Lookup()
        {
            ApiResponse response = new ApiResponse();
            string code = Ui.Clip(GetDataValue("bcode"), 12).ToUpperInvariant();
            string last = Ui.Clip(GetDataValue("lname"), 40);
            if (code == string.Empty || last == string.Empty)
            {
                response.SetElementContents("mb-e-lookup", "Enter both the booking code and the last name.");
                response.SetElementContents("mb-mbres", string.Empty);
                return response;
            }
            if (TooMany(code))
            {
                response.SetElementContents("mb-e-lookup", "Too many tries for this code. Please wait ten minutes.");
                return response;
            }
            SiteData site = await Store.LoadSite(Readable(GetDataValue("sbx")));
            Booking? b = Match(site, code, last);
            response.SetElementContents("mb-e-lookup", b == null ? "We couldn't find a booking with that code and last name." : string.Empty);
            response.SetElementContents("mb-mbres", b == null ? string.Empty : Card(site, b));
            if (b != null)
            {
                response.ExecuteScript("history.replaceState(null,'','MyBooking?c=" + Uri.EscapeDataString(b.Code) + "');");
            }
            return response;
        }

        public async Task<ApiResponse> Cancel()
        {
            ApiResponse response = new ApiResponse();
            string code = Ui.Clip(GetDataValue("bcode"), 12).ToUpperInvariant();
            string last = Ui.Clip(GetDataValue("lname"), 40);
            string sbx = await Writable(response, GetDataValue("sbx"));
            string msg = string.Empty;
            decimal refund = 0;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(sbx);
                Booking? b = Match(site, code, last);
                if (b == null || b.Status != "booked")
                {
                    msg = "This booking can't be cancelled online. Please call the hotel.";
                }
                else
                {
                    refund = Book.RefundFor(site, b);
                    b.Status = "cancelled";
                    b.Cancelled = DateTime.UtcNow;
                    b.Refund = refund;
                    b.Room = string.Empty;
                    Guest? g = site.Guests.FirstOrDefault(x => x.Id == b.Guest);
                    if (g != null && g.Member && b.PointsUsed > 0)
                    {
                        Book.AddPoints(site, g, b.PointsUsed, "return", b.Code, "Returned from cancelled " + b.Code, string.Empty);
                    }
                    await Store.SaveSite(site, sbx);
                }
            }
            if (msg != string.Empty)
            {
                response.SetElementContents("mb-e-lookup", msg);
                return response;
            }
            await Store.Log(sbx, "web", "cancel", code + " · refund " + Ui.Cash(refund), true);
            SiteData fresh = await Store.LoadSite(sbx);
            Booking? nb = Match(fresh, code, last);
            response.SetElementContents("mb-mbres", nb != null ? Card(fresh, nb) : string.Empty);
            response.ExecuteScript("MyBookingJs.toast('Booking cancelled. Refund " + Ui.Js(Ui.Cash(refund)) + " (demo).');");
            return response;
        }

        public async Task<ApiResponse> Review()
        {
            ApiResponse response = new ApiResponse();
            string code = Ui.Clip(GetDataValue("bcode"), 12).ToUpperInvariant();
            string last = Ui.Clip(GetDataValue("lname"), 40);
            string title = Regex.Replace(Ui.Clip(GetDataValue("rtitle"), 80), "\\s+", " ");
            string text = Ui.Clip(GetDataValue("rtext"), 800);
            int stars = int.TryParse(GetDataValue("stars"), out int s) ? Math.Clamp(s, 1, 5) : 5;
            string eT = title.Length < 3 ? "Give your review a short title." : string.Empty;
            string eX = text.Length < 20 ? "Tell us a little more (20 characters or more)." : string.Empty;
            response.SetElementContents("mb-e-rtitle", eT);
            response.SetElementContents("mb-e-rtext", eX);
            if (eT + eX != string.Empty)
            {
                return response;
            }
            string sbx = await Writable(response, GetDataValue("sbx"));
            bool ok = false;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(sbx);
                Booking? b = Match(site, code, last);
                if (b != null && b.Status == "out" && !site.Reviews.Any(r => r.Booking == b.Code) && site.Reviews.Count(r => r.Status == "pending") < 300)
                {
                    Guest? g = site.Guests.FirstOrDefault(x => x.Id == b.Guest);
                    site.Reviews.Insert(0, new Review { Id = Book.NewId(), Guest = b.Guest, Name = g != null ? g.First + " " + (g.Last.Length > 0 ? g.Last.Substring(0, 1) + "." : string.Empty) : "Guest", Booking = b.Code, Rating = stars, Title = title, Text = text, When = DateTime.UtcNow, Status = "pending" });
                    await Store.SaveSite(site, sbx);
                    ok = true;
                }
            }
            if (!ok)
            {
                response.SetElementContents("mb-e-rtext", "We couldn't save this review.");
                return response;
            }
            SiteData fresh = await Store.LoadSite(sbx);
            Booking? nb = Match(fresh, code, last);
            response.SetElementContents("mb-mbres", nb != null ? Card(fresh, nb) : string.Empty);
            response.ExecuteScript("MyBookingJs.toast('Thank you. Your review will appear once our team has read it.');");
            return response;
        }
    }
}
