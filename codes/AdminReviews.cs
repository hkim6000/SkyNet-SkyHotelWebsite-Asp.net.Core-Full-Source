using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyHotel.Core;
using SkyHotel.Models;
using SkyNet;

namespace SkyHotel.codes
{
    public class AdminReviews : WebPage
    {
        private const string AreaKey = "reviews";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Reviews");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--rw-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("rw-menu", Auth.Menu("rw", "AdminReviews", c, site));
            response.SetElementContents("rw-who", Auth.Who("rw", c));
            response.SetElementContents("rw-demo", Auth.DemoPill("rw", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminReviewsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"rw-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"rw-panel\"><p>You don't have access to this area. Ask the general manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminReviewsJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminReviewsJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("rw-main", NoAccess("Reviews"));
                return response;
            }
            response.SetElementContents("rw-main", await MainHtml(c, site));
            response.ExecuteScript("AdminReviewsJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"rw-tr rw-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private const int Size = 20;

        private static string Chips(SiteData site, string cur)
        {
            (string k, string n)[] items = { ("", "All"), ("pending", "Waiting"), ("approved", "Published"), ("hidden", "Hidden") };
            StringBuilder sb = new StringBuilder("<div class=\"rw-chips\">");
            foreach ((string k, string n) in items)
            {
                sb.Append("<button type=\"button\" class=\"rw-chip" + (k == cur ? " rw-act" : string.Empty) + "\" onclick=\"AdminReviewsJs.chip('" + k + "')\">" + n + "<em>" + (k == string.Empty ? site.Reviews.Count : site.Reviews.Count(r => r.Status == k)) + "</em></button>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static async Task<string> MainHtml(Ctx c, SiteData site)
        {
            List<Review> pub = site.Reviews.Where(r => r.Status == "approved").ToList();
            StringBuilder sb = new StringBuilder("<div class=\"rw-ptop\"><div><h1>Reviews</h1><p>Guests can review a stay after check-out from Manage a booking. Published reviews show on the home page" + (pub.Count > 0 ? " &middot; average " + pub.Average(r => r.Rating).ToString("0.0", Ui.Inv) + " from " + pub.Count : string.Empty) + ".</p></div></div>");
            sb.Append("<div id=\"rw-rchips\" style=\"margin-bottom:14px\">" + Chips(site, "pending") + "</div><div class=\"rw-bar\"><input type=\"hidden\" id=\"rw-rst\" value=\"pending\"><label>Search<input type=\"search\" id=\"rw-q\" list=\"rw-qlist\" placeholder=\"Guest, title or text\" maxlength=\"60\" oninput=\"AdminReviewsJs.search()\"></label>" + Ui.DataList("rw-qlist", site.Reviews.Select(r => r.Name)) + "<label>Rating<select id=\"rw-rstar\" onchange=\"AdminReviewsJs.list(1)\">" + Ui.Opts(new[] { ("", "Any rating"), ("5", "5 stars"), ("4", "4 stars"), ("3", "3 stars"), ("2", "2 stars"), ("1", "1 star") }, string.Empty) + "</select></label></div><div class=\"rw-bar\" id=\"rw-perbar\">" + Ui.PeriodBar("rw", "AdminReviewsJs.per", "all", string.Empty, string.Empty) + "</div><div class=\"rw-tbl rw-rwtbl\" id=\"rw-list\"></div><div id=\"rw-pager\"></div>");
            return sb.ToString();
        }

        private string Rows(SiteData site, Ctx c, out string pager, out string per)
        {
            string st = GetDataValue("rst") is "pending" or "approved" or "hidden" ? GetDataValue("rst") : string.Empty;
            int star = int.TryParse(GetDataValue("rstar"), out int s) ? s : 0;
            string term = Ui.Clip(GetDataValue("term"), 60);
            per = Per(GetDataValue("per"));
            (DateTime? from, DateTime? to) = Ui.Period(per, GetDataValue("pfrom"), GetDataValue("pto"));
            List<Review> all = site.Reviews.Where(r => (st == string.Empty || r.Status == st) && (star == 0 || r.Rating == star) && Ui.InPeriod(r.When, from, to) && (term == string.Empty || r.Name.Contains(term, StringComparison.OrdinalIgnoreCase) || r.Title.Contains(term, StringComparison.OrdinalIgnoreCase) || r.Text.Contains(term, StringComparison.OrdinalIgnoreCase))).OrderByDescending(r => r.When).ToList();
            int page = Ui.PageOf(GetDataValue("pg"), Size, all.Count);
            bool edit = Access.Can(c.U, AreaKey, 'E');
            StringBuilder sb = new StringBuilder(Head(new[] { ("Date", string.Empty), ("Guest", string.Empty), ("Rating", string.Empty), ("Review", string.Empty), ("Status", string.Empty), (string.Empty, string.Empty) }));
            if (all.Count == 0)
            {
                sb.Append("<div class=\"rw-tr\"><span class=\"rw-none rw-wide\">No reviews match.</span></div>");
            }
            foreach (Review r in all.Skip((page - 1) * Size).Take(Size))
            {
                sb.Append("<div class=\"rw-tr\"><span data-l=\"Date\">" + r.When.ToString("d MMM yyyy", Ui.Inv) + "</span><span data-l=\"Guest\"><b>" + Ui.Enc(r.Name) + "</b><small class=\"rw-code\">" + Ui.Enc(r.Booking) + "</small></span><span data-l=\"Rating\" class=\"rw-rstars\">" + new string('★', r.Rating) + "</span><span data-l=\"Review\" class=\"rw-wide\"><b>" + Ui.Enc(r.Title) + "</b><small style=\"white-space:normal\">" + Ui.Enc(r.Text) + "</small>" + (r.Reply != string.Empty ? "<small style=\"white-space:normal\">&#8627; " + Ui.Enc(r.Reply) + "</small>" : string.Empty) + "</span><span data-l=\"Status\"><span class=\"rw-st rw-st-" + r.Status + "\">" + (r.Status == "approved" ? "Published" : r.Status == "pending" ? "Waiting" : "Hidden") + "</span></span><span class=\"rw-rwbtns\">");
                if (edit)
                {
                    sb.Append((r.Status != "approved" ? "<button type=\"button\" class=\"rw-btn rw-btn-sm\" onclick=\"AdminReviewsJs.act('" + r.Id + "','approve'," + page + ")\">Publish</button>" : "<button type=\"button\" class=\"rw-btn rw-btn-sm rw-btn-ghost\" onclick=\"AdminReviewsJs.act('" + r.Id + "','hide'," + page + ")\">Hide</button>") + "<button type=\"button\" class=\"rw-btn rw-btn-sm rw-btn-ghost\" onclick=\"AdminReviewsJs.act('" + r.Id + "','reply'," + page + ")\">Reply</button><button type=\"button\" class=\"rw-btn rw-btn-sm rw-btn-danger\" onclick=\"AdminReviewsJs.act('" + r.Id + "','delete'," + page + ")\">Delete</button>");
                }
                sb.Append("</span></div>");
            }
            pager = Ui.Pager("rw", page, Size, all.Count, "review", "reviews", "AdminReviewsJs.list");
            return sb.ToString();
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminReviewsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            response.SetElementContents("rw-list", Rows(site, c, out string pager, out string per));
            response.SetElementContents("rw-pager", pager);
            response.SetElementContents("rw-rchips", Chips(site, GetDataValue("rst")));
            response.SetElementContents("rw-perbar", Ui.PeriodBar("rw", "AdminReviewsJs.per", per, GetDataValue("pfrom"), GetDataValue("pto")));
            return response;
        }

        public async Task<ApiResponse> Act()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminReviewsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "review", GetDataValue("rid"), "You can read reviews but not moderate them.");
            }
            string act = GetDataValue("ract");
            string id = Ui.Clip(GetDataValue("rid"), 40);
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Review? r = site.Reviews.FirstOrDefault(x => x.Id == id);
                if (r == null)
                {
                    return response;
                }
                switch (act)
                {
                    case "approve":
                        r.Status = "approved";
                        break;
                    case "hide":
                        r.Status = "hidden";
                        break;
                    case "reply":
                        r.Reply = Ui.Clip(GetDataValue("rtext"), 400);
                        break;
                    case "delete":
                        site.Reviews.Remove(r);
                        break;
                    default:
                        return response;
                }
                await Store.SaveSite(site, c.Sbx);
                response.SetElementContents("rw-list", Rows(site, c, out string pager, out string per));
                response.SetElementContents("rw-pager", pager);
                response.SetElementContents("rw-rchips", Chips(site, GetDataValue("rst")));
                Chrome(response, c, site);
            }
            await Store.Log(c.Sbx, c.U.Id, "review-" + act, id, true);
            response.ExecuteScript("AdminReviewsJs.toast('Done');");
            return response;
        }
    }
}
