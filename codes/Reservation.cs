using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyHotel.Core;
using SkyHotel.Models;
using SkyNet;

namespace SkyHotel.codes
{
    public class Reservation : WebPage
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
            View v = Render(site, Ui.Clip(QueryValue("ci"), 80), Ui.Clip(QueryValue("co"), 80));
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
                .Replace("{plhd_style}", SafeColor(site.Settings.Accent) ? "--rv-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_nav}", Nav("rv", "Reservation", false))
                .Replace("{plhd_drawer}", Nav("rv", "Reservation", true))
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
                response.ExecuteScript("ReservationJs.hydrated(false);");
                return response;
            }
            SiteData site = await Store.LoadSite(sbx);
            View v = Render(site, Ui.Clip(GetDataValue("argA"), 80), Ui.Clip(GetDataValue("argB"), 80));
            response.SetElementContents("rv-main", v.Main);
            response.SetElementContents("rv-brand", Ui.Enc(site.Settings.Name));
            response.SetElementContents("rv-town", Ui.Enc(site.Settings.Town));
            response.SetElementContents("rv-ftag", Ui.Enc(site.Settings.Tagline));
            response.SetElementContents("rv-faddr", Ui.Enc(site.Settings.Address) + "<br><a href=\"tel:" + Ui.Attr(Regex.Replace(site.Settings.Phone, "[^0-9+]", string.Empty)) + "\">" + Ui.Enc(site.Settings.Phone) + "</a> &middot; <a href=\"mailto:" + Ui.Attr(site.Settings.Email) + "\">" + Ui.Enc(site.Settings.Email) + "</a>");
            string accent = SafeColor(site.Settings.Accent) ? site.Settings.Accent : string.Empty;
            response.ExecuteScript("ReservationJs.hydrated(true,'" + accent + "','" + (site.Settings.Theme == "dark" ? "dark" : "light") + "');");
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
                response.ExecuteScript("ReservationJs.sandboxed('" + sbx + "');");
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
                response.ExecuteScript("ReservationJs.who('', '');");
                return response;
            }
            SiteData site = await Store.LoadSite(m.Sbx);
            Guest? g = site.Guests.FirstOrDefault(x => x.Id == m.Guest && x.Member);
            if (g == null)
            {
                response.ExecuteScript("ReservationJs.who('', '');");
                return response;
            }
            response.ExecuteScript("ReservationJs.who('" + Ui.Js(Ui.Enc(g.First)) + "','" + Ui.GradeOf(g.Lifetime).Key + "');");
            return response;
        }

        private static string Stars(int n)
        {
            int k = Math.Clamp(n, 0, 5);
            return "<span class=\"rv-stars\" aria-label=\"" + k + " out of 5\">" + new string('★', k) + "<span style=\"opacity:0.3\">" + new string('★', 5 - k) + "</span></span>";
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
            return "<article class=\"rv-card\"><a class=\"rv-card-img\" href=\"Room?t=" + Uri.EscapeDataString(t.Key) + "\">" + Ui.Pic(img, t.Name, string.Empty, true, !lazy) + "</a><div class=\"rv-card-body\"><h3><a href=\"Room?t=" + Uri.EscapeDataString(t.Key) + "\">" + Ui.Enc(t.Name) + "</a></h3><div class=\"rv-facts\"><span>" + t.Size + " m&sup2;</span><span>" + Ui.Enc(t.Beds) + "</span><span>" + Ui.Enc(t.View) + " view</span><span>Up to " + t.MaxGuests + " guests</span></div><p>" + Ui.Enc(t.Short) + "</p><div class=\"rv-from\"><div><span>From</span> <b>" + Ui.Money(FromPrice(site, t)) + "</b> <span>/ night</span></div><a class=\"rv-btn rv-btn-sm rv-btn-ghost\" href=\"Reservation?t=" + Uri.EscapeDataString(t.Key) + "\">Book</a></div></div></article>";
        }

        private static string Hero(ContentBlock c, string fallbackTitle)
        {
            return "<section class=\"rv-phero\">" + (c.Image != string.Empty ? Ui.Pic(c.Image, string.Empty, string.Empty, false, true) : string.Empty) + "<div class=\"rv-wrap\"><h1>" + Ui.Enc(c.Title != string.Empty ? c.Title : fallbackTitle) + "</h1>" + (c.Text != string.Empty ? "<p>" + Ui.Enc(c.Text) + "</p>" : string.Empty) + "</div></section>";
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
            DateTime today = DateTime.UtcNow.Date;
            DateTime din = Ui.ParseDay(a) ?? today.AddDays(1);
            DateTime dout = Ui.ParseDay(b) ?? din.AddDays(2);
            if (din < today)
            {
                din = today.AddDays(1);
            }
            if (dout <= din)
            {
                dout = din.AddDays(2);
            }
            StringBuilder sb = new StringBuilder("<div class=\"rv-wrap\"><div class=\"rv-phead\"><span class=\"rv-kick\">Reservation</span><h1>Book your stay</h1></div>");
            sb.Append("<ol class=\"rv-steps\" id=\"rv-steps\"><li class=\"rv-act\" data-s=\"1\"><b>1</b>Dates &amp; room</li><li data-s=\"2\"><b>2</b>Your details</li><li data-s=\"3\"><b>3</b>Payment</li><li data-s=\"4\"><b>4</b>Confirmed</li></ol>");
            sb.Append("<div class=\"rv-rvgrid\"><div class=\"rv-flow\">");
            sb.Append("<section class=\"rv-box rv-msign\" id=\"rv-msign\" hidden><h2>Member sign in</h2><form class=\"rv-msform\" onsubmit=\"return ReservationJs.memberIn()\" novalidate><label class=\"rv-field\"><span>Email</span><input type=\"email\" id=\"rv-memail\" maxlength=\"100\" autocomplete=\"username\" value=\"" + (Store.Demo ? "mia@example.com" : string.Empty) + "\"></label><label class=\"rv-field\"><span>Password</span><input type=\"password\" id=\"rv-mpass\" maxlength=\"100\" autocomplete=\"current-password\" value=\"" + (Store.Demo ? "demo" : string.Empty) + "\"></label><button type=\"submit\" class=\"rv-btn\">Sign in</button></form><em class=\"rv-err\" id=\"rv-e-msign\"></em>" + (Store.Demo ? "<p class=\"rv-hint\" style=\"margin-top:8px\">Demo member: mia@example.com / demo (Gold)</p>" : string.Empty) + "</section>");
            sb.Append("<section class=\"rv-box rv-stp\" id=\"rv-s1\"><h2>When are you staying?</h2><form class=\"rv-rvform\" onsubmit=\"return ReservationJs.find()\" novalidate>");
            sb.Append("<label class=\"rv-field\"><span>Check-in</span><input type=\"date\" id=\"rv-din\" min=\"" + Ui.Ymd(today) + "\" value=\"" + Ui.Ymd(din) + "\"></label>");
            sb.Append("<label class=\"rv-field\"><span>Check-out</span><input type=\"date\" id=\"rv-dout\" min=\"" + Ui.Ymd(today.AddDays(1)) + "\" value=\"" + Ui.Ymd(dout) + "\"></label>");
            sb.Append("<label class=\"rv-field\"><span>Adults</span><select id=\"rv-nad\">" + Ui.Opts(new[] { ("1", "1"), ("2", "2"), ("3", "3"), ("4", "4") }, "2") + "</select></label>");
            sb.Append("<label class=\"rv-field\"><span>Children</span><select id=\"rv-nch\">" + Ui.Opts(new[] { ("0", "0"), ("1", "1"), ("2", "2"), ("3", "3") }, "0") + "</select></label>");
            sb.Append("<label class=\"rv-field\"><span>Promo code</span><input type=\"text\" id=\"rv-pcode\" maxlength=\"20\" autocomplete=\"off\" placeholder=\"Optional\"></label>");
            sb.Append("<button type=\"submit\" class=\"rv-btn rv-btn-gold\">Search</button></form><em class=\"rv-err\" id=\"rv-e-dates\"></em><div class=\"rv-mnote\" id=\"rv-mnote\"></div><div id=\"rv-results\"></div></section>");
            sb.Append("<section class=\"rv-box rv-stp\" id=\"rv-s2\" hidden></section><section class=\"rv-box rv-stp\" id=\"rv-s3\" hidden></section><section class=\"rv-box rv-stp\" id=\"rv-s4\" hidden></section>");
            sb.Append("<input type=\"hidden\" id=\"rv-rtype\"><input type=\"hidden\" id=\"rv-rplan\"><input type=\"hidden\" id=\"rv-pts\" value=\"0\"></div><aside class=\"rv-sum\" id=\"rv-sum\"><div class=\"rv-box rv-sumempty\"><h2>Your stay</h2><p class=\"rv-hint\">Choose your dates and a room to see the price.</p><ul class=\"rv-why\"><li>Best rate when you book here</li><li>Free cancellation on flexible rates</li><li>Members save up to 15% and earn points</li></ul></div></aside></div></div>");
            return new View { Title = "Book your stay", Main = sb.ToString(), Desc = "Book a room at SkyHotel." };
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

        private sealed class Ask
        {
            public DateTime In { get; set; }
            public DateTime Out { get; set; }
            public int Adults { get; set; }
            public int Children { get; set; }
            public string Promo { get; set; } = string.Empty;
            public string Error { get; set; } = string.Empty;
        }

        private Ask ReadAsk()
        {
            Ask k = new Ask();
            DateTime? din = Ui.ParseDay(GetDataValue("din"));
            DateTime? dout = Ui.ParseDay(GetDataValue("dout"));
            k.Error = Book.CheckDates(din, dout);
            k.In = din ?? DateTime.UtcNow.Date;
            k.Out = dout ?? k.In.AddDays(1);
            k.Adults = int.TryParse(GetDataValue("nad"), out int a) ? Math.Clamp(a, 1, 4) : 2;
            k.Children = int.TryParse(GetDataValue("nch"), out int c) ? Math.Clamp(c, 0, 3) : 0;
            k.Promo = Ui.Clip(GetDataValue("pcode"), 20).ToUpperInvariant();
            return k;
        }

        public async Task<ApiResponse> Find()
        {
            ApiResponse response = new ApiResponse();
            Ask k = ReadAsk();
            response.SetElementContents("rv-e-dates", k.Error);
            if (k.Error != string.Empty)
            {
                response.SetElementContents("rv-results", string.Empty);
                return response;
            }
            (SiteData site, string sbx, Guest? member) = await Context(GetDataValue("sbx"), GetDataValue("mtok"));
            string want = Ui.Clip(GetDataValue("want"), 30);
            int guests = k.Adults + k.Children;
            int nights = Book.Nights(k.In, k.Out);
            StringBuilder sb = new StringBuilder("<p class=\"rv-rhead\"><b>" + nights + (nights == 1 ? " night" : " nights") + "</b> &middot; " + Ui.Day(k.In, false) + " &rarr; " + Ui.Day(k.Out, false) + " &middot; " + guests + (guests == 1 ? " guest" : " guests") + "</p>");
            string promoNote = string.Empty;
            int shown = 0;
            foreach (RoomType t in site.Types.Where(x => x.Active).OrderByDescending(x => x.Key == want).ThenBy(x => x.Order))
            {
                if (t.MaxGuests < guests)
                {
                    continue;
                }
                shown++;
                int left = Book.Available(site, t.Key, k.In, k.Out);
                sb.Append("<article class=\"rv-res" + (left <= 0 ? " rv-sold" : string.Empty) + "\"><div class=\"rv-res-img\">" + (t.Images.Count > 0 ? Ui.Pic(t.Images[0], t.Name, string.Empty, true) : string.Empty) + (left > 0 && left <= 3 ? "<span class=\"rv-tag\">Only " + left + " left</span>" : string.Empty) + "</div><div class=\"rv-res-body\"><h3>" + Ui.Enc(t.Name) + "</h3><div class=\"rv-facts\"><span>" + t.Size + " m&sup2;</span><span>" + Ui.Enc(t.Beds) + "</span><span>" + Ui.Enc(t.View) + " view</span></div>");
                if (left <= 0)
                {
                    sb.Append("<p class=\"rv-soldtxt\">Sold out for these dates. Try other dates or another room.</p></div></article>");
                    continue;
                }
                Offer? po = Book.FindOffer(site, k.Promo);
                bool misfit = po != null && !Book.Fits(po, t.Key);
                sb.Append((misfit ? "<p class=\"rv-hint rv-pmiss\">Code " + Ui.Enc(po!.Code) + " isn't valid for this room. It's for " + Ui.Enc(Book.TypesText(site, po)) + ".</p>" : string.Empty) + "<div class=\"rv-plans\">");
                foreach ((string pk, string pn, string ph) in Book.Plans)
                {
                    Quote q = Book.Price(site, t.Key, k.In, k.Out, k.Adults, k.Children, pk, k.Promo, member, 0);
                    if (q.PromoNote != string.Empty && !misfit)
                    {
                        promoNote = q.PromoNote;
                    }
                    decimal before = q.Total + q.MemberOff + q.PromoOff;
                    sb.Append("<div class=\"rv-plan\"><div><b>" + pn + "</b><small>" + Ui.Enc(pk == "flex" ? "Free cancellation until " + site.Settings.CancelHours + " hours before arrival" : pk == "breakfast" ? (q.FreeBreakfast ? "Breakfast free for your grade" : "Breakfast for everyone, flexible cancellation") : ph) + "</small></div><div class=\"rv-pprice\">" + (q.MemberOff + q.PromoOff > 0 ? "<s>" + Ui.Money(before) + "</s>" : string.Empty) + "<b>" + Ui.Money(q.Total) + "</b><small>incl. tax &middot; " + Ui.Money(q.Total / Math.Max(1, q.Nights)) + " / night</small></div><button type=\"button\" class=\"rv-btn rv-btn-sm\" onclick=\"ReservationJs.pick('" + Ui.Attr(t.Key) + "','" + pk + "')\">Select</button></div>");
                }
                sb.Append("</div></div></article>");
            }
            if (shown == 0)
            {
                sb.Append("<p class=\"rv-none\">None of our rooms sleeps " + guests + " guests. Try two rooms or call us on " + Ui.Enc(site.Settings.Phone) + ".</p>");
            }
            if (k.Promo != string.Empty)
            {
                Offer? o = Book.FindOffer(site, k.Promo);
                sb.Insert(0, promoNote != string.Empty ? "<p class=\"rv-warn-box\">" + Ui.Enc(promoNote) + "</p>" : "<p class=\"rv-ok-box\">Code <b>" + Ui.Enc(k.Promo) + "</b> applied" + (o != null && o.Pct > 0 ? ": " + o.Pct + "% off the room" : string.Empty) + (o != null && o.Bonus > 1 ? ", " + o.Bonus + "&times; points" : string.Empty) + (o != null && o.Types.Count > 0 ? ". Valid on " + Ui.Enc(Book.TypesText(site, o)) + "." : ".") + "</p>");
            }
            response.SetElementContents("rv-results", sb.ToString());
            response.SetElementContents("rv-mnote", MemberNote(member));
            response.ExecuteScript("ReservationJs.found();");
            return response;
        }

        private static string MemberNote(Guest? member)
        {
            if (member != null)
            {
                (string key, string name, _, int pct, _) = Ui.GradeOf(member.Lifetime);
                return "<p class=\"rv-ok-box\">Signed in as <b>" + Ui.Enc(member.First) + "</b> <span class=\"rv-grade rv-g-" + key + "\">" + name + "</span> Member prices include your " + pct + "% discount.</p>";
            }
            return "<p class=\"rv-info-box\">SkyHotel Circle members save 5&ndash;15% and earn a point for every dollar. <a href=\"#\" onclick=\"ReservationJs.showSignIn();return false;\"><b>Sign in</b></a> or <a href=\"Membership\"><b>join free</b></a>.</p>";
        }

        private static string Summary(SiteData site, Quote q, DateTime din, DateTime dout, int adults, int children, string plan, Guest? member, bool final = false)
        {
            RoomType t = q.Type!;
            int guests = adults + children;
            StringBuilder sb = new StringBuilder("<div class=\"rv-box rv-sumbox\"><h2>Your stay</h2>" + (t.Images.Count > 0 ? Ui.Pic(t.Images[0], t.Name, "rv-sumimg", true) : string.Empty) + "<h3>" + Ui.Enc(t.Name) + "</h3><p class=\"rv-hint\">" + Ui.Day(din) + " &rarr; " + Ui.Day(dout) + "<br>" + q.Nights + (q.Nights == 1 ? " night" : " nights") + " &middot; " + adults + (adults == 1 ? " adult" : " adults") + (children > 0 ? ", " + children + (children == 1 ? " child" : " children") : string.Empty) + " &middot; " + Book.PlanName(plan) + "</p><dl class=\"rv-lines\">");
            sb.Append("<dt>Room, " + q.Nights + (q.Nights == 1 ? " night" : " nights") + (plan == "nonref" ? " (10% saver)" : string.Empty) + "</dt><dd>" + Ui.Money(q.RoomSub, true) + "</dd>");
            if (q.MemberOff > 0)
            {
                sb.Append("<dt>Member discount " + q.MemberPct + "%</dt><dd class=\"rv-minus\">&minus;" + Ui.Money(q.MemberOff, true).Replace("$", "$") + "</dd>");
            }
            if (q.PromoOff > 0)
            {
                sb.Append("<dt>Code " + Ui.Enc(q.Offer!.Code) + "</dt><dd class=\"rv-minus\">&minus;" + Ui.Money(q.PromoOff, true) + "</dd>");
            }
            if (plan == "breakfast")
            {
                sb.Append("<dt>Breakfast, " + guests + " &times; " + q.Nights + "</dt><dd>" + (q.FreeBreakfast ? "Free" : Ui.Money(q.Breakfast, true)) + "</dd>");
            }
            if (q.PointsValue > 0)
            {
                sb.Append("<dt>" + Ui.Num(q.PointsUsed) + " points</dt><dd class=\"rv-minus\">&minus;" + Ui.Money(q.PointsValue, true) + "</dd>");
            }
            sb.Append("<dt>Tax " + site.Settings.Tax.ToString("0.##", Ui.Inv) + "%</dt><dd>" + Ui.Money(q.Tax, true) + "</dd><dt class=\"rv-tot\">Total</dt><dd class=\"rv-tot\">" + Ui.Money(q.Total, true) + "</dd></dl>");
            sb.Append("<details class=\"rv-nightly\"><summary>Nightly rates</summary><ul>");
            foreach ((DateTime d, decimal r) in q.Nightly)
            {
                sb.Append("<li><span>" + Ui.Day(d, false) + "</span><span>" + Ui.Money(r) + "</span></li>");
            }
            sb.Append("</ul></details>");
            if (member != null)
            {
                sb.Append("<p class=\"rv-earn\">You'll earn <b>" + Ui.Num(q.Earn) + " points</b> when you check out" + (q.Offer != null && q.Offer.Bonus > 1 ? " (" + q.Offer.Bonus + "&times; offer)" : string.Empty) + ".</p>");
            }
            else if (!final)
            {
                sb.Append("<p class=\"rv-earn\">Circle members would earn <b>" + Ui.Num((int)Math.Floor(q.Total - q.Tax)) + " points</b> on this stay.</p>");
            }
            sb.Append("<p class=\"rv-hint\">" + (plan == "nonref" ? "Advance saver: paid now, no refund if you cancel." : "Free cancellation until " + Book.ArrivalTime(site, new Booking { In = din }).AddHours(-site.Settings.CancelHours).ToString("ddd d MMM, HH:mm", Ui.Inv) + ".") + "</p></div>");
            return sb.ToString();
        }

        private static string PlanOf(string v)
        {
            return Book.Plans.Any(p => p.Key == v) ? v : "flex";
        }

        public async Task<ApiResponse> Pick()
        {
            ApiResponse response = new ApiResponse();
            Ask k = ReadAsk();
            if (k.Error != string.Empty)
            {
                response.SetElementContents("rv-e-dates", k.Error);
                response.ExecuteScript("ReservationJs.step(1);");
                return response;
            }
            (SiteData site, string sbx, Guest? member) = await Context(GetDataValue("sbx"), GetDataValue("mtok"));
            string type = Ui.Clip(GetDataValue("rtype"), 30);
            string plan = PlanOf(GetDataValue("rplan"));
            Quote q = Book.Price(site, type, k.In, k.Out, k.Adults, k.Children, plan, k.Promo, member, 0);
            if (q.Error != string.Empty || Book.Available(site, type, k.In, k.Out) <= 0)
            {
                response.SetElementContents("rv-e-dates", q.Error != string.Empty ? q.Error : "That room has just sold out. Please choose another.");
                response.ExecuteScript("ReservationJs.step(1);ReservationJs.find();");
                return response;
            }
            response.SetElementContents("rv-sum", Summary(site, q, k.In, k.Out, k.Adults, k.Children, plan, member));
            response.SetElementContents("rv-s2", DetailsForm(site, member));
            response.ExecuteScript("ReservationJs.step(2);");
            return response;
        }

        private static string DetailsForm(SiteData site, Guest? m)
        {
            StringBuilder sb = new StringBuilder("<h2>Your details</h2>" + MemberNote(m) + "<form class=\"rv-form\" onsubmit=\"return ReservationJs.details()\" novalidate><div class=\"rv-row2\">");
            sb.Append("<label class=\"rv-field\"><span>First name</span><input type=\"text\" id=\"rv-gfirst\" maxlength=\"40\" autocomplete=\"given-name\" value=\"" + Ui.Attr(m?.First) + "\"><em class=\"rv-err\" id=\"rv-e-gfirst\"></em></label>");
            sb.Append("<label class=\"rv-field\"><span>Last name</span><input type=\"text\" id=\"rv-glast\" maxlength=\"40\" autocomplete=\"family-name\" value=\"" + Ui.Attr(m?.Last) + "\"><em class=\"rv-err\" id=\"rv-e-glast\"></em></label></div><div class=\"rv-row2\">");
            sb.Append("<label class=\"rv-field\"><span>Email</span><input type=\"email\" id=\"rv-gmail\" maxlength=\"100\" autocomplete=\"email\" value=\"" + Ui.Attr(m?.Email) + "\"" + (m != null ? " readonly" : string.Empty) + "><em class=\"rv-err\" id=\"rv-e-gmail\"></em></label>");
            sb.Append("<label class=\"rv-field\"><span>Phone</span><input type=\"tel\" id=\"rv-gphone\" maxlength=\"30\" autocomplete=\"tel\" value=\"" + Ui.Attr(m?.Phone) + "\"><em class=\"rv-err\" id=\"rv-e-gphone\"></em></label></div><div class=\"rv-row2\">");
            sb.Append("<label class=\"rv-field\"><span>Country</span><input type=\"text\" id=\"rv-gcountry\" maxlength=\"40\" autocomplete=\"country-name\" list=\"rv-countries\" value=\"" + Ui.Attr(m?.Country) + "\"></label>" + Ui.DataList("rv-countries", site.Guests.Select(g => g.Country)));
            sb.Append("<label class=\"rv-field\"><span>Arrival time</span><select id=\"rv-garr\">" + Ui.Opts(new[] { ("", "I don't know yet"), ("12:00", "12:00 - 15:00"), ("15:00", "15:00 - 18:00"), ("18:00", "18:00 - 21:00"), ("21:00", "After 21:00") }, string.Empty) + "</select></label></div>");
            sb.Append("<label class=\"rv-field\"><span>Requests (optional)</span><textarea id=\"rv-greq\" rows=\"3\" maxlength=\"300\" placeholder=\"A quiet room, a cot, an anniversary...\"></textarea></label>");
            sb.Append("<div class=\"rv-acts\"><button type=\"button\" class=\"rv-btn rv-btn-ghost\" onclick=\"ReservationJs.step(1)\">&larr; Back</button><button type=\"submit\" class=\"rv-btn rv-btn-gold\">Continue to payment</button></div></form>");
            return sb.ToString();
        }

        private sealed class GuestIn
        {
            public string First { get; set; } = string.Empty;
            public string Last { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string Country { get; set; } = string.Empty;
            public string Arrival { get; set; } = string.Empty;
            public string Requests { get; set; } = string.Empty;
        }

        private GuestIn ReadGuest(ApiResponse response, out bool ok)
        {
            GuestIn g = new GuestIn
            {
                First = Regex.Replace(Ui.Clip(GetDataValue("gfirst"), 40), "\\s+", " "),
                Last = Regex.Replace(Ui.Clip(GetDataValue("glast"), 40), "\\s+", " "),
                Email = Ui.Clip(GetDataValue("gmail"), 100).ToLowerInvariant(),
                Phone = Ui.Clip(GetDataValue("gphone"), 30),
                Country = Ui.Clip(GetDataValue("gcountry"), 40),
                Arrival = Regex.IsMatch(GetDataValue("garr"), "^[0-9]{2}:00$") ? GetDataValue("garr") : string.Empty,
                Requests = Ui.Clip(GetDataValue("greq"), 300)
            };
            string eF = g.First.Length < 1 ? "Enter your first name." : string.Empty;
            string eL = g.Last.Length < 1 ? "Enter your last name." : string.Empty;
            string eM = !IsEmail(g.Email) ? "Enter a valid email address." : string.Empty;
            string eP = !Regex.IsMatch(g.Phone, "^[+0-9 ()-]{6,30}$") ? "Enter a phone number we can reach you on." : string.Empty;
            response.SetElementContents("rv-e-gfirst", eF);
            response.SetElementContents("rv-e-glast", eL);
            response.SetElementContents("rv-e-gmail", eM);
            response.SetElementContents("rv-e-gphone", eP);
            ok = eF + eL + eM + eP == string.Empty;
            return g;
        }

        public async Task<ApiResponse> Details()
        {
            ApiResponse response = new ApiResponse();
            Ask k = ReadAsk();
            if (k.Error != string.Empty)
            {
                response.SetElementContents("rv-e-dates", k.Error);
                response.ExecuteScript("ReservationJs.step(1);");
                return response;
            }
            GuestIn gi = ReadGuest(response, out bool ok);
            if (!ok)
            {
                return response;
            }
            (SiteData site, string sbx, Guest? member) = await Context(GetDataValue("sbx"), GetDataValue("mtok"));
            string type = Ui.Clip(GetDataValue("rtype"), 30);
            string plan = PlanOf(GetDataValue("rplan"));
            Quote q = Book.Price(site, type, k.In, k.Out, k.Adults, k.Children, plan, k.Promo, member, 0);
            if (q.Error != string.Empty)
            {
                response.SetElementContents("rv-e-dates", q.Error);
                response.ExecuteScript("ReservationJs.step(1);");
                return response;
            }
            response.SetElementContents("rv-s3", PayForm(q, gi, member));
            response.SetElementContents("rv-sum", Summary(site, q, k.In, k.Out, k.Adults, k.Children, plan, member));
            response.ExecuteScript("ReservationJs.step(3);");
            return response;
        }

        private static string PayForm(Quote q, GuestIn gi, Guest? member)
        {
            int yy = DateTime.UtcNow.Year % 100 + 2;
            StringBuilder sb = new StringBuilder("<h2>Payment</h2><div class=\"rv-warn-box rv-demo\"><b>Demo &ndash; no real payment is made.</b> Test card <code>4242 4242 4242 4242</code> is accepted; <code>4000 0000 0000 0002</code> is always declined. Please don't enter a real card.</div>");
            if (member != null)
            {
                sb.Append("<div class=\"rv-ptsbox\"><div><b>Use your points</b><small>You have " + Ui.Num(member.Balance) + " points. 1,000 points = $10, up to half the room price.</small></div><select id=\"rv-ptsel\" onchange=\"ReservationJs.points()\" aria-label=\"Points to use\">");
                for (int p = 0; p <= q.PointsMax; p += 1000)
                {
                    sb.Append("<option value=\"" + p + "\">" + (p == 0 ? "Don't use points" : Ui.Num(p) + " points (&minus;" + Ui.Money(p / 100m) + ")") + "</option>");
                }
                sb.Append("</select></div>");
            }
            sb.Append("<form class=\"rv-form rv-card-form\" onsubmit=\"return ReservationJs.pay()\" novalidate autocomplete=\"off\">");
            sb.Append("<label class=\"rv-field\"><span>Name on card</span><input type=\"text\" id=\"rv-cname\" maxlength=\"60\" value=\"" + Ui.Attr((gi.First + " " + gi.Last).Trim()) + "\"><em class=\"rv-err\" id=\"rv-e-cname\"></em></label>");
            sb.Append("<label class=\"rv-field\"><span>Card number</span><input type=\"text\" id=\"rv-cnum\" inputmode=\"numeric\" maxlength=\"23\" value=\"4242 4242 4242 4242\" oninput=\"ReservationJs.fmtCard(this)\"><em class=\"rv-err\" id=\"rv-e-cnum\"></em></label>");
            sb.Append("<div class=\"rv-row2\"><label class=\"rv-field\"><span>Expiry (MM/YY)</span><input type=\"text\" id=\"rv-cexp\" inputmode=\"numeric\" maxlength=\"5\" value=\"12/" + yy.ToString("00", Ui.Inv) + "\"><em class=\"rv-err\" id=\"rv-e-cexp\"></em></label><label class=\"rv-field\"><span>Security code</span><input type=\"text\" id=\"rv-ccvc\" inputmode=\"numeric\" maxlength=\"4\" value=\"123\"><em class=\"rv-err\" id=\"rv-e-ccvc\"></em></label></div>");
            sb.Append("<label class=\"rv-check\"><input type=\"checkbox\" id=\"rv-cagree\"> I agree to the rate rules and the hotel's cancellation policy.</label><em class=\"rv-err\" id=\"rv-e-cagree\"></em><em class=\"rv-err\" id=\"rv-e-pay\"></em>");
            sb.Append("<div class=\"rv-acts\"><button type=\"button\" class=\"rv-btn rv-btn-ghost\" onclick=\"ReservationJs.step(2)\">&larr; Back</button><button type=\"submit\" class=\"rv-btn rv-btn-gold\" id=\"rv-paybtn\">Pay " + Ui.Money(q.Total, true) + "</button></div></form>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Points()
        {
            ApiResponse response = new ApiResponse();
            Ask k = ReadAsk();
            if (k.Error != string.Empty)
            {
                return response;
            }
            (SiteData site, string sbx, Guest? member) = await Context(GetDataValue("sbx"), GetDataValue("mtok"));
            string plan = PlanOf(GetDataValue("rplan"));
            int pts = int.TryParse(GetDataValue("pts"), out int p) ? p : 0;
            Quote q = Book.Price(site, Ui.Clip(GetDataValue("rtype"), 30), k.In, k.Out, k.Adults, k.Children, plan, k.Promo, member, pts);
            if (q.Error != string.Empty)
            {
                return response;
            }
            response.SetElementContents("rv-sum", Summary(site, q, k.In, k.Out, k.Adults, k.Children, plan, member));
            response.ExecuteScript("ReservationJs.payLabel('" + Ui.Js(Ui.Money(q.Total, true)) + "');");
            return response;
        }

        public async Task<ApiResponse> Pay()
        {
            ApiResponse response = new ApiResponse();
            Ask k = ReadAsk();
            if (k.Error != string.Empty)
            {
                response.SetElementContents("rv-e-dates", k.Error);
                response.ExecuteScript("ReservationJs.step(1);ReservationJs.payDone();");
                return response;
            }
            GuestIn gi = ReadGuest(response, out bool gok);
            if (!gok)
            {
                response.ExecuteScript("ReservationJs.step(2);ReservationJs.payDone();");
                return response;
            }
            string cname = Ui.Clip(GetDataValue("cname"), 60);
            string num = Regex.Replace(GetDataValue("cnum"), "[\\s-]", string.Empty);
            string exp = Ui.Clip(GetDataValue("cexp"), 5);
            string cvc = Ui.Clip(GetDataValue("ccvc"), 4);
            string eName = cname.Length < 2 ? "Enter the name on the card." : string.Empty;
            string eNum = !Regex.IsMatch(num, "^[0-9]{13,19}$") || !Access.Luhn(num) ? "That card number isn't valid. Use the test card 4242 4242 4242 4242." : string.Empty;
            string eExp = string.Empty;
            Match em = Regex.Match(exp, "^(0[1-9]|1[0-2])/([0-9]{2})$");
            if (!em.Success)
            {
                eExp = "Use MM/YY.";
            }
            else
            {
                DateTime last = new DateTime(2000 + int.Parse(em.Groups[2].Value, Ui.Inv), int.Parse(em.Groups[1].Value, Ui.Inv), 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
                if (last <= DateTime.UtcNow)
                {
                    eExp = "This card has expired.";
                }
            }
            string brand = num.Length > 0 ? Access.Brand(num) : "Card";
            string eCvc = !Regex.IsMatch(cvc, brand == "Amex" ? "^[0-9]{4}$" : "^[0-9]{3}$") ? (brand == "Amex" ? "Enter the 4-digit code." : "Enter the 3-digit code.") : string.Empty;
            string eAgree = GetDataValue("cagree") != "1" ? "Please accept the rate rules to continue." : string.Empty;
            response.SetElementContents("rv-e-cname", eName);
            response.SetElementContents("rv-e-cnum", eNum);
            response.SetElementContents("rv-e-cexp", eExp);
            response.SetElementContents("rv-e-ccvc", eCvc);
            response.SetElementContents("rv-e-cagree", eAgree);
            response.SetElementContents("rv-e-pay", string.Empty);
            if (eName + eNum + eExp + eCvc + eAgree != string.Empty)
            {
                response.ExecuteScript("ReservationJs.payDone();");
                return response;
            }
            if (num == "4000000000000002")
            {
                response.SetElementContents("rv-e-pay", "Your card was declined. (This is the demo decline card. Try 4242 4242 4242 4242.)");
                response.ExecuteScript("ReservationJs.payDone();");
                return response;
            }
            string sbx = await Writable(response, GetDataValue("sbx"));
            MCtx? mc = await Auth.Member(GetDataValue("mtok"));
            string type = Ui.Clip(GetDataValue("rtype"), 30);
            string plan = PlanOf(GetDataValue("rplan"));
            int pts = int.TryParse(GetDataValue("pts"), out int p) ? p : 0;
            Booking? bk = null;
            Quote? q = null;
            string fail = string.Empty;
            Guest? member = null;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(sbx);
                member = mc != null && mc.Sbx == sbx ? site.Guests.FirstOrDefault(x => x.Id == mc.Guest && x.Member) : null;
                q = Book.Price(site, type, k.In, k.Out, k.Adults, k.Children, plan, k.Promo, member, pts);
                if (q.Error != string.Empty)
                {
                    fail = q.Error;
                }
                else if (Book.Available(site, type, k.In, k.Out) <= 0)
                {
                    fail = "Sorry, that room sold out a moment ago. Your card was not charged.";
                }
                else if (site.Bookings.Count(x => x.Created > DateTime.UtcNow.AddMinutes(-10) && x.Source == "web") > 60)
                {
                    fail = "We're taking a lot of bookings right now. Please try again in a few minutes.";
                }
                else
                {
                    Guest? g = member ?? site.Guests.FirstOrDefault(x => x.Email.Equals(gi.Email, StringComparison.OrdinalIgnoreCase));
                    if (g == null)
                    {
                        g = new Guest { Id = "g" + Book.NewId(), Email = gi.Email, Created = DateTime.UtcNow };
                        site.Guests.Add(g);
                    }
                    if (member == null || g.Id == member.Id)
                    {
                        g.First = gi.First;
                        g.Last = gi.Last;
                        g.Phone = gi.Phone;
                        if (gi.Country != string.Empty)
                        {
                            g.Country = gi.Country;
                        }
                    }
                    bk = new Booking
                    {
                        Id = Book.NewId(), Code = Book.NewCode(site), Guest = g.Id, Type = type, In = k.In, Out = k.Out, Adults = k.Adults, Children = k.Children, Plan = plan,
                        RoomTotal = q.RoomSub, MemberOff = q.MemberOff, Discount = q.PromoOff, Promo = q.Offer?.Code ?? string.Empty, Bonus = q.Offer?.Bonus ?? 1, Breakfast = q.Breakfast,
                        PointsUsed = q.PointsUsed, PointsValue = q.PointsValue, Tax = q.Tax, Total = q.Total, Paid = q.Total, Card = Access.Brand(num), Last4 = num.Substring(num.Length - 4),
                        Status = "booked", Source = "web", Requests = gi.Requests, Arrival = gi.Arrival, Created = DateTime.UtcNow
                    };
                    site.Bookings.Add(bk);
                    if (q.Offer != null)
                    {
                        q.Offer.Used++;
                    }
                    if (member != null && q.PointsUsed > 0)
                    {
                        Book.AddPoints(site, member, -q.PointsUsed, "redeem", bk.Code, "Used on " + bk.Code, string.Empty);
                    }
                    await Store.SaveSite(site, sbx);
                }
            }
            if (fail != string.Empty || bk == null || q == null)
            {
                response.SetElementContents("rv-e-pay", fail);
                response.ExecuteScript("ReservationJs.payDone();");
                return response;
            }
            await Store.Log(sbx, "web", "booking", bk.Code + " · " + type + " · " + Ui.Cash(bk.Total), true);
            SiteData fresh = await Store.LoadSite(sbx);
            Guest? mem = member != null ? fresh.Guests.FirstOrDefault(x => x.Id == member.Id) : null;
            StringBuilder sb = new StringBuilder("<div class=\"rv-done\"><span class=\"rv-tick\">&#10003;</span><h2>You're booked</h2><p>Thank you, " + Ui.Enc(gi.First) + ". We look forward to welcoming you on " + Ui.Day(k.In) + ".</p><div class=\"rv-codebox\"><small>Booking code</small><b id=\"rv-bkcode\">" + Ui.Enc(bk.Code) + "</b></div>");
            sb.Append("<p class=\"rv-hint\">Paid " + Ui.Cash(bk.Paid) + " on " + Ui.Enc(bk.Card) + " ending " + Ui.Enc(bk.Last4) + " (demo, nothing was charged). Keep your code and last name to view or cancel the booking.</p>");
            if (mem != null)
            {
                sb.Append("<p class=\"rv-ok-box\"><b>+" + Ui.Num(q.Earn) + " points</b> will be added to your Circle account when you check out." + (bk.PointsUsed > 0 ? " You used " + Ui.Num(bk.PointsUsed) + " points; " + Ui.Num(mem.Balance) + " left." : string.Empty) + "</p>");
            }
            else
            {
                sb.Append("<p class=\"rv-info-box\">Join SkyHotel Circle to earn points on your next stay. <a href=\"Membership\"><b>Join free</b></a></p>");
            }
            sb.Append("<div class=\"rv-acts rv-acts-c\"><a class=\"rv-btn\" href=\"MyBooking?c=" + Uri.EscapeDataString(bk.Code) + "\">Manage booking</a><a class=\"rv-btn rv-btn-ghost\" href=\"Home\">Back to home</a></div></div>");
            response.SetElementContents("rv-s4", sb.ToString());
            response.SetElementContents("rv-sum", Summary(fresh, q, k.In, k.Out, k.Adults, k.Children, plan, mem, true));
            response.ExecuteScript("ReservationJs.booked();");
            return response;
        }

        public async Task<ApiResponse> MemberIn()
        {
            ApiResponse response = new ApiResponse();
            string email = Ui.Clip(GetDataValue("memail"), 100).ToLowerInvariant();
            string pass = GetDataValue("mpass");
            if (Auth.Locked("m:" + email))
            {
                response.SetElementContents("rv-e-msign", "Too many attempts. Wait five minutes and try again.");
                return response;
            }
            string sbx = Readable(GetDataValue("sbx"));
            SiteData site = await Store.LoadSite(sbx);
            Guest? g = site.Guests.FirstOrDefault(x => x.Member && x.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (g == null || pass.Length > 100 || !Access.Verify(pass, g.Hash))
            {
                Auth.Fail("m:" + email);
                response.SetElementContents("rv-e-msign", "That email and password don't match a member account.");
                return response;
            }
            if (Store.Demo)
            {
                sbx = await Writable(response, sbx);
            }
            string tok = await Auth.Open(sbx, g.Id, DateTime.UtcNow.AddHours(8), "member");
            response.ExecuteScript("ReservationJs.signedIn('" + tok + "');ReservationJs.memberReady();");
            return response;
        }
    }
}
