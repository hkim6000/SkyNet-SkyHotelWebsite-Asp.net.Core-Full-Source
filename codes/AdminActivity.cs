using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyHotel.Core;
using SkyHotel.Models;
using SkyNet;

namespace SkyHotel.codes
{
    public class AdminActivity : WebPage
    {
        private const string AreaKey = "";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Activity");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--lv-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("lv-menu", Auth.Menu("lv", "AdminActivity", c, site));
            response.SetElementContents("lv-who", Auth.Who("lv", c));
            response.SetElementContents("lv-demo", Auth.DemoPill("lv", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminActivityJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"lv-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"lv-panel\"><p>You don't have access to this area. Ask the general manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminActivityJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminActivityJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("lv-main", NoAccess("Activity"));
                return response;
            }
            response.SetElementContents("lv-main", await MainHtml(c, site));
            response.ExecuteScript("AdminActivityJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"lv-tr lv-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private const int Size = 20;

        private static async Task<string> MainHtml(Ctx c, SiteData site)
        {
            List<Staff> staff = await Store.LoadStaff(c.Sbx);
            bool all = Access.Can(c.U, "activity", 'V');
            StringBuilder sb = new StringBuilder("<div class=\"lv-ptop\"><div><h1>" + (all ? "Activity log" : "My activity") + "</h1><p>" + (all ? "Everything staff and the website do, including actions the server refused." : "Your own actions, including any the server refused.") + "</p></div></div><div class=\"lv-bar\">");
            if (all)
            {
                sb.Append("<label>Person<input type=\"text\" id=\"lv-whoq\" list=\"lv-wholist\" placeholder=\"Everyone\" maxlength=\"40\" oninput=\"AdminActivityJs.search()\"></label>" + Ui.DataList("lv-wholist", staff.Select(s => s.Name).Append("Website")));
            }
            sb.Append("<label>Search<input type=\"search\" id=\"lv-q\" list=\"lv-alist\" placeholder=\"Action or item\" maxlength=\"60\" oninput=\"AdminActivityJs.search()\"></label>" + Ui.DataList("lv-alist", (await Store.LoadActivity(c.Sbx)).Select(a => a.Action).Distinct()) + "<label>Outcome<select id=\"lv-aok\" onchange=\"AdminActivityJs.list(1)\">" + Ui.Opts(new[] { ("", "All outcomes"), ("1", "Done"), ("0", "Refused") }, string.Empty) + "</select></label></div><div class=\"lv-bar\" id=\"lv-perbar\">" + Ui.PeriodBar("lv", "AdminActivityJs.per", "today", string.Empty, string.Empty) + "</div><div class=\"lv-tbl lv-lvtbl\" id=\"lv-list\"></div><div id=\"lv-pager\"></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminActivityJs.expired();");
                return response;
            }
            Dictionary<string, Staff> staff = Ui.StaffMap(await Store.LoadStaff(c.Sbx));
            bool all = Access.Can(c.U, "activity", 'V');
            string whoq = all ? Ui.Clip(GetDataValue("whoq"), 40) : string.Empty;
            string aq = Ui.Clip(GetDataValue("aq"), 60);
            string ok = GetDataValue("aok");
            string per = GetDataValue("per") == string.Empty ? "today" : Per(GetDataValue("per"));
            (DateTime? from, DateTime? to) = Ui.Period(per, GetDataValue("pfrom"), GetDataValue("pto"));
            IEnumerable<Activity> q = (await Store.LoadActivity(c.Sbx)).Where(a => (all || a.User == c.U.Id) && Ui.InPeriod(a.When, from, to));
            if (whoq != string.Empty)
            {
                q = q.Where(a => Who(staff, a.User).Contains(whoq, StringComparison.OrdinalIgnoreCase));
            }
            if (aq != string.Empty)
            {
                q = q.Where(a => a.Action.Contains(aq, StringComparison.OrdinalIgnoreCase) || a.Target.Contains(aq, StringComparison.OrdinalIgnoreCase));
            }
            if (ok == "1" || ok == "0")
            {
                q = q.Where(a => a.Ok == (ok == "1"));
            }
            List<Activity> list = q.ToList();
            int page = Ui.PageOf(GetDataValue("pg"), Size, list.Count);
            StringBuilder sb = new StringBuilder(Head(new[] { ("When", string.Empty), ("Who", string.Empty), ("Action", string.Empty), ("Item", string.Empty), ("Result", string.Empty) }));
            if (list.Count == 0)
            {
                sb.Append("<div class=\"lv-tr\"><span class=\"lv-none lv-wide\">Nothing in this period.</span></div>");
            }
            foreach (Activity a in list.Skip((page - 1) * Size).Take(Size))
            {
                sb.Append("<div class=\"lv-tr" + (a.Ok ? string.Empty : " lv-bad") + "\"><span data-l=\"When\">" + a.When.ToString("d MMM, HH:mm", Ui.Inv) + "</span><span data-l=\"Who\"><b>" + Ui.Enc(Who(staff, a.User)) + "</b></span><span data-l=\"Action\">" + Ui.Enc(a.Action) + "</span><span data-l=\"Item\" class=\"lv-wide\" title=\"" + Ui.Attr(a.Target) + "\">" + Ui.Enc(a.Target) + "</span><span data-l=\"Result\">" + (a.Ok ? "<span class=\"lv-yes\">Done</span>" : "<span class=\"lv-no\">Refused</span>") + "</span></div>");
            }
            response.SetElementContents("lv-list", sb.ToString());
            response.SetElementContents("lv-pager", Ui.Pager("lv", page, Size, list.Count, "entry", "entries", "AdminActivityJs.list"));
            response.SetElementContents("lv-perbar", Ui.PeriodBar("lv", "AdminActivityJs.per", per, GetDataValue("pfrom"), GetDataValue("pto")));
            return response;
        }
    }
}
