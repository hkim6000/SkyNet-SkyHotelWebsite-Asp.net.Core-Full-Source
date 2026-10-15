using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyHotel.Core;
using SkyHotel.Models;
using SkyNet;

namespace SkyHotel.codes
{
    public class AdminMedia : WebPage
    {
        private const string AreaKey = "media";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Media library");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--ml-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("ml-menu", Auth.Menu("ml", "AdminMedia", c, site));
            response.SetElementContents("ml-who", Auth.Who("ml", c));
            response.SetElementContents("ml-demo", Auth.DemoPill("ml", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminMediaJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"ml-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"ml-panel\"><p>You don't have access to this area. Ask the general manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminMediaJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMediaJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("ml-main", NoAccess("Media library"));
                return response;
            }
            response.SetElementContents("ml-main", await MainHtml(c, site));
            response.ExecuteScript("AdminMediaJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"ml-tr ml-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private const int Size = 24;

        private const int ChunkSize = 2 * 1024 * 1024;

        private static long MaxVideo => Store.Demo ? 20L * 1024 * 1024 : 50L * 1024 * 1024;

        private static async Task<string> MainHtml(Ctx c, SiteData site)
        {
            Dictionary<string, Staff> staff = Ui.StaffMap(await Store.LoadStaff(c.Sbx));
            StringBuilder sb = new StringBuilder("<div class=\"ml-ptop\"><div><h1>Media library</h1><p>Photos and videos for the website. Rooms, Offers and Content pick from this library.</p></div></div>");
            if (Access.Can(c.U, AreaKey, 'E'))
            {
                string demo = Store.Demo ? "<p class=\"ml-hint\">Demo: uploads stay in your private copy and are removed after an hour.</p>" : string.Empty;
                sb.Append("<details class=\"ml-panel ml-col\"><summary><b>Upload a photo</b><span>JPG, PNG or WebP &middot; resized to 1600 px</span></summary><div class=\"ml-colb\"><div class=\"ml-up\"><label class=\"ml-drop\" id=\"ml-prev\" for=\"ml-file\"><span>Choose a photo<br>JPG, PNG or WebP</span><input type=\"file\" id=\"ml-file\" accept=\"image/jpeg,image/png,image/webp\" onchange=\"AdminMediaJs.file(this)\"></label><div class=\"ml-form\"><label class=\"ml-field\"><span>Title</span><input type=\"text\" id=\"ml-mtitle\" maxlength=\"80\" placeholder=\"e.g. Harbour View Room at night\"></label><em class=\"ml-err\" id=\"ml-e-mtitle\"></em><em class=\"ml-err\" id=\"ml-e-file\"></em><p class=\"ml-hint\">The photo is resized in your browser to 1600 px, with a 640 px preview for lists.</p>" + demo + "<div><button type=\"button\" class=\"ml-btn ml-btn-accent\" id=\"ml-pbtn\" onclick=\"AdminMediaJs.upload()\">Upload photo</button></div></div></div></div></details>");
                sb.Append("<details class=\"ml-panel ml-col\"><summary><b>Upload a video</b><span>MP4 or WebM &middot; up to " + (MaxVideo / 1048576) + " MB</span></summary><div class=\"ml-colb\"><div class=\"ml-up\"><label class=\"ml-drop\" id=\"ml-vprev\" for=\"ml-vfile\"><span>Choose a video<br>MP4 or WebM</span><input type=\"file\" id=\"ml-vfile\" accept=\"video/mp4,video/webm\" data-max=\"" + MaxVideo + "\" onchange=\"AdminMediaJs.vpick(this)\"></label><div class=\"ml-form\"><label class=\"ml-field\"><span>Title</span><input type=\"text\" id=\"ml-vtitle\" maxlength=\"80\" placeholder=\"e.g. Spa morning\"></label><em class=\"ml-err\" id=\"ml-e-vtitle\"></em><em class=\"ml-err\" id=\"ml-e-vfile\"></em><p class=\"ml-hint\">Sent in 2 MB parts. The poster image is taken from the first second. Short, silent clips work best for the home page.</p>" + demo + "<div class=\"ml-prog\" id=\"ml-vprog\"><i id=\"ml-vbar\"></i></div><div><button type=\"button\" class=\"ml-btn ml-btn-accent\" id=\"ml-vbtn\" onclick=\"AdminMediaJs.vupload()\">Upload video</button></div></div></div></div></details>");
            }
            else
            {
                sb.Append("<div class=\"ml-info-box\">You can browse the library. Uploading needs Edit rights on Media.</div>");
            }
            IEnumerable<string> by = site.Media.Select(m => m.By).Distinct();
            sb.Append("<div class=\"ml-mtop\"><div id=\"ml-mchips\">" + Chips(site, string.Empty) + "</div></div><div class=\"ml-bar\"><label>Search<input type=\"search\" id=\"ml-q\" list=\"ml-qlist\" placeholder=\"Title\" maxlength=\"60\" oninput=\"AdminMediaJs.search()\"></label>" + Ui.DataList("ml-qlist", site.Media.Select(m => m.Title)) + "<label>Added by<select id=\"ml-mby\" onchange=\"AdminMediaJs.list(1)\">" + Ui.Opts(new[] { (string.Empty, "Anyone") }.Concat(by.Select(id => (id, Who(staff, id))).OrderBy(x => x.Item2)), string.Empty) + "</select></label></div><div class=\"ml-bar\" id=\"ml-perbar\">" + Ui.PeriodBar("ml", "AdminMediaJs.per", "all", string.Empty, string.Empty) + "</div><div class=\"ml-mgrid\" id=\"ml-mgrid\"></div><div id=\"ml-pager\"></div>");
            return sb.ToString();
        }

        private static string Chips(SiteData site, string cur)
        {
            (string k, string n)[] items = { ("", "All"), ("photo", "Photos"), ("video", "Videos") };
            StringBuilder sb = new StringBuilder("<div class=\"ml-chips\">");
            foreach ((string k, string n) in items)
            {
                sb.Append("<button type=\"button\" class=\"ml-chip" + (k == cur ? " ml-act" : string.Empty) + "\" onclick=\"AdminMediaJs.setKind('" + k + "')\">" + n + "<em>" + (k == string.Empty ? site.Media.Count : site.Media.Count(m => m.Kind == k)) + "</em></button>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string BlockName(string key)
        {
            return key switch
            {
                "home" => "Home hero",
                "intro" => "Home welcome",
                "dining" => "Dining page",
                "spa" => "Spa page",
                "location" => "Location",
                "membership" => "Membership page",
                _ => key
            };
        }

        private static List<string> Uses(SiteData site, string src)
        {
            List<string> u = new List<string>();
            foreach (KeyValuePair<string, ContentBlock> kv in site.Content)
            {
                if (kv.Value.Image == src || kv.Value.Video == src)
                {
                    u.Add(BlockName(kv.Key));
                }
            }
            if (site.Gallery.Contains(src))
            {
                u.Add("Gallery");
            }
            foreach (RoomType t in site.Types)
            {
                if (t.Images.Contains(src) || t.Video == src)
                {
                    u.Add(t.Name);
                }
            }
            foreach (Offer o in site.Offers)
            {
                if (o.Image == src)
                {
                    u.Add("Offer " + o.Code);
                }
            }
            return u;
        }

        private async Task<(string Grid, string Pager, string Chips, string Per)> Grid(Ctx c, SiteData site)
        {
            Dictionary<string, Staff> staff = Ui.StaffMap(await Store.LoadStaff(c.Sbx));
            string kind = GetDataValue("mkind") is "photo" or "video" ? GetDataValue("mkind") : string.Empty;
            string term = Ui.Clip(GetDataValue("term"), 60).Trim();
            string by = Ui.Clip(GetDataValue("mby"), 40);
            string per = Per(GetDataValue("per"));
            (DateTime? from, DateTime? to) = Ui.Period(per, GetDataValue("pfrom"), GetDataValue("pto"));
            List<MediaItem> all = site.Media.Where(m => (kind == string.Empty || m.Kind == kind) && (by == string.Empty || m.By == by) && Ui.InPeriod(m.When, from, to) && (term == string.Empty || m.Title.Contains(term, StringComparison.OrdinalIgnoreCase))).OrderByDescending(m => m.When).ToList();
            bool edit = Access.Can(c.U, AreaKey, 'E');
            int page = Ui.PageOf(GetDataValue("pg"), Size, all.Count);
            StringBuilder sb = new StringBuilder();
            if (all.Count == 0)
            {
                sb.Append("<p class=\"ml-none\">Nothing matches.</p>");
            }
            foreach (MediaItem m in all.Skip((page - 1) * Size).Take(Size))
            {
                bool vid = m.Kind == "video";
                int used = Uses(site, m.Full).Count;
                sb.Append("<div class=\"ml-mi\"><button type=\"button\" class=\"ml-mthumb\" onclick=\"AdminMediaJs.view('" + Ui.Attr(m.Id) + "')\" aria-label=\"Open " + Ui.Attr(m.Title) + "\"><img src=\"" + Ui.Attr(m.Thumb) + "\" alt=\"\" loading=\"lazy\">" + (vid ? "<span class=\"ml-play\"></span><em class=\"ml-dur\">" + Ui.Clock(m.Seconds) + "</em>" : string.Empty) + "</button><div class=\"ml-mtxt\"><b>" + Ui.Enc(m.Title) + "</b><small>" + (vid ? "Video" : "Photo") + " &middot; " + Ui.Enc(Who(staff, m.By)) + " &middot; " + m.When.ToString("d MMM yyyy", Ui.Inv) + "</small>" + (used > 0 ? "<span class=\"ml-tag ml-tag-use\">Used &times;" + used + "</span>" : "<span class=\"ml-tag\">Not used</span>") + (m.Builtin ? "<span class=\"ml-tag\">Built-in</span>" : string.Empty) + "</div>");
                if (edit)
                {
                    sb.Append("<footer><button type=\"button\" class=\"ml-btn ml-btn-sm ml-btn-ghost\" onclick=\"AdminMediaJs.rename('" + Ui.Attr(m.Id) + "','" + Ui.Attr(Ui.Js(m.Title)) + "')\">Rename</button>" + (m.Builtin ? string.Empty : "<button type=\"button\" class=\"ml-btn ml-btn-sm ml-btn-danger\" onclick=\"AdminMediaJs.del('" + Ui.Attr(m.Id) + "','" + Ui.Attr(Ui.Js(m.Title)) + "')\">Delete</button>") + "</footer>");
                }
                sb.Append("</div>");
            }
            return (sb.ToString(), Ui.Pager("ml", page, Size, all.Count, "item", "items", "AdminMediaJs.list"), Chips(site, kind), Ui.PeriodBar("ml", "AdminMediaJs.per", per, GetDataValue("pfrom"), GetDataValue("pto")));
        }

        private async Task Redraw(ApiResponse response, Ctx c, SiteData site)
        {
            (string grid, string pager, string chips, string per) = await Grid(c, site);
            response.SetElementContents("ml-mgrid", grid);
            response.SetElementContents("ml-pager", pager);
            response.SetElementContents("ml-mchips", chips);
            response.SetElementContents("ml-perbar", per);
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMediaJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            await Redraw(response, c, await Store.LoadSite(c.Sbx));
            return response;
        }

        public async Task<ApiResponse> Open()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMediaJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            MediaItem? m = site.Media.FirstOrDefault(x => x.Id == Ui.Clip(GetDataValue("mid"), 60));
            if (m == null)
            {
                return response;
            }
            Dictionary<string, Staff> staff = Ui.StaffMap(await Store.LoadStaff(c.Sbx));
            bool vid = m.Kind == "video";
            bool edit = Access.Can(c.U, AreaKey, 'E');
            List<string> uses = Uses(site, m.Full);
            StringBuilder sb = new StringBuilder("<h2>" + Ui.Enc(m.Title) + "</h2><div class=\"ml-pv\">" + (vid ? "<video controls autoplay playsinline poster=\"" + Ui.Attr(m.Thumb) + "\" src=\"" + Ui.Attr(m.Full) + "\"></video>" : "<img src=\"" + Ui.Attr(m.Full) + "\" alt=\"" + Ui.Attr(m.Title) + "\">") + "</div><dl class=\"ml-pvmeta\"><dt>Type</dt><dd>" + (vid ? "Video &middot; " + Ui.Clock(m.Seconds) : "Photo") + (m.Builtin ? " &middot; built-in" : string.Empty) + "</dd><dt>Added</dt><dd>" + Ui.Enc(Who(staff, m.By)) + " &middot; " + m.When.ToString("d MMM yyyy", Ui.Inv) + "</dd><dt>Used on</dt><dd>" + (uses.Count > 0 ? Ui.Enc(string.Join(", ", uses)) : "Not used yet") + "</dd><dt>File</dt><dd class=\"ml-code\">" + Ui.Enc(m.Full) + "</dd></dl><div class=\"ml-mfoot\">");
            if (edit)
            {
                sb.Append((m.Builtin ? string.Empty : "<button type=\"button\" class=\"ml-btn ml-btn-danger\" onclick=\"AdminMediaJs.del('" + Ui.Attr(m.Id) + "','" + Ui.Attr(Ui.Js(m.Title)) + "')\">Delete</button>") + "<span class=\"ml-grow\"></span><button type=\"button\" class=\"ml-btn ml-btn-ghost\" onclick=\"AdminMediaJs.stopPreview();AdminMediaJs.closeModal();AdminMediaJs.rename('" + Ui.Attr(m.Id) + "','" + Ui.Attr(Ui.Js(m.Title)) + "')\">Rename</button>");
            }
            sb.Append("<button type=\"button\" class=\"ml-btn\" onclick=\"AdminMediaJs.stopPreview();AdminMediaJs.closeModal()\">Close</button></div>");
            response.SetElementContents("ml-mbody", sb.ToString());
            response.ExecuteScript("AdminMediaJs.openModal();");
            return response;
        }

        private static string ImageExt(byte[] b)
        {
            if (b.Length > 12 && b[0] == 0xFF && b[1] == 0xD8)
            {
                return "jpg";
            }
            if (b.Length > 12 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47)
            {
                return "png";
            }
            if (b.Length > 12 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50)
            {
                return "webp";
            }
            return string.Empty;
        }

        private static byte[]? DataImage(string data, int max)
        {
            Match m = Regex.Match(data ?? string.Empty, "^data:image/(jpeg|png|webp);base64,([A-Za-z0-9+/=]+)$");
            if (!m.Success)
            {
                return null;
            }
            try
            {
                byte[] b = Convert.FromBase64String(m.Groups[2].Value);
                return b.Length <= max && ImageExt(b) != string.Empty ? b : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private static string Folder(Ctx c)
        {
            return c.Sbx != string.Empty ? "sandbox/" + c.Sbx : "live";
        }

        private static string Dir(Ctx c)
        {
            return Path.Combine(Store.Root, "images", "uploads", Folder(c).Replace('/', Path.DirectorySeparatorChar));
        }

        private static bool OverQuota(Ctx c, long incoming)
        {
            if (c.Sbx == string.Empty || !Directory.Exists(Dir(c)))
            {
                return false;
            }
            FileInfo[] files = new DirectoryInfo(Dir(c)).GetFiles();
            return files.Length >= 40 || files.Sum(f => f.Length) + incoming > 80L * 1024 * 1024;
        }

        private static string CleanTitle(string value)
        {
            return Regex.Replace(Ui.Clip(value, 80), "\\s+", " ").Trim();
        }

        public async Task<ApiResponse> Upload()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMediaJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                response.ExecuteScript("AdminMediaJs.upFail('Uploading needs Edit rights on Media.');");
                return await Refuse(response, c, "upload", "photo", "Uploading needs Edit rights on Media.");
            }
            string title = CleanTitle(GetDataValue("mtitle"));
            if (title.Length < 2)
            {
                response.SetElementContents("ml-e-mtitle", "Give the photo a title.");
                response.ExecuteScript("AdminMediaJs.upFail('');");
                return response;
            }
            byte[]? big = DataImage(GetDataValue("full"), 8 * 1024 * 1024);
            byte[]? small = DataImage(GetDataValue("thumb"), 3 * 1024 * 1024);
            if (big == null || small == null || ImageExt(big) != ImageExt(small))
            {
                response.ExecuteScript("AdminMediaJs.upFail('That file is not a JPG, PNG or WebP image.');");
                return response;
            }
            if (OverQuota(c, big.Length + small.Length))
            {
                response.ExecuteScript("AdminMediaJs.upFail('Demo limit reached for this copy.');");
                return response;
            }
            Directory.CreateDirectory(Dir(c));
            string id = Guid.NewGuid().ToString("N").Substring(0, 16);
            string ext = ImageExt(big);
            await File.WriteAllBytesAsync(Path.Combine(Dir(c), id + "." + ext), big);
            await File.WriteAllBytesAsync(Path.Combine(Dir(c), id + "-t." + ext), small);
            string url = "images/uploads/" + Folder(c) + "/" + id;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                site.Media.Add(new MediaItem { Id = id, Kind = "photo", Full = url + "." + ext, Thumb = url + "-t." + ext, Title = title, By = c.U.Id, When = DateTime.UtcNow });
                await Store.SaveSite(site, c.Sbx);
            }
            await Store.Log(c.Sbx, c.U.Id, "upload", "photo · " + title, true);
            response.ExecuteScript("AdminMediaJs.uploaded();AdminMediaJs.toast('Photo added to the library');");
            return response;
        }

        private static string PartFile(Ctx c, string uid)
        {
            return Path.Combine(Store.Root, "temp", "uploads", (c.Sbx != string.Empty ? c.Sbx : "live") + "-" + uid + ".part");
        }

        public async Task<ApiResponse> Chunk()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMediaJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                response.ExecuteScript("AdminMediaJs.chunkFail('Uploading needs Edit rights on Media.');");
                return await Refuse(response, c, "upload", "video", "Uploading needs Edit rights on Media.");
            }
            string uid = Store.CleanId(GetDataValue("uid"));
            int idx = int.TryParse(GetDataValue("idx"), out int i) ? i : -1;
            int total = int.TryParse(GetDataValue("total"), out int t) ? t : 0;
            if (uid == string.Empty || idx < 0 || total < 1 || idx >= total || (long)total * ChunkSize > MaxVideo + ChunkSize)
            {
                response.ExecuteScript("AdminMediaJs.chunkFail('The video must be under " + (MaxVideo / 1048576) + " MB.');");
                return response;
            }
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(GetDataValue("part"));
            }
            catch (FormatException)
            {
                response.ExecuteScript("AdminMediaJs.chunkFail('The upload was damaged. Please try again.');");
                return response;
            }
            string file = PartFile(c, uid);
            string dir = Path.GetDirectoryName(file) ?? Store.Root;
            Directory.CreateDirectory(dir);
            if (idx == 0)
            {
                foreach (string old in Directory.GetFiles(dir, "*.part"))
                {
                    if (File.GetLastWriteTimeUtc(old) < DateTime.UtcNow.AddHours(-2))
                    {
                        File.Delete(old);
                    }
                }
                if (OverQuota(c, (long)total * ChunkSize))
                {
                    response.ExecuteScript("AdminMediaJs.chunkFail('Demo limit reached for this copy.');");
                    return response;
                }
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            long have = File.Exists(file) ? new FileInfo(file).Length : 0;
            if (have != (long)idx * ChunkSize || bytes.Length == 0 || bytes.Length > ChunkSize || (idx < total - 1 && bytes.Length != ChunkSize) || have + bytes.Length > MaxVideo)
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
                response.ExecuteScript("AdminMediaJs.chunkFail('" + (have + bytes.Length > MaxVideo ? "The video must be under " + (MaxVideo / 1048576) + " MB." : "The upload was interrupted. Please try again.") + "');");
                return response;
            }
            using (FileStream fs = new FileStream(file, FileMode.Append, FileAccess.Write))
            {
                await fs.WriteAsync(bytes);
            }
            response.ExecuteScript("AdminMediaJs.chunkOk(" + idx + ");");
            return response;
        }

