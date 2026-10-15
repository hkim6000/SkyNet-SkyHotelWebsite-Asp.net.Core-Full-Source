using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using SkyHotel.Models;

namespace SkyHotel.Core
{
    public static class Store
    {
        private static readonly JsonSerializerOptions ReadOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private static readonly JsonSerializerOptions WriteOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim LogGate = new SemaphoreSlim(1, 1);

        public static readonly string[] Files = { "site", "staff", "activity" };

        public static string Root => Directory.GetCurrentDirectory();

        public static string DataDir => Path.Combine(Root, "data");

        public static string Cfg(string key)
        {
            string file = Path.Combine(Root, "appConfig", "application.cfg");
            if (!File.Exists(file))
            {
                return string.Empty;
            }
            foreach (string line in File.ReadAllLines(file))
            {
                int i = line.IndexOf('|');
                if (i > 0 && line.Substring(0, i).Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return line.Substring(i + 1).Trim();
                }
            }
            return string.Empty;
        }

        public static bool Demo => Cfg("app.app.runmode") != "1";

        public static string CleanId(string? value)
        {
            string v = (value ?? string.Empty).Trim().ToLowerInvariant();
            return Regex.IsMatch(v, "^[a-f0-9]{32}$") ? v : string.Empty;
        }

        public static string SandboxDir(string sbx)
        {
            return Path.Combine(Root, "temp", "sandbox", sbx);
        }

        public static string Sandbox(string? value)
        {
            string id = CleanId(value);
            return id != string.Empty && Demo && File.Exists(Path.Combine(SandboxDir(id), "site.json")) && Made(id) > DateTime.UtcNow.AddMinutes(-60) ? id : string.Empty;
        }

        public static DateTime Made(string sbx)
        {
            string file = Path.Combine(SandboxDir(sbx), "made");
            return File.Exists(file) && long.TryParse(File.ReadAllText(file), NumberStyles.Integer, CultureInfo.InvariantCulture, out long t) ? new DateTime(t, DateTimeKind.Utc) : Directory.GetCreationTimeUtc(SandboxDir(sbx));
        }

        public static int MinutesLeft(string sbx)
        {
            return Math.Max(1, (int)Math.Ceiling((Made(sbx).AddMinutes(60) - DateTime.UtcNow).TotalMinutes));
        }

        private static string FileOf(string name, string sbx)
        {
            return Path.Combine(sbx == string.Empty ? DataDir : SandboxDir(sbx), name + ".json");
        }

        public static async Task<IDisposable> Hold()
        {
            await Gate.WaitAsync();
            return new Release(Gate);
        }

        private sealed class Release : IDisposable
        {
            private SemaphoreSlim? gate;

            public Release(SemaphoreSlim g)
            {
                gate = g;
            }

            public void Dispose()
            {
                gate?.Release();
                gate = null;
            }
        }

        private static async Task<T> Read<T>(string name, string sbx) where T : new()
        {
            string file = FileOf(name, sbx);
            if (!File.Exists(file))
            {
                file = FileOf(name, string.Empty);
            }
            if (!File.Exists(file))
            {
                return new T();
            }
            try
            {
                return JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(file), ReadOpts) ?? new T();
            }
            catch (JsonException)
            {
                return new T();
            }
        }

