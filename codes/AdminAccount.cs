using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyHotel.Core;
using SkyHotel.Models;
using SkyNet;

namespace SkyHotel.codes
{
    public class AdminAccount : WebPage
    {
        private const string AreaKey = "";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | My account");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--my-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("my-menu", Auth.Menu("my", "AdminAccount", c, site));
            response.SetElementContents("my-who", Auth.Who("my", c));
            response.SetElementContents("my-demo", Auth.DemoPill("my", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminAccountJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"my-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"my-panel\"><p>You don't have access to this area. Ask the general manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminAccountJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAccountJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("my-main", NoAccess("My account"));
                return response;
            }
            response.SetElementContents("my-main", await MainHtml(c, site));
            response.ExecuteScript("AdminAccountJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"my-tr my-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static async Task<string> MainHtml(Ctx c, SiteData site)
        {
            Staff u = c.U;
            StringBuilder sb = new StringBuilder("<div class=\"my-ptop\"><div><h1>My account</h1><p>" + Ui.Enc(u.Title) + " &middot; signed in as <b>" + Ui.Enc(u.Login) + "</b></p></div></div><div class=\"my-grid2\" style=\"margin-top:0\"><section class=\"my-panel\"><h2>Profile &amp; password</h2><form class=\"my-form\" onsubmit=\"return AdminAccountJs.save()\" novalidate><label class=\"my-field\"><span>Name</span><input type=\"text\" id=\"my-aname\" maxlength=\"60\" value=\"" + Ui.Attr(u.Name) + "\"></label><label class=\"my-field\"><span>Current password</span><input type=\"password\" id=\"my-acur\" maxlength=\"100\" autocomplete=\"current-password\"></label><label class=\"my-field\"><span>New password (optional)</span><input type=\"password\" id=\"my-anew\" maxlength=\"100\" autocomplete=\"new-password\"></label><em class=\"my-err\" id=\"my-e-acc\"></em><div><button type=\"submit\" class=\"my-btn my-btn-accent\">Save</button></div></form></section><section class=\"my-panel\"><h2>What you can do</h2>");
            if (u.Admin)
            {
                sb.Append("<p>You are a general manager: every area, plus users and settings.</p>");
            }
            else
            {
                foreach (Area a in Access.Areas)
                {
                    string lv = Access.Level(u, a.Key);
                    sb.Append("<div class=\"my-row\"><div><b>" + Ui.Enc(a.Name) + "</b></div><span class=\"my-st my-st-" + (lv.Contains('E') ? "live\">Edit" + (lv.Contains('X') ? " + " + Ui.Enc(a.Extra.ToLowerInvariant()) : string.Empty) : lv.Contains('V') ? "inspected\">View" : "off\">None") + "</span></div>");
                }
            }
            sb.Append("</section></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAccountJs.expired();");
                return response;
            }
            string name = Regex.Replace(Ui.Clip(GetDataValue("aname"), 60), "\\s+", " ");
            string cur = GetDataValue("acur");
            string nw = GetDataValue("anew");
            if (name.Length < 2)
            {
                response.SetElementContents("my-e-acc", "Enter your name.");
                return response;
            }
            using (await Store.Hold())
            {
                List<Staff> staff = await Store.LoadStaff(c.Sbx);
                Staff? u = staff.FirstOrDefault(x => x.Id == c.U.Id);
                if (u == null)
                {
                    return response;
                }
                if (nw != string.Empty)
                {
                    if (!Access.Verify(cur, u.Hash))
                    {
                        response.SetElementContents("my-e-acc", "Your current password isn't right.");
                        return response;
                    }
                    if (nw.Length < 8 || nw.Length > 100)
                    {
                        response.SetElementContents("my-e-acc", "Use 8 characters or more.");
                        return response;
                    }
                    u.Hash = Access.HashPassword(nw);
                    if (!Store.Demo)
                    {
                        u.Demo = false;
                    }
                }
                u.Name = name;
                await Store.SaveStaff(staff, c.Sbx);
            }
            if (nw != string.Empty)
            {
                await Auth.CloseUser(c.Sbx, c.U.Id, Store.CleanId(GetDataValue("tok")));
            }
            await Store.Log(c.Sbx, c.U.Id, "account", nw != string.Empty ? "password changed" : "name", true);
            response.ExecuteScript("document.getElementById('my-acur').value='';document.getElementById('my-anew').value='';AdminAccountJs.toast('Saved');");
            return response;
        }
    }
}
