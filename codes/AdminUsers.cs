using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyHotel.Core;
using SkyHotel.Models;
using SkyNet;

namespace SkyHotel.codes
{
    public class AdminUsers : WebPage
    {
        private const string AreaKey = "users";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Users");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--us-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("us-menu", Auth.Menu("us", "AdminUsers", c, site));
            response.SetElementContents("us-who", Auth.Who("us", c));
            response.SetElementContents("us-demo", Auth.DemoPill("us", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminUsersJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"us-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"us-panel\"><p>You don't have access to this area. Ask the general manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminUsersJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminUsersJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("us-main", NoAccess("Users"));
                return response;
            }
            response.SetElementContents("us-main", await MainHtml(c, site));
            response.ExecuteScript("AdminUsersJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"us-tr us-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private const int Size = 20;

        private static string Summary(Staff u)
        {
            if (u.Admin)
            {
                return "<b>Everything</b>, including users and settings";
            }
            List<string> parts = new List<string>();
            foreach (Area a in Access.Areas)
            {
                string lv = Access.Level(u, a.Key);
                if (lv == string.Empty)
                {
                    continue;
                }
                parts.Add("<b>" + Ui.Enc(a.Name) + "</b> " + (lv.Contains('E') ? "edit" : "view") + (lv.Contains('X') ? " + " + Ui.Enc(a.Extra.ToLowerInvariant()) : string.Empty));
            }
            return parts.Count == 0 ? "No access yet" : string.Join(" &middot; ", parts);
        }

        private static async Task<string> MainHtml(Ctx c, SiteData site)
        {
            List<Staff> staff = await Store.LoadStaff(c.Sbx);
            StringBuilder sb = new StringBuilder("<div class=\"us-ptop\"><div><h1>Users</h1><p>Give each person access to the areas they manage. View lets them look; Edit lets them change; some areas have one extra right.</p></div><div class=\"us-acts\"><button type=\"button\" class=\"us-btn us-btn-accent\" onclick=\"AdminUsersJs.edit('')\">Add a user</button></div></div>");
            sb.Append("<div class=\"us-bar\"><label>Search<input type=\"search\" id=\"us-q\" list=\"us-qlist\" placeholder=\"Name, login or job title\" maxlength=\"40\" oninput=\"AdminUsersJs.search()\"></label>" + Ui.DataList("us-qlist", staff.Select(s => s.Name)) + "<label>Area<select id=\"us-urole\" onchange=\"AdminUsersJs.list(1)\"><option value=\"\">Any area</option><option value=\"admin\">General managers</option>" + Ui.Opts(Access.Areas.Where(a => !a.AdminOnly).Select(a => (a.Key, a.Name)), string.Empty) + "</select></label></div><div class=\"us-tbl us-ustbl\" id=\"us-list\"></div><div id=\"us-pager\"></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminUsersJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            List<Staff> staff = await Store.LoadStaff(c.Sbx);
            string term = Ui.Clip(GetDataValue("term"), 40);
            string role = Ui.Clip(GetDataValue("urole"), 20);
            List<Staff> all = staff.Where(u => (term == string.Empty || u.Name.Contains(term, StringComparison.OrdinalIgnoreCase) || u.Login.Contains(term, StringComparison.OrdinalIgnoreCase) || u.Title.Contains(term, StringComparison.OrdinalIgnoreCase)) && (role == string.Empty || (role == "admin" ? u.Admin : Access.Can(u, role, 'V')))).OrderByDescending(u => u.Admin).ThenBy(u => u.Name).ToList();
            int page = Ui.PageOf(GetDataValue("pg"), Size, all.Count);
            StringBuilder sb = new StringBuilder(Head(new[] { ("Person", string.Empty), ("Login", string.Empty), ("Access", string.Empty), ("Status", string.Empty), (string.Empty, string.Empty) }));
            if (all.Count == 0)
            {
                sb.Append("<div class=\"us-tr\"><span class=\"us-none us-wide\">No users match.</span></div>");
            }
            foreach (Staff u in all.Skip((page - 1) * Size).Take(Size))
            {
                sb.Append("<div class=\"us-tr\"><span class=\"us-wide\" style=\"display:flex;gap:10px;align-items:center\">" + Ui.Avatar("us", u) + "<span style=\"min-width:0\"><b>" + Ui.Enc(u.Name) + "</b><small>" + Ui.Enc(u.Title) + "</small></span></span><span data-l=\"Login\" class=\"us-code\">" + Ui.Enc(u.Login) + "</span><span data-l=\"Access\" class=\"us-areas us-wide\">" + Summary(u) + "</span><span data-l=\"Status\"><span class=\"us-st us-st-" + (u.Active ? "live\">Active" : "off\">Off") + "</span></span><span><button type=\"button\" class=\"us-btn us-btn-sm us-btn-ghost\" onclick=\"AdminUsersJs.edit('" + Ui.Attr(u.Id) + "')\">Edit</button></span></div>");
            }
            response.SetElementContents("us-list", sb.ToString());
            response.SetElementContents("us-pager", Ui.Pager("us", page, Size, all.Count, "user", "users", "AdminUsersJs.list"));
            return response;
        }

        public async Task<ApiResponse> Form()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminUsersJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "users", "form", "Only the general manager manages users.");
            }
            List<Staff> staff = await Store.LoadStaff(c.Sbx);
            Staff? u = staff.FirstOrDefault(x => x.Id == GetDataValue("uid"));
            Staff v = u ?? new Staff();
            StringBuilder sb = new StringBuilder("<h2>" + (u == null ? "Add a user" : Ui.Enc(u.Name)) + "</h2><div class=\"us-form\"><div class=\"us-f3\"><label class=\"us-field\"><span>Full name</span><input type=\"text\" id=\"us-uname\" maxlength=\"60\" value=\"" + Ui.Attr(v.Name) + "\"></label><label class=\"us-field\"><span>User name</span><input type=\"text\" id=\"us-ulogin\" maxlength=\"20\" autocapitalize=\"none\" value=\"" + Ui.Attr(v.Login) + "\"" + (u != null ? " readonly" : string.Empty) + "></label><label class=\"us-field\"><span>Job title</span><input type=\"text\" id=\"us-utitle\" maxlength=\"60\" value=\"" + Ui.Attr(v.Title) + "\"></label></div><div class=\"us-f3\"><label class=\"us-field\"><span>" + (u == null ? "Password" : "New password (optional)") + "</span><input type=\"password\" id=\"us-upass\" maxlength=\"100\" autocomplete=\"new-password\"></label><label class=\"us-chk\" style=\"align-self:end\"><input type=\"checkbox\" id=\"us-uadmin\" onchange=\"AdminUsersJs.adminToggle()\"" + (v.Admin ? " checked" : string.Empty) + "> General manager (every right)</label><label class=\"us-chk\" style=\"align-self:end\"><input type=\"checkbox\" id=\"us-uactive\"" + (u == null || v.Active ? " checked" : string.Empty) + "> Can sign in</label></div>");
            sb.Append("<div><span class=\"us-hint\">What this person manages</span><div class=\"us-matrix" + (v.Admin ? " us-all" : string.Empty) + "\" id=\"us-matrix\"><div class=\"us-mrow\"><span>Area</span><span style=\"text-align:center\">None</span><span style=\"text-align:center\">View</span><span style=\"text-align:center\">Edit</span><span>Extra right</span></div>");
            foreach (Area a in Access.Areas.Where(a => !a.AdminOnly))
            {
                string lv = v.Admin ? "VE" : Access.Level(v, a.Key);
                string cur = lv.Contains('E') ? "VE" : lv.Contains('V') ? "V" : string.Empty;
                string n = "us-p-" + a.Key;
                sb.Append("<div class=\"us-mrow\" data-area=\"" + a.Key + "\"><span><b>" + Ui.Enc(a.Name) + "</b></span><label><input type=\"radio\" name=\"" + n + "\" value=\"\"" + (cur == string.Empty ? " checked" : string.Empty) + " aria-label=\"No access to " + Ui.Attr(a.Name) + "\"></label><label><input type=\"radio\" name=\"" + n + "\" value=\"V\"" + (cur == "V" ? " checked" : string.Empty) + " aria-label=\"View " + Ui.Attr(a.Name) + "\"></label><label>" + (a.NoEdit ? "<span class=\"us-hint\">&ndash;</span>" : "<input type=\"radio\" name=\"" + n + "\" value=\"VE\"" + (cur == "VE" ? " checked" : string.Empty) + " aria-label=\"Edit " + Ui.Attr(a.Name) + "\">") + "</label><span>" + (a.Extra != string.Empty ? "<label class=\"us-x\"><input type=\"checkbox\"" + (lv.Contains('X') ? " checked" : string.Empty) + "> " + Ui.Enc(a.Extra) + "</label>" : string.Empty) + "</span></div>");
            }
            sb.Append("</div><p class=\"us-hint\" style=\"margin-top:6px\">Extra rights need Edit. Users and Settings belong to general managers only. Everyone can see their own activity.</p></div><em class=\"us-err\" id=\"us-e-user\"></em></div><div class=\"us-mfoot\"><button type=\"button\" class=\"us-btn us-btn-ghost\" onclick=\"AdminUsersJs.closeModal()\">Cancel</button><button type=\"button\" class=\"us-btn us-btn-accent\" onclick=\"AdminUsersJs.save('" + Ui.Attr(u?.Id ?? string.Empty) + "')\">Save</button></div>");
            response.SetElementContents("us-mbody", sb.ToString());
            response.ExecuteScript("AdminUsersJs.openModal();");
            return response;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminUsersJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "users", GetDataValue("uid"), "Only the general manager manages users.");
            }
            string id = Ui.Clip(GetDataValue("uid"), 40);
            string name = Regex.Replace(Ui.Clip(GetDataValue("uname"), 60), "\\s+", " ");
            string login = Regex.Replace(Ui.Clip(GetDataValue("ulogin"), 20).ToLowerInvariant(), "[^a-z0-9._-]", string.Empty);
            string title = Ui.Clip(GetDataValue("utitle"), 60);
            string pass = GetDataValue("upass");
            bool admin = GetDataValue("uadmin") == "1";
            bool active = GetDataValue("uactive") == "1";
            Dictionary<string, string> perms = new Dictionary<string, string>();
            foreach (string part in GetDataValue("uperms").Split('|'))
            {
                string[] kv = part.Split(':');
                if (kv.Length == 2)
                {
                    string v = Access.Clean(kv[0], kv[1]);
                    if (v != string.Empty)
                    {
                        perms[kv[0]] = v;
                    }
                }
            }
            string err = name.Length < 2 ? "Enter the person's name." : string.Empty;
            string saved = string.Empty;
            string savedId = string.Empty;
            using (await Store.Hold())
            {
                List<Staff> staff = await Store.LoadStaff(c.Sbx);
                Staff? u = staff.FirstOrDefault(x => x.Id == id);
                if (err == string.Empty && u == null)
                {
                    if (login.Length < 3)
                    {
                        err = "Choose a user name of 3 characters or more.";
                    }
                    else if (staff.Any(x => x.Login == login))
                    {
                        err = "That user name is taken.";
                    }
                    else if (pass.Length < 8)
                    {
                        err = "Give new users a password of 8 characters or more.";
                    }
                    else if (staff.Count >= 60)
                    {
                        err = "That's the most users this demo allows.";
                    }
                }
                if (err == string.Empty && pass != string.Empty && (pass.Length < 8 || pass.Length > 100))
                {
                    err = "Passwords need 8 characters or more.";
                }
                if (err == string.Empty && u != null && u.Id == c.U.Id && (!admin || !active))
                {
                    err = "You can't remove your own general manager rights or switch yourself off.";
                }
                if (err == string.Empty)
                {
                    if (u == null)
                    {
                        u = new Staff { Id = "u-" + login, Login = login, Demo = Store.Demo };
                        staff.Add(u);
                    }
                    u.Name = name;
                    u.Title = title;
                    u.Admin = admin;
                    u.Active = active;
                    u.Perms = admin ? new Dictionary<string, string>() : perms;
                    if (pass != string.Empty)
                    {
                        u.Hash = Access.HashPassword(pass);
                        if (!Store.Demo)
                        {
                            u.Demo = false;
                        }
                    }
                    saved = u.Login;
                    savedId = u.Id;
                    await Store.SaveStaff(staff, c.Sbx);
                }
            }
            if (err != string.Empty)
            {
                response.SetElementContents("us-e-user", err);
                return response;
            }
            if (savedId != c.U.Id && (pass != string.Empty || !active))
            {
                await Auth.CloseUser(c.Sbx, savedId, string.Empty);
            }
            await Store.Log(c.Sbx, c.U.Id, "user", saved + (admin ? " · general manager" : " · " + string.Join(", ", perms.Select(p => p.Key + " " + p.Value))), true);
            response.ExecuteScript("AdminUsersJs.closeModal();AdminUsersJs.list(1);AdminUsersJs.toast('Saved. Changes apply on their next click.');");
            return response;
        }
    }
}