        private static async Task Write<T>(string name, string sbx, T data, bool keepPrevious)
        {
            string file = FileOf(name, sbx);
            Directory.CreateDirectory(Path.GetDirectoryName(file) ?? Root);
            if (keepPrevious && File.Exists(file))
            {
                File.Copy(file, Path.Combine(Path.GetDirectoryName(file) ?? Root, name + ".prev.json"), true);
            }
            string tmp = file + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp";
            await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(data, WriteOpts));
            File.Move(tmp, file, true);
        }

        public static async Task<SiteData> LoadSite(string sbx = "")
        {
            SiteData site = await Read<SiteData>("site", sbx);
            Builtins(site);
            if (Demo && sbx == string.Empty)
            {
                Shift(site, Days(site.Anchor));
            }
            return site;
        }

        private static readonly Dictionary<string, (string Title, int Seconds)> Videos = new Dictionary<string, (string, int)>
        {
            ["harbour-dusk"] = ("Harbour at dusk", 14),
            ["penthouse-tour"] = ("Penthouse Suite tour", 13),
            ["rooftop-sunset"] = ("Sunset on the roof", 13)
        };

        private static string Pretty(string key)
        {
            string t = Regex.Replace(key, "[-_]+", " ").Trim();
            return t.Length > 0 ? char.ToUpperInvariant(t[0]) + t.Substring(1) : key;
        }

        private static void Builtins(SiteData site)
        {
            if (site.Media.Count > 0)
            {
                return;
            }
            DateTime when = site.Anchor == default ? DateTime.UtcNow.Date : site.Anchor.Date;
            string full = Path.Combine(Root, "images", "hotel", "full");
            if (Directory.Exists(full))
            {
                foreach (string f in Directory.GetFiles(full, "*.webp").OrderBy(f => f))
                {
                    string key = Path.GetFileNameWithoutExtension(f);
                    site.Media.Add(new MediaItem { Id = "b-" + key, Kind = "photo", Title = Pretty(key), Full = "images/hotel/full/" + key + ".webp", Thumb = "images/hotel/thumbs/" + key + ".webp", By = "u-elena", When = when.AddDays(-200), Builtin = true });
                }
            }
            string video = Path.Combine(Root, "images", "hotel", "video");
            if (Directory.Exists(video))
            {
                foreach (string f in Directory.GetFiles(video, "*.mp4").OrderBy(f => f))
                {
                    string key = Path.GetFileNameWithoutExtension(f);
                    (string title, int secs) = Videos.TryGetValue(key, out (string, int) v) ? v : (Pretty(key), 0);
                    site.Media.Add(new MediaItem { Id = "b-" + key, Kind = "video", Title = title, Full = "images/hotel/video/" + key + ".mp4", Thumb = File.Exists(Path.Combine(video, key + ".webp")) ? "images/hotel/video/" + key + ".webp" : "images/hotel/thumbs/exterior-dusk.webp", Seconds = secs, By = "u-elena", When = when.AddDays(-40), Builtin = true });
                }
            }
        }

        private static TimeSpan Days(DateTime anchor)
        {
            return anchor == default ? TimeSpan.Zero : TimeSpan.FromDays(Math.Floor((DateTime.UtcNow.Date - anchor.Date).TotalDays));
        }

        private static void Shift(SiteData site, TimeSpan d)
        {
            if (site.Anchor == default || d == TimeSpan.Zero)
            {
                return;
            }
            foreach (Season x in site.Seasons)
            {
                x.From = x.From.Add(d);
                x.To = x.To.Add(d);
            }
            foreach (RoomBlock x in site.Blocks)
            {
                x.From = x.From.Add(d);
                x.To = x.To.Add(d);
            }
            foreach (Offer x in site.Offers)
            {
                x.From = x.From.Add(d);
                x.To = x.To.Add(d);
            }
            foreach (HotelRoom x in site.Rooms)
            {
                x.Changed = x.Changed.Add(d);
            }
            foreach (Booking b in site.Bookings)
            {
                b.In = b.In.Add(d);
                b.Out = b.Out.Add(d);
                b.Created = b.Created.Add(d);
                b.CheckedIn = b.CheckedIn?.Add(d);
                b.CheckedOut = b.CheckedOut?.Add(d);
                b.Cancelled = b.Cancelled?.Add(d);
                foreach (Charge c in b.Extras)
                {
                    c.When = c.When.Add(d);
                }
            }
            foreach (Guest g in site.Guests)
            {
                g.Created = g.Created.Add(d);
                g.Joined = g.Joined?.Add(d);
            }
            foreach (PointEntry p in site.Points)
            {
                p.When = p.When.Add(d);
            }
            foreach (Review r in site.Reviews)
            {
                r.When = r.When.Add(d);
            }
            foreach (MediaItem m in site.Media)
            {
                m.When = m.When.Add(d);
            }
            site.Anchor = site.Anchor.Add(d);
        }

        public static Task SaveSite(SiteData site, string sbx)
        {
            return Write("site", sbx, site, false);
        }

        public static Task<List<Staff>> LoadStaff(string sbx = "")
        {
            return Read<List<Staff>>("staff", sbx);
        }

        public static Task SaveStaff(List<Staff> staff, string sbx)
        {
            return Write("staff", sbx, staff, false);
        }

        public static Task<List<Activity>> LoadActivity(string sbx = "")
        {
            return Read<List<Activity>>("activity", sbx);
        }

        public static async Task Log(string sbx, string user, string action, string target, bool ok)
        {
            await LogGate.WaitAsync();
            try
            {
                List<Activity> list = await Read<List<Activity>>("activity", sbx);
                list.Insert(0, new Activity { When = DateTime.UtcNow, User = user, Action = action, Target = target.Length > 120 ? target.Substring(0, 120) : target, Ok = ok });
                if (list.Count > 5000)
                {
                    list.RemoveRange(5000, list.Count - 5000);
                }
                await Write("activity", sbx, list, false);
            }
            finally
            {
                LogGate.Release();
            }
        }

        public static async Task<string> NewSandbox()
        {
            Sweep();
            string id = Guid.NewGuid().ToString("N");
            string dir = SandboxDir(id);
            Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(Path.Combine(dir, "made"), DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
            SiteData site = await Read<SiteData>("site", string.Empty);
            TimeSpan d = Days(site.Anchor);
            Shift(site, d);
            await Write("site", id, site, false);
            List<Activity> acts = await Read<List<Activity>>("activity", string.Empty);
            foreach (Activity a in acts)
            {
                a.When = a.When.Add(d);
            }
            await Write("activity", id, acts, false);
            File.Copy(FileOf("staff", string.Empty), FileOf("staff", id), true);
            return id;
        }

        public static void Sweep()
        {
            DateTime limit = DateTime.UtcNow.AddMinutes(-60);
            string boxes = Path.Combine(Root, "temp", "sandbox");
            string uploads = Path.Combine(Root, "images", "uploads", "sandbox");
            try
            {
                if (Directory.Exists(boxes))
                {
                    foreach (string dir in Directory.GetDirectories(boxes))
                    {
                        if (Made(Path.GetFileName(dir)) < limit)
                        {
                            Directory.Delete(dir, true);
                        }
                    }
                }
                if (Directory.Exists(uploads))
                {
                    foreach (string dir in Directory.GetDirectories(uploads))
                    {
                        if (!Directory.Exists(SandboxDir(Path.GetFileName(dir))))
                        {
                            Directory.Delete(dir, true);
                        }
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
