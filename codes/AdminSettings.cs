using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyHotel.Core;
using SkyHotel.Models;
using SkyNet;

namespace SkyHotel.codes
{
    public class AdminSettings : WebPage
    {
        private const string AreaKey = "settings";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Settings");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--st-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("st-menu", Auth.Menu("st", "AdminSettings", c, site));
            response.SetElementContents("st-who", Auth.Who("st", c));
            response.SetElementContents("st-demo", Auth.DemoPill("st", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminSettingsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"st-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"st-panel\"><p>You don't have access to this area. Ask the general manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminSettingsJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminSettingsJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("st-main", NoAccess("Settings"));
                return response;
            }
            response.SetElementContents("st-main", await MainHtml(c, site));
            response.ExecuteScript("AdminSettingsJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"st-tr st-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static async Task<string> MainHtml(Ctx c, SiteData site)
        {
            HotelSettings s = site.Settings;
            StringBuilder sb = new StringBuilder("<div class=\"st-ptop\"><div><h1>Settings</h1><p>Hotel details, look and the rules the booking page follows.</p></div></div><form class=\"st-form\" onsubmit=\"return AdminSettingsJs.save()\" novalidate>");
            sb.Append("<section class=\"st-panel\"><h2>Hotel</h2><div class=\"st-form\"><div class=\"st-f3\"><label class=\"st-field\"><span>Name</span><input type=\"text\" id=\"st-sname\" maxlength=\"40\" value=\"" + Ui.Attr(s.Name) + "\"></label><label class=\"st-field\"><span>Town</span><input type=\"text\" id=\"st-stown\" maxlength=\"40\" value=\"" + Ui.Attr(s.Town) + "\"></label><label class=\"st-field\"><span>Tagline</span><input type=\"text\" id=\"st-stag\" maxlength=\"80\" value=\"" + Ui.Attr(s.Tagline) + "\"></label></div><div class=\"st-f3\"><label class=\"st-field\"><span>Address</span><input type=\"text\" id=\"st-saddr\" maxlength=\"100\" value=\"" + Ui.Attr(s.Address) + "\"></label><label class=\"st-field\"><span>Phone</span><input type=\"text\" id=\"st-sphone\" maxlength=\"30\" value=\"" + Ui.Attr(s.Phone) + "\"></label><label class=\"st-field\"><span>Email</span><input type=\"email\" id=\"st-semail\" maxlength=\"80\" value=\"" + Ui.Attr(s.Email) + "\"></label></div></div></section>");
            sb.Append("<section class=\"st-panel\"><h2>Look</h2><div class=\"st-f3\"><label class=\"st-field\"><span>Accent colour</span><input type=\"color\" id=\"st-saccent\" value=\"" + Ui.Attr(s.Accent) + "\"></label><label class=\"st-field\"><span>Website theme</span><select id=\"st-stheme\">" + Ui.Opts(new[] { ("light", "Light"), ("dark", "Dark") }, s.Theme) + "</select></label></div></section>");
            sb.Append("<section class=\"st-panel\"><h2>Booking rules</h2><div class=\"st-f4\"><label class=\"st-field\"><span>Tax (%)</span><input type=\"number\" id=\"st-stax\" min=\"0\" max=\"30\" step=\"0.5\" value=\"" + s.Tax.ToString("0.##", Ui.Inv) + "\"></label><label class=\"st-field\"><span>Check-in from</span><input type=\"time\" id=\"st-sin\" value=\"" + Ui.Attr(s.CheckIn) + "\"></label><label class=\"st-field\"><span>Check-out until</span><input type=\"time\" id=\"st-sout\" value=\"" + Ui.Attr(s.CheckOut) + "\"></label><label class=\"st-field\"><span>Free cancellation (hours before)</span><input type=\"number\" id=\"st-scancel\" min=\"0\" max=\"336\" value=\"" + s.CancelHours + "\"></label></div><div class=\"st-f4\" style=\"margin-top:12px\"><label class=\"st-field\"><span>Breakfast per person ($)</span><input type=\"number\" id=\"st-sbreak\" min=\"0\" max=\"200\" value=\"" + s.Breakfast.ToString("0", Ui.Inv) + "\"></label><label class=\"st-field\"><span>Security deposit ($)</span><input type=\"number\" id=\"st-sdep\" min=\"0\" max=\"5000\" value=\"" + s.Deposit.ToString("0", Ui.Inv) + "\"></label><label class=\"st-field\"><span>Hotel country</span><select id=\"st-scountry\">" + Ui.Opts(Ui.Countries.Select(x => (x, x)), s.Country) + "</select></label></div><p class=\"st-hint\" style=\"margin-top:10px\">The deposit is taken at check-in (0 = none). Guests from outside the hotel country need a passport no. at check-in.</p><p class=\"st-hint\" style=\"margin-top:10px\">Rules apply to new bookings. Existing bookings keep the price they were booked at.</p></section>");
            sb.Append("<div class=\"st-acts\" style=\"margin-top:16px;justify-content:flex-end\"><em class=\"st-err\" id=\"st-e-set\"></em><button type=\"submit\" class=\"st-btn st-btn-accent\">Save settings</button></div></form>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminSettingsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "settings", "settings", "Only the general manager changes settings.");
            }
            string name = Ui.Clip(GetDataValue("sname"), 40);
            string accent = Regex.IsMatch(GetDataValue("saccent"), "^#[0-9a-fA-F]{6}$") ? GetDataValue("saccent") : "#B0874A";
            string email = Ui.Clip(GetDataValue("semail"), 80);
            if (name.Length < 2)
            {
                response.SetElementContents("st-e-set", "Enter the hotel name.");
                return response;
            }
            if (email != string.Empty && !Regex.IsMatch(email, "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$"))
            {
                response.SetElementContents("st-e-set", "Check the email address.");
                return response;
            }
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                HotelSettings s = site.Settings;
                s.Name = name;
                s.Tagline = Ui.Clip(GetDataValue("stag"), 80);
                s.Town = Ui.Clip(GetDataValue("stown"), 40);
                s.Address = Ui.Clip(GetDataValue("saddr"), 100);
                s.Phone = Ui.Clip(GetDataValue("sphone"), 30);
                s.Email = email;
                s.Accent = accent;
                s.Theme = GetDataValue("stheme") == "dark" ? "dark" : "light";
                s.Tax = decimal.TryParse(GetDataValue("stax"), System.Globalization.NumberStyles.Number, Ui.Inv, out decimal t) ? Math.Clamp(t, 0, 30) : s.Tax;
                s.CheckIn = Regex.IsMatch(GetDataValue("sin"), "^[0-2][0-9]:[0-5][0-9]$") ? GetDataValue("sin") : s.CheckIn;
                s.CheckOut = Regex.IsMatch(GetDataValue("sout"), "^[0-2][0-9]:[0-5][0-9]$") ? GetDataValue("sout") : s.CheckOut;
                s.CancelHours = int.TryParse(GetDataValue("scancel"), out int ch) ? Math.Clamp(ch, 0, 336) : s.CancelHours;
                s.Breakfast = decimal.TryParse(GetDataValue("sbreak"), System.Globalization.NumberStyles.Number, Ui.Inv, out decimal b) ? Math.Clamp(b, 0, 200) : s.Breakfast;
                s.Deposit = decimal.TryParse(GetDataValue("sdep"), System.Globalization.NumberStyles.Number, Ui.Inv, out decimal dp) ? Math.Round(Math.Clamp(dp, 0, 5000), 2) : s.Deposit;
                s.Country = Ui.Countries.Contains(GetDataValue("scountry")) ? GetDataValue("scountry") : s.Country;
                await Store.SaveSite(site, c.Sbx);
            }
            await Store.Log(c.Sbx, c.U.Id, "settings", name + " · tax " + GetDataValue("stax") + "%", true);
            response.ExecuteScript("AdminSettingsJs.toast('Settings saved');document.getElementById('st-site').style.setProperty('--st-accent','" + accent + "');");
            return response;
        }
    }
}
