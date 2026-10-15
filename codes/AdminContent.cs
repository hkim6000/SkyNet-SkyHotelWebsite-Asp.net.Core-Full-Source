using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyHotel.Core;
using SkyHotel.Models;
using SkyNet;

namespace SkyHotel.codes
{
    public class AdminContent : WebPage
    {
        private const string AreaKey = "content";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Content");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--ce-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("ce-menu", Auth.Menu("ce", "AdminContent", c, site));
            response.SetElementContents("ce-who", Auth.Who("ce", c));
            response.SetElementContents("ce-demo", Auth.DemoPill("ce", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminContentJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"ce-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"ce-panel\"><p>You don't have access to this area. Ask the general manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminContentJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminContentJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("ce-main", NoAccess("Content"));
                return response;
            }
            response.SetElementContents("ce-main", await MainHtml(c, site));
            response.ExecuteScript("AdminContentJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"ce-tr ce-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static readonly (string Key, string Name, string Where)[] Blocks =
        {
            ("home", "Home hero", "Top of the home page"),
            ("intro", "Welcome", "Home page, under the booking bar"),
            ("dining", "Dining", "Dining page"),
            ("spa", "Spa & wellness", "Spa page"),
            ("location", "Location", "Contact page and home"),
            ("membership", "Membership", "Membership page and home"),
            ("gallery", "Gallery", "Photos and videos on the Gallery page")
        };

        private static async Task<string> MainHtml(Ctx c, SiteData site)
        {
            StringBuilder sb = new StringBuilder("<div class=\"ce-ptop\"><div><h1>Content</h1><p>Words and pictures on the public pages. Changes show on the website straight away.</p></div><a class=\"ce-btn ce-btn-ghost\" href=\"Home\" target=\"_blank\" rel=\"noopener\">Open the website</a></div><div class=\"ce-cegrid\"><div class=\"ce-celist\">");
            foreach ((string k, string n, string w) in Blocks)
            {
                sb.Append("<button type=\"button\" data-k=\"" + k + "\" class=\"" + (k == "home" ? "ce-act" : string.Empty) + "\" onclick=\"AdminContentJs.show('" + k + "')\">" + n + "<small>" + w + "</small></button>");
            }
            sb.Append("</div><section class=\"ce-panel\" id=\"ce-ceform\"><p class=\"ce-loading\">Loading&hellip;</p></section></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Block()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminContentJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            string key = Blocks.Any(b => b.Key == GetDataValue("ckey")) ? GetDataValue("ckey") : "home";
            bool edit = Access.Can(c.U, AreaKey, 'E');
            string ro = edit ? string.Empty : " readonly";
            StringBuilder sb = new StringBuilder();
            if (key == "gallery")
            {
                sb.Append("<h2>Gallery</h2><p class=\"ce-hint\" style=\"margin-bottom:12px\">Tick the photos and videos to show on the Gallery page. Add new ones in <a href=\"AdminMedia\">Media library</a>.</p>" + Ui.Picker("ce", site, "ce-gal", string.Empty, site.Gallery, true, !edit));
                sb.Append((edit ? "<div class=\"ce-mfoot\"><button type=\"button\" class=\"ce-btn ce-btn-accent\" onclick=\"AdminContentJs.saveGallery()\">Save gallery</button></div>" : string.Empty));
            }
            else
            {
                ContentBlock b = site.Content.TryGetValue(key, out ContentBlock? x) && x != null ? x : new ContentBlock();
                sb.Append("<h2>" + Ui.Enc(Blocks.First(k => k.Key == key).Name) + "</h2><div class=\"ce-form\"><label class=\"ce-field\"><span>Title</span><input type=\"text\" id=\"ce-ctitle\" maxlength=\"80\" value=\"" + Ui.Attr(b.Title) + "\"" + ro + "></label><label class=\"ce-field\"><span>Text</span><textarea id=\"ce-ctext\" rows=\"3\" maxlength=\"400\"" + ro + ">" + Ui.Enc(b.Text) + "</textarea></label>");
                sb.Append("<div><span class=\"ce-hint\">Picture</span>" + Ui.Picker("ce", site, "ce-cimg", "photo", new[] { b.Image }, false, !edit) + "</div>");
                if (key == "home")
                {
                    sb.Append("<div><span class=\"ce-hint\">Background video (plays muted on a loop; the picture shows while it loads and for visitors who prefer less motion)</span>" + Ui.Picker("ce", site, "ce-cvid", "video", new[] { b.Video }, false, !edit, true) + "</div>");
                }
                if (key is "intro" or "dining" or "spa" or "location")
                {
                    sb.Append("<div><span class=\"ce-hint\">" + (key == "intro" ? "Numbers under the welcome text" : key == "spa" ? "Treatments and facilities" : key == "dining" ? "Restaurants and bars" : "Ways to get here") + "</span><div class=\"ce-items\" id=\"ce-items\">");
                    foreach (ContentItem it in b.Items)
                    {
                        sb.Append("<div class=\"ce-item\"><label class=\"ce-field\"><span>Name</span><input type=\"text\" maxlength=\"60\" data-f=\"name\" value=\"" + Ui.Attr(it.Name) + "\"" + ro + "></label><label class=\"ce-field\"><span>Detail</span><input type=\"text\" maxlength=\"60\" data-f=\"meta\" value=\"" + Ui.Attr(it.Meta) + "\"" + ro + "></label><label class=\"ce-field\"><span>Text</span><input type=\"text\" maxlength=\"200\" data-f=\"text\" value=\"" + Ui.Attr(it.Text) + "\"" + ro + "></label>" + (edit ? "<button type=\"button\" class=\"ce-btn ce-btn-sm ce-btn-danger\" onclick=\"this.parentNode.remove()\">Remove</button>" : "<span></span>") + "</div>");
                    }
                    sb.Append("</div>" + (edit ? "<button type=\"button\" class=\"ce-btn ce-btn-sm ce-btn-ghost\" style=\"margin-top:8px\" onclick=\"AdminContentJs.addItem()\">Add a line</button>" : string.Empty) + "</div>");
                }
                else
                {
                    sb.Append("<div id=\"ce-items\" hidden></div>");
                }
                sb.Append("<em class=\"ce-err\" id=\"ce-e-ce\"></em></div>" + (edit ? "<div class=\"ce-mfoot\"><button type=\"button\" class=\"ce-btn ce-btn-accent\" onclick=\"AdminContentJs.save()\">Save</button></div>" : "<p class=\"ce-hint\" style=\"margin-top:12px\">You can view content but not change it.</p>"));
            }
            response.SetElementContents("ce-ceform", sb.ToString());
            return response;
        }

        private sealed class ItemIn
        {
            public string Name { get; set; } = string.Empty;
            public string Meta { get; set; } = string.Empty;
            public string Text { get; set; } = string.Empty;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminContentJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "content", GetDataValue("ckey"), "You can view content but not change it.");
            }
            string key = Blocks.Any(b => b.Key == GetDataValue("ckey") && b.Key != "gallery") ? GetDataValue("ckey") : string.Empty;
            string title = Ui.Clip(GetDataValue("ctitle"), 80);
            if (key == string.Empty || title.Length < 2)
            {
                response.SetElementContents("ce-e-ce", "Give it a title.");
                return response;
            }
            List<ItemIn> items = new List<ItemIn>();
            try
            {
                items = System.Text.Json.JsonSerializer.Deserialize<List<ItemIn>>(GetDataValue("citems"), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ItemIn>();
            }
            catch (System.Text.Json.JsonException)
            {
                items = new List<ItemIn>();
            }
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                if (!site.Content.TryGetValue(key, out ContentBlock? b) || b == null)
                {
                    b = new ContentBlock();
                    site.Content[key] = b;
                }
                b.Title = title;
                b.Text = Ui.Clip(GetDataValue("ctext"), 400);
                if (Access.SafeImage(GetDataValue("cimg")) && site.Media.Any(m => m.Full == GetDataValue("cimg") && m.Kind == "photo"))
                {
                    b.Image = GetDataValue("cimg");
                }
                if (key == "home")
                {
                    string vid = GetDataValue("cvid");
                    b.Video = vid == string.Empty ? string.Empty : Access.SafeVideo(vid) && site.Media.Any(m => m.Full == vid && m.Kind == "video") ? vid : b.Video;
                }
                if (key is "intro" or "dining" or "spa" or "location")
                {
                    b.Items = items.Where(i => (i.Name ?? string.Empty).Trim() != string.Empty).Take(8).Select(i => new ContentItem { Name = Ui.Clip(i.Name, 60), Meta = Ui.Clip(i.Meta, 60), Text = Ui.Clip(i.Text, 200) }).ToList();
                }
                await Store.SaveSite(site, c.Sbx);
            }
            await Store.Log(c.Sbx, c.U.Id, "content", key, true);
            response.ExecuteScript("AdminContentJs.toast('Saved. The website is updated.');");
            return response;
        }

        public async Task<ApiResponse> SaveGallery()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminContentJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "content", "gallery", "You can view content but not change it.");
            }
            List<string> pics;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                HashSet<string> lib = new HashSet<string>(site.Media.Select(m => m.Full));
                pics = GetDataValue("cgal").Split('|').Where(x => lib.Contains(x)).Distinct().Take(60).ToList();
                if (pics.Count >= 3)
                {
                    site.Gallery = pics;
                    await Store.SaveSite(site, c.Sbx);
                }
            }
            if (pics.Count < 3)
            {
                response.ExecuteScript("AdminContentJs.toast('Choose at least three items.');");
                return response;
            }
            await Store.Log(c.Sbx, c.U.Id, "content", "gallery · " + pics.Count + " items", true);
            response.ExecuteScript("AdminContentJs.toast('Gallery saved');");
            return response;
        }
    }
}
