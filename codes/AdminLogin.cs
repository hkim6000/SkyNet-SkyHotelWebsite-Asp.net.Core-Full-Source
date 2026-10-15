using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyHotel.Core;
using SkyHotel.Models;
using SkyNet;

namespace SkyHotel.codes
{
    public class AdminLogin : WebPage
    {
        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Sign in");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string demo = string.Empty;
            if (Store.Demo)
            {
                List<Staff> staff = await Store.LoadStaff();
                StringBuilder sb = new StringBuilder("<p class=\"lg-lhint\"><b>Live demo.</b> Pick a role. Each one sees different menus and buttons. You get a private copy of the hotel for one hour.</p><div class=\"lg-ucards\">");
                foreach (Staff u in staff.Where(x => x.Demo && x.Active))
                {
                    sb.Append("<button type=\"button\" class=\"lg-ucard\" onclick=\"AdminLoginJs.as('" + Ui.Attr(u.Login) + "')\">" + Ui.Avatar("lg", u) + "<span><b>" + Ui.Enc(u.Name) + "</b><small>" + Ui.Enc(u.Title) + "</small></span></button>");
                }
                sb.Append("</div><div class=\"lg-lor\">or sign in with the password&nbsp;<b>demo</b></div>");
                demo = sb.ToString();
            }
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--lg-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_brand}", Ui.Enc(site.Settings.Name))
                .Replace("{plhd_demo}", demo)
                .Replace("{plhd_user}", Store.Demo ? "elena" : string.Empty)
                .Replace("{plhd_pass}", Store.Demo ? "demo" : string.Empty);
        }

        public async Task<ApiResponse> SignIn()
        {
            ApiResponse response = new ApiResponse();
            string login = Ui.Clip(GetDataValue("user"), 40).ToLowerInvariant();
            string pass = Ui.Clip(GetDataValue("pass"), 200);
            if (login == string.Empty || pass == string.Empty)
            {
                response.SetElementContents("lg-e-pass", "Enter your user name and password.");
                return response;
            }
            if (Auth.Locked(login))
            {
                response.SetElementContents("lg-e-pass", "Too many attempts. Please wait a few minutes.");
                return response;
            }
            Staff? user = null;
            string sbx = string.Empty;
            if (Store.Demo)
            {
                sbx = Store.Sandbox(GetDataValue("sbx"));
                List<Staff> staff = await Store.LoadStaff(sbx);
                Staff? u = staff.FirstOrDefault(x => x.Login == login && x.Active);
                if (u != null && Access.Verify(pass, u.Hash))
                {
                    user = u;
                    if (sbx == string.Empty)
                    {
                        sbx = await Store.NewSandbox();
                    }
                }
            }
            else
            {
                List<Staff> staff = await Store.LoadStaff();
                Staff? u = staff.FirstOrDefault(x => x.Login == login && x.Active);
                if (u != null && !u.Demo && Access.Verify(pass, u.Hash))
                {
                    user = u;
                }
                else if (login == Store.Cfg("app.admin.user").ToLowerInvariant() && Access.VerifySha(pass, Store.Cfg("app.admin.passhash")))
                {
                    using (await Store.Hold())
                    {
                        staff = await Store.LoadStaff();
                        u = staff.FirstOrDefault(x => x.Login == login);
                        if (u == null)
                        {
                            u = new Staff { Id = "u-" + login, Login = login, Name = login, Title = "General Manager" };
                            staff.Insert(0, u);
                        }
                        u.Admin = true;
                        u.Active = true;
                        u.Demo = false;
                        await Store.SaveStaff(staff, string.Empty);
                    }
                    user = u;
                }
            }
            if (user == null)
            {
                Auth.Fail(login);
                response.SetElementContents("lg-e-pass", "Wrong user name or password.");
                return response;
            }
            DateTime exp = Store.Demo ? Store.Made(sbx).AddMinutes(60) : DateTime.UtcNow.AddHours(8);
            string tok = await Auth.Open(sbx, user.Id, exp);
            await Store.Log(sbx, user.Id, "signin", user.Login, true);
            int secs = Math.Max(60, (int)(exp - DateTime.UtcNow).TotalSeconds);
            response.ExecuteScript("AdminLoginJs.signedIn('" + tok + "','" + sbx + "'," + secs + ");");
            return response;
        }
    }
}