        public async Task<ApiResponse> VideoDone()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMediaJs.expired();");
                return response;
            }
            string uid = Store.CleanId(GetDataValue("uid"));
            string file = uid != string.Empty ? PartFile(c, uid) : string.Empty;
            if (!Access.Can(c.U, AreaKey, 'E') || file == string.Empty || !File.Exists(file))
            {
                response.ExecuteScript("AdminMediaJs.chunkFail('The upload was interrupted. Please try again.');");
                return response;
            }
            string title = CleanTitle(GetDataValue("vtitle"));
            if (title.Length < 2)
            {
                File.Delete(file);
                response.ExecuteScript("AdminMediaJs.chunkFail('Give the video a title.');");
                return response;
            }
            byte[] head = new byte[12];
            using (FileStream fs = File.OpenRead(file))
            {
                int n = await fs.ReadAsync(head.AsMemory(0, 12));
                if (n < 12)
                {
                    head = Array.Empty<byte>();
                }
            }
            string ext = head.Length == 12 && head[4] == 0x66 && head[5] == 0x74 && head[6] == 0x79 && head[7] == 0x70 ? "mp4" : head.Length == 12 && head[0] == 0x1A && head[1] == 0x45 && head[2] == 0xDF && head[3] == 0xA3 ? "webm" : string.Empty;
            if (ext == string.Empty)
            {
                File.Delete(file);
                response.ExecuteScript("AdminMediaJs.chunkFail('That file is not an MP4 or WebM video.');");
                return response;
            }
            Directory.CreateDirectory(Dir(c));
            string id = Guid.NewGuid().ToString("N").Substring(0, 16);
            File.Move(file, Path.Combine(Dir(c), id + "." + ext));
            string url = "images/uploads/" + Folder(c) + "/" + id;
            string poster = "images/hotel/thumbs/exterior-dusk.webp";
            byte[]? still = DataImage(GetDataValue("poster"), 3 * 1024 * 1024);
            if (still != null)
            {
                string pe = ImageExt(still);
                await File.WriteAllBytesAsync(Path.Combine(Dir(c), id + "-p." + pe), still);
                poster = url + "-p." + pe;
            }
            int secs = int.TryParse(GetDataValue("secs"), out int sv) ? Math.Clamp(sv, 0, 36000) : 0;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                site.Media.Add(new MediaItem { Id = id, Kind = "video", Full = url + "." + ext, Thumb = poster, Title = title, Seconds = secs, By = c.U.Id, When = DateTime.UtcNow });
                await Store.SaveSite(site, c.Sbx);
            }
            await Store.Log(c.Sbx, c.U.Id, "upload", "video · " + title, true);
            response.ExecuteScript("AdminMediaJs.vdone();AdminMediaJs.toast('Video added to the library');");
            return response;
        }

        public async Task<ApiResponse> Rename()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMediaJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "media-rename", GetDataValue("mid"), "You can browse the library but not change it.");
            }
            string id = Ui.Clip(GetDataValue("mid"), 60);
            string title = CleanTitle(GetDataValue("mtitle"));
            if (title.Length < 2)
            {
                response.ExecuteScript("AdminMediaJs.toast('Give it a title.');");
                return response;
            }
            SiteData? saved = null;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                MediaItem? m = site.Media.FirstOrDefault(x => x.Id == id);
                if (m != null)
                {
                    m.Title = title;
                    await Store.SaveSite(site, c.Sbx);
                    saved = site;
                }
            }
            if (saved == null)
            {
                return response;
            }
            await Store.Log(c.Sbx, c.U.Id, "media-rename", title, true);
            await Redraw(response, c, saved);
            response.ExecuteScript("AdminMediaJs.toast('Renamed');");
            return response;
        }

        public async Task<ApiResponse> Remove()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMediaJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "media-delete", GetDataValue("mid"), "You can browse the library but not change it.");
            }
            string id = Ui.Clip(GetDataValue("mid"), 60);
            MediaItem? gone = null;
            List<string> uses = new List<string>();
            bool builtin = false;
            SiteData? saved = null;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                MediaItem? m = site.Media.FirstOrDefault(x => x.Id == id);
                if (m != null)
                {
                    builtin = m.Builtin;
                    uses = Uses(site, m.Full);
                    if (!builtin && uses.Count == 0)
                    {
                        site.Media.Remove(m);
                        await Store.SaveSite(site, c.Sbx);
                        gone = m;
                        saved = site;
                    }
                }
            }
            if (builtin)
            {
                response.ExecuteScript("AdminMediaJs.toast('Built-in photos and videos can be renamed but not deleted.');");
                return response;
            }
            if (uses.Count > 0)
            {
                response.ExecuteScript("AdminMediaJs.toast('" + Ui.Js("Used on " + string.Join(", ", uses) + " – remove it there first.") + "');");
                return response;
            }
            if (gone == null || saved == null)
            {
                return response;
            }
            foreach (string f in new[] { gone.Full, gone.Thumb }.Distinct())
            {
                if (f.StartsWith("images/uploads/", StringComparison.Ordinal) && (Access.SafeImage(f) || Access.SafeVideo(f)))
                {
                    string path = Path.Combine(Store.Root, f.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
            }
            await Store.Log(c.Sbx, c.U.Id, "media-delete", gone.Title, true);
            await Redraw(response, c, saved);
            response.ExecuteScript("AdminMediaJs.toast('Deleted');");
            return response;
        }
    }
}
