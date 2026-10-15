using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyHotel.Core;
using SkyHotel.Models;
using SkyNet;

namespace SkyHotel.codes
{
    public class AdminMembers : WebPage
    {
        private const string AreaKey = "members";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Members");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--cm-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("cm-menu", Auth.Menu("cm", "AdminMembers", c, site));
            response.SetElementContents("cm-who", Auth.Who("cm", c));
            response.SetElementContents("cm-demo", Auth.DemoPill("cm", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminMembersJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"cm-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"cm-panel\"><p>You don't have access to this area. Ask the general manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminMembersJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMembersJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("cm-main", NoAccess("Members"));
                return response;
            }
            response.SetElementContents("cm-main", await MainHtml(c, site));
            response.ExecuteScript("AdminMembersJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"cm-tr cm-th\">");
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
            List<Guest> ms = site.Guests.Where(g => g.Member).ToList();
            StringBuilder sb = new StringBuilder("<div class=\"cm-ptop\"><div><h1>Members</h1><p>SkyHotel Circle: " + ms.Count + " members. $1 = 1 point, credited at check-out. Grades follow lifetime points.</p></div>" + (Access.Can(c.U, AreaKey, 'E') ? "<button type=\"button\" class=\"cm-btn cm-btn-accent\" onclick=\"AdminMembersJs.addForm()\">Add member</button>" : string.Empty) + "</div>");
            sb.Append("<div class=\"cm-chips\" style=\"margin-bottom:14px\">");
            foreach ((string key, string name, int min, int pct, string perks) in Ui.Grades)
            {
                sb.Append("<span class=\"cm-chip\" style=\"cursor:default\"><span class=\"cm-grade cm-g-" + key + "\">" + name + "</span><em>" + ms.Count(g => Ui.GradeOf(g.Lifetime).Key == key) + "</em></span>");
            }
            sb.Append("</div><div class=\"cm-tabs\"><button type=\"button\" class=\"cm-tab cm-act\" data-t=\"members\" onclick=\"AdminMembersJs.setTab('members')\">Members<em>" + ms.Count + "</em></button><button type=\"button\" class=\"cm-tab\" data-t=\"points\" onclick=\"AdminMembersJs.setTab('points')\">Points history<em>" + site.Points.Count + "</em></button></div>");
            sb.Append("<div class=\"cm-bar\" id=\"cm-mbar\"><label>Search<input type=\"search\" id=\"cm-q\" list=\"cm-qlist\" placeholder=\"Name, email or member no.\" maxlength=\"60\" oninput=\"AdminMembersJs.search()\"></label>" + Ui.DataList("cm-qlist", ms.Select(g => Ui.GuestName(g))) + "<label>Grade<select id=\"cm-mgrade\" onchange=\"AdminMembersJs.list(1)\"><option value=\"\">All grades</option>" + Ui.Opts(Ui.Grades.Select(g => (g.Key, g.Name)), string.Empty) + "</select></label><label>Sort<select id=\"cm-msort\" onchange=\"AdminMembersJs.list(1)\">" + Ui.Opts(new[] { ("life", "Lifetime points"), ("bal", "Points to spend"), ("joined", "Newest members"), ("name", "Name A-Z") }, "life") + "</select></label></div>");
            sb.Append("<div class=\"cm-bar\" id=\"cm-pbar\" hidden><label>Search<input type=\"search\" id=\"cm-q2\" list=\"cm-qlist\" placeholder=\"Member or note\" maxlength=\"60\" oninput=\"AdminMembersJs.search()\"></label><label>Type<select id=\"cm-pkind\" onchange=\"AdminMembersJs.list(1)\">" + Ui.Opts(new[] { ("", "All"), ("earn", "Earned"), ("redeem", "Used"), ("return", "Returned"), ("adjust", "Adjusted") }, string.Empty) + "</select></label></div>");
            sb.Append("<div class=\"cm-bar\" id=\"cm-perbar\">" + Ui.PeriodBar("cm", "AdminMembersJs.per", "all", string.Empty, string.Empty) + "</div><div class=\"cm-tbl cm-cmtbl\" id=\"cm-list\"></div><div id=\"cm-pager\"></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMembersJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            string term = Ui.Clip(GetDataValue("term"), 60);
            string per = Per(GetDataValue("per"));
            (DateTime? from, DateTime? to) = Ui.Period(per, GetDataValue("pfrom"), GetDataValue("pto"));
            Dictionary<string, Guest> gm = Ui.GuestMap(site);
            StringBuilder sb = new StringBuilder();
            int total;
            int page;
            if (GetDataValue("mtab") == "points")
            {
                Dictionary<string, Staff> staff = Ui.StaffMap(await Store.LoadStaff(c.Sbx));
                string kind = GetDataValue("pkind");
                List<PointEntry> all = site.Points.Where(p => (kind == string.Empty || p.Kind == kind) && Ui.InPeriod(p.When, from, to) && (term == string.Empty || p.Note.Contains(term, StringComparison.OrdinalIgnoreCase) || (gm.TryGetValue(p.Guest, out Guest? g) && (Ui.GuestName(g).Contains(term, StringComparison.OrdinalIgnoreCase) || g.MemberNo.Contains(term, StringComparison.OrdinalIgnoreCase))))).OrderByDescending(p => p.When).ToList();
                total = all.Count;
                page = Ui.PageOf(GetDataValue("pg"), Size, total);
                sb.Append(Head(new[] { ("When", string.Empty), ("Member", string.Empty), ("Type", string.Empty), ("Note", string.Empty), ("Points", "cm-num"), ("By", string.Empty) }));
                foreach (PointEntry p in all.Skip((page - 1) * Size).Take(Size))
                {
                    Guest? g = gm.TryGetValue(p.Guest, out Guest? x) ? x : null;
                    sb.Append("<div class=\"cm-tr\"><span data-l=\"When\">" + p.When.ToString("d MMM yyyy", Ui.Inv) + "</span><span data-l=\"Member\"><a href=\"#\" onclick=\"AdminMembersJs.open('" + Ui.Attr(p.Guest) + "');return false;\"><b>" + Ui.Enc(Ui.GuestName(g)) + "</b></a><small>" + Ui.Enc(g?.MemberNo) + "</small></span><span data-l=\"Type\">" + (p.Kind switch { "earn" => "Earned", "redeem" => "Used", "return" => "Returned", _ => "Adjusted" }) + "</span><span data-l=\"Note\">" + Ui.Enc(p.Note) + "</span><span data-l=\"Points\" class=\"cm-num " + (p.Pts >= 0 ? "cm-plus" : "cm-neg") + "\">" + (p.Pts >= 0 ? "+" : "&minus;") + Ui.Num(Math.Abs(p.Pts)) + "</span><span data-l=\"By\">" + Ui.Enc(p.By == string.Empty ? "Website" : Who(staff, p.By)) + "</span></div>");
                }
                response.ExecuteScript("document.getElementById('cm-list').className='cm-tbl cm-pttbl';");
            }
            else
            {
                string grade = GetDataValue("mgrade");
                IEnumerable<Guest> q = site.Guests.Where(g => g.Member && (grade == string.Empty || Ui.GradeOf(g.Lifetime).Key == grade) && (from == null && to == null || (g.Joined != null && Ui.InPeriod(g.Joined.Value, from, to))));
                if (term != string.Empty)
                {
                    q = q.Where(g => Ui.GuestName(g).Contains(term, StringComparison.OrdinalIgnoreCase) || g.Email.Contains(term, StringComparison.OrdinalIgnoreCase) || g.MemberNo.Contains(term, StringComparison.OrdinalIgnoreCase));
                }
                q = GetDataValue("msort") switch
                {
                    "bal" => q.OrderByDescending(g => g.Balance),
                    "joined" => q.OrderByDescending(g => g.Joined),
                    "name" => q.OrderBy(g => g.Last).ThenBy(g => g.First),
                    _ => q.OrderByDescending(g => g.Lifetime)
                };
                List<Guest> all = q.ToList();
                total = all.Count;
                page = Ui.PageOf(GetDataValue("pg"), Size, total);
                sb.Append(Head(new[] { ("Member no.", string.Empty), ("Name", string.Empty), ("Grade", string.Empty), ("Lifetime", "cm-num"), ("To spend", "cm-num"), ("Stays", string.Empty), ("Joined", string.Empty), (string.Empty, string.Empty) }));
                foreach (Guest g in all.Skip((page - 1) * Size).Take(Size))
                {
                    int stays = site.Bookings.Count(b => b.Guest == g.Id && b.Status == "out");
                    sb.Append("<div class=\"cm-tr\"><span data-l=\"Member no.\" class=\"cm-code\">" + Ui.Enc(g.MemberNo) + "</span><span data-l=\"Name\"><b>" + Ui.Enc(Ui.GuestName(g)) + (g.Vip ? "<span class=\"cm-vip\">VIP</span>" : string.Empty) + "</b><small>" + Ui.Enc(g.Email) + "</small></span><span data-l=\"Grade\">" + Ui.GradePill("cm", g) + "</span><span data-l=\"Lifetime\" class=\"cm-num\">" + Ui.Num(g.Lifetime) + "</span><span data-l=\"To spend\" class=\"cm-num\">" + Ui.Num(g.Balance) + "</span><span data-l=\"Stays\">" + stays + "</span><span data-l=\"Joined\">" + (g.Joined?.ToString("d MMM yyyy", Ui.Inv) ?? "&ndash;") + "</span><span><button type=\"button\" class=\"cm-btn cm-btn-sm cm-btn-ghost\" onclick=\"AdminMembersJs.open('" + Ui.Attr(g.Id) + "')\">Open</button></span></div>");
                }
                response.ExecuteScript("document.getElementById('cm-list').className='cm-tbl cm-cmtbl';");
            }
            if (total == 0)
            {
                sb.Append("<div class=\"cm-tr\"><span class=\"cm-none cm-wide\">Nothing matches.</span></div>");
            }
            response.SetElementContents("cm-list", sb.ToString());
            response.SetElementContents("cm-pager", Ui.Pager("cm", page, Size, total, "row", "rows", "AdminMembersJs.list"));
            response.SetElementContents("cm-perbar", Ui.PeriodBar("cm", "AdminMembersJs.per", per, GetDataValue("pfrom"), GetDataValue("pto")));
            return response;
        }

        private static string Card(SiteData site, Guest g, Ctx c, Dictionary<string, Staff> staff)
        {
            (string key, string name, int min, int pct, string perks) = Ui.GradeOf(g.Lifetime);
            (string Key, string Name, int Min, int Pct, string Perks)? next = Ui.NextGrade(g.Lifetime);
            bool edit = Access.Can(c.U, AreaKey, 'E');
            StringBuilder sb = new StringBuilder("<h2>" + Ui.Enc(Ui.GuestName(g)) + " " + Ui.GradePill("cm", g) + (g.Vip ? "<span class=\"cm-vip\">VIP</span>" : string.Empty) + "</h2><div class=\"cm-mgrid\"><div><div class=\"cm-mnum\"><div><small>Lifetime</small><b>" + Ui.Num(g.Lifetime) + "</b></div><div><small>To spend</small><b>" + Ui.Num(g.Balance) + "</b></div><div><small>Pending</small><b>" + Ui.Num(Book.Pending(site, g)) + "</b></div></div>");
            if (next != null)
            {
                int w = Math.Clamp((g.Lifetime - min) * 100 / Math.Max(1, next.Value.Min - min), 2, 100);
                sb.Append("<div class=\"cm-prog\"><i style=\"width:" + w + "%\"></i></div><p class=\"cm-hint\" style=\"margin:6px 0 12px\">" + Ui.Num(next.Value.Min - g.Lifetime) + " points to " + next.Value.Name + ". Benefits now: " + Ui.Enc(perks) + ".</p>");
            }
            sb.Append("<dl class=\"cm-dl\"><dt>Member no.</dt><dd class=\"cm-code\">" + Ui.Enc(g.MemberNo) + "</dd><dt>Email</dt><dd>" + Ui.Enc(g.Email) + "</dd><dt>Joined</dt><dd>" + (g.Joined?.ToString("d MMM yyyy", Ui.Inv) ?? "&ndash;") + "</dd></dl><h3>Profile</h3><div class=\"cm-form\"><div class=\"cm-f2\"><label class=\"cm-field\"><span>Phone</span><input type=\"tel\" id=\"cm-mphone\" maxlength=\"30\" value=\"" + Ui.Attr(g.Phone) + "\"" + (edit ? string.Empty : " readonly") + "></label><label class=\"cm-field\"><span>Country</span><input type=\"text\" id=\"cm-mcountry\" maxlength=\"40\" value=\"" + Ui.Attr(g.Country) + "\"" + (edit ? string.Empty : " readonly") + "></label></div><label class=\"cm-field\"><span>Notes for staff</span><textarea id=\"cm-mnotes\" rows=\"2\" maxlength=\"300\"" + (edit ? string.Empty : " readonly") + ">" + Ui.Enc(g.Notes) + "</textarea></label><label class=\"cm-chk\"><input type=\"checkbox\" id=\"cm-mvip\"" + (g.Vip ? " checked" : string.Empty) + (edit ? string.Empty : " disabled") + "> VIP guest</label>" + (edit ? "<div><button type=\"button\" class=\"cm-btn cm-btn-sm cm-btn-ghost\" onclick=\"AdminMembersJs.saveMember('" + Ui.Attr(g.Id) + "')\">Save profile</button></div>" : string.Empty) + "</div></div><div>");
            if (Access.Can(c.U, AreaKey, 'X'))
            {
                sb.Append("<h3 style=\"margin-top:0\">Adjust points</h3><div class=\"cm-adj\"><label class=\"cm-field\"><span>Points (+/&minus;)</span><input type=\"number\" id=\"cm-apts\" step=\"50\" min=\"-50000\" max=\"50000\" placeholder=\"500\"></label><label class=\"cm-field\"><span>Reason</span><input type=\"text\" id=\"cm-areason\" maxlength=\"80\" placeholder=\"Goodwill, correction...\"></label><button type=\"button\" class=\"cm-btn cm-btn-sm\" onclick=\"AdminMembersJs.adjust('" + Ui.Attr(g.Id) + "')\">Adjust</button></div><em class=\"cm-err\" id=\"cm-e-apts\"></em>");
            }
            sb.Append("<h3>Points history</h3><div class=\"cm-plist\">");
            List<PointEntry> pts = site.Points.Where(p => p.Guest == g.Id).OrderByDescending(p => p.When).ToList();
            if (pts.Count == 0)
            {
                sb.Append("<p class=\"cm-none\">No points yet.</p>");
            }
            foreach (PointEntry p in pts.Take(10))
            {
                sb.Append("<div class=\"cm-row\"><div><b>" + Ui.Enc(p.Note) + "</b><small>" + p.When.ToString("d MMM yyyy", Ui.Inv) + (p.By != string.Empty ? " &middot; " + Ui.Enc(Who(staff, p.By)) : string.Empty) + "</small></div><span class=\"" + (p.Pts >= 0 ? "cm-plus" : "cm-neg") + "\">" + (p.Pts >= 0 ? "+" : "&minus;") + Ui.Num(Math.Abs(p.Pts)) + "</span></div>");
            }
            if (pts.Count > 10)
            {
                sb.Append("<p class=\"cm-hint\">" + (pts.Count - 10) + " older entries in Points history.</p>");
            }
            sb.Append("</div><h3>Stays</h3>");
            List<Booking> bs = site.Bookings.Where(b => b.Guest == g.Id).OrderByDescending(b => b.In).ToList();
            if (bs.Count == 0)
            {
                sb.Append("<p class=\"cm-none\">No stays yet.</p>");
            }
            foreach (Booking b in bs.Take(6))
            {
                sb.Append("<div class=\"cm-row\"><div><b>" + Ui.Enc(Ui.TypeName(site, b.Type)) + "</b><small>" + Ui.Enc(b.Code) + " &middot; " + b.In.ToString("d MMM yyyy", Ui.Inv) + " &middot; " + Ui.Cash(b.Total) + "</small></div>" + Auth.StatusPill("cm", b) + "</div>");
            }
            sb.Append("</div></div><div class=\"cm-mfoot\"><button type=\"button\" class=\"cm-btn\" onclick=\"AdminMembersJs.closeModal()\">Close</button></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Open()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMembersJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Guest? g = site.Guests.FirstOrDefault(x => x.Id == GetDataValue("gid") && x.Member);
            if (g == null)
            {
                return response;
            }
            response.SetElementContents("cm-mbody", Card(site, g, c, Ui.StaffMap(await Store.LoadStaff(c.Sbx))));
            response.ExecuteScript("AdminMembersJs.openModal();");
            return response;
        }

        public async Task<ApiResponse> Adjust()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMembersJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'X'))
            {
                return await Refuse(response, c, "points-adjust", GetDataValue("gid"), "Adjusting points needs the Adjust points right.");
            }
            int pts = int.TryParse(GetDataValue("apts"), out int p) ? p : 0;
            string reason = Ui.Clip(GetDataValue("areason"), 80);
            if (pts == 0 || Math.Abs(pts) > 50000 || reason.Length < 3)
            {
                response.SetElementContents("cm-e-apts", "Enter up to 50,000 points and a reason.");
                return response;
            }
            string log;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Guest? g = site.Guests.FirstOrDefault(x => x.Id == GetDataValue("gid") && x.Member);
                if (g == null)
                {
                    return response;
                }
                if (pts < 0 && g.Balance + pts < 0)
                {
                    response.SetElementContents("cm-e-apts", "The member only has " + Ui.Num(g.Balance) + " points to remove.");
                    return response;
                }
                Book.AddPoints(site, g, pts, "adjust", string.Empty, reason, c.U.Id);
                log = g.MemberNo + " · " + (pts > 0 ? "+" : string.Empty) + Ui.Num(pts) + " · " + reason;
                await Store.SaveSite(site, c.Sbx);
                response.SetElementContents("cm-mbody", Card(site, g, c, Ui.StaffMap(await Store.LoadStaff(c.Sbx))));
            }
            await Store.Log(c.Sbx, c.U.Id, "points-adjust", log, true);
            response.ExecuteScript("AdminMembersJs.list(1);AdminMembersJs.toast('Points adjusted');");
            return response;
        }

        public async Task<ApiResponse> AddForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMembersJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "member-add", GetDataValue("amail"), "You can view members but not add them.");
            }
            response.SetElementContents("cm-mbody", "<h2>Add a member</h2><p class=\"cm-hint\" style=\"margin-bottom:12px\">If the email belongs to a past guest, their stays are kept and they become a member. A temporary password is shown once for the guest's first sign-in.</p><div class=\"cm-form\"><div class=\"cm-f2\"><label class=\"cm-field\"><span>First name</span><input type=\"text\" id=\"cm-afirst\" maxlength=\"40\"></label><label class=\"cm-field\"><span>Last name</span><input type=\"text\" id=\"cm-alast\" maxlength=\"40\"></label></div><label class=\"cm-field\"><span>Email</span><input type=\"email\" id=\"cm-amail\" maxlength=\"100\"></label><div class=\"cm-f2\"><label class=\"cm-field\"><span>Phone</span><input type=\"tel\" id=\"cm-aphone\" maxlength=\"30\"></label><label class=\"cm-field\"><span>Country</span><input type=\"text\" id=\"cm-acountry\" maxlength=\"40\"></label></div><em class=\"cm-err\" id=\"cm-e-add\"></em></div><div class=\"cm-mfoot\"><button type=\"button\" class=\"cm-btn cm-btn-ghost\" onclick=\"AdminMembersJs.closeModal()\">Cancel</button><button type=\"button\" class=\"cm-btn cm-btn-accent\" id=\"cm-abtn\" onclick=\"AdminMembersJs.addMember()\">Add member</button></div>");
            response.ExecuteScript("AdminMembersJs.openModal();");
            return response;
        }

        private static string TempPassword()
        {
            const string abc = "abcdefghjkmnpqrstuvwxyz23456789";
            char[] c = new char[10];
            for (int i = 0; i < c.Length; i++)
            {
                c[i] = abc[System.Security.Cryptography.RandomNumberGenerator.GetInt32(abc.Length)];
            }
            return new string(c, 0, 5) + "-" + new string(c, 5, 5);
        }

        public async Task<ApiResponse> AddMember()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMembersJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "member-add", GetDataValue("amail"), "You can view members but not add them.");
            }
            string first = Regex.Replace(Ui.Clip(GetDataValue("afirst"), 40), "\\s+", " ").Trim();
            string last = Regex.Replace(Ui.Clip(GetDataValue("alast"), 40), "\\s+", " ").Trim();
            string email = Ui.Clip(GetDataValue("amail"), 100).Trim().ToLowerInvariant();
            string phone = Ui.CleanPhone(GetDataValue("aphone"));
            string country = Ui.Clip(GetDataValue("acountry"), 40).Trim();
            string err = first.Length < 1 || last.Length < 1 ? "Enter the first and last name." : !Regex.IsMatch(email, "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$") ? "Enter a valid email address." : string.Empty;
            string pass = TempPassword();
            string no = string.Empty;
            string gid = string.Empty;
            bool existing = false;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Guest? g = site.Guests.FirstOrDefault(x => x.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
                if (err == string.Empty && g != null && g.Member)
                {
                    err = Ui.GuestName(g) + " is already a member (" + g.MemberNo + ").";
                }
                if (err == string.Empty)
                {
                    existing = g != null;
                    if (g == null)
                    {
                        g = new Guest { Id = "g" + Book.NewId(), Email = email, Created = DateTime.UtcNow };
                        site.Guests.Add(g);
                    }
                    g.First = first;
                    g.Last = last;
                    if (phone != string.Empty)
                    {
                        g.Phone = phone;
                    }
                    if (country != string.Empty)
                    {
                        g.Country = country;
                    }
                    g.Member = true;
                    g.Joined = DateTime.UtcNow;
                    g.Hash = Access.HashPassword(pass);
                    int next = site.Guests.Where(x => x.MemberNo.StartsWith("SC", StringComparison.Ordinal)).Select(x => int.TryParse(x.MemberNo.Substring(2), out int n) ? n : 0).DefaultIfEmpty(204000).Max() + 37;
                    g.MemberNo = "SC" + next.ToString(Ui.Inv);
                    no = g.MemberNo;
                    gid = g.Id;
                    await Store.SaveSite(site, c.Sbx);
                }
            }
            if (err != string.Empty)
            {
                response.SetElementContents("cm-e-add", err);
                response.ExecuteScript("AdminMembersJs.addFail();");
                return response;
            }
            await Store.Log(c.Sbx, c.U.Id, "member-add", no + " · " + first + " " + last, true);
            response.SetElementContents("cm-mbody", "<h2>Welcome to SkyHotel Circle</h2><p>" + Ui.Enc(first + " " + last) + " is now a member" + (existing ? ", with their past stays kept" : string.Empty) + ". Member no. <b class=\"cm-code\">" + Ui.Enc(no) + "</b></p><div class=\"cm-temp\"><span class=\"cm-hint\">Temporary password, shown only once</span><code>" + Ui.Enc(pass) + "</code><span class=\"cm-hint\">The guest signs in at Membership with " + Ui.Enc(email) + " and can change it under Profile in My Circle.</span></div><div class=\"cm-mfoot\"><button type=\"button\" class=\"cm-btn cm-btn-ghost\" onclick=\"AdminMembersJs.open('" + Ui.Attr(gid) + "')\">Open member</button><button type=\"button\" class=\"cm-btn\" onclick=\"AdminMembersJs.closeModal()\">Done</button></div>");
            response.ExecuteScript("AdminMembersJs.list(1);AdminMembersJs.toast('Member " + Ui.Js(no) + " added');");
            return response;
        }

        public async Task<ApiResponse> SaveMember()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMembersJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "member", GetDataValue("gid"), "You can view members but not edit them.");
            }
            string no;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Guest? g = site.Guests.FirstOrDefault(x => x.Id == GetDataValue("gid") && x.Member);
                if (g == null)
                {
                    return response;
                }
                g.Phone = Ui.Clip(GetDataValue("mphone"), 30);
                g.Country = Ui.Clip(GetDataValue("mcountry"), 40);
                g.Notes = Ui.Clip(GetDataValue("mnotes"), 300);
                g.Vip = GetDataValue("mvip") == "1";
                no = g.MemberNo;
                await Store.SaveSite(site, c.Sbx);
            }
            await Store.Log(c.Sbx, c.U.Id, "member", no, true);
            response.ExecuteScript("AdminMembersJs.list(1);AdminMembersJs.toast('Saved');");
            return response;
        }
    }
}
