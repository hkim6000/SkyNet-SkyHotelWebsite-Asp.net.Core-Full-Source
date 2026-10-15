using System.Globalization;
using System.Security.Cryptography;
using SkyHotel.Models;

namespace SkyHotel.Core
{
    public sealed class Quote
    {
        public string Error { get; set; } = string.Empty;
        public RoomType? Type { get; set; }
        public int Nights { get; set; }
        public List<(DateTime Day, decimal Rate)> Nightly { get; set; } = new List<(DateTime, decimal)>();
        public decimal RoomSub { get; set; }
        public decimal Breakfast { get; set; }
        public bool FreeBreakfast { get; set; }
        public int MemberPct { get; set; }
        public decimal MemberOff { get; set; }
        public Offer? Offer { get; set; }
        public decimal PromoOff { get; set; }
        public string PromoNote { get; set; } = string.Empty;
        public int PointsUsed { get; set; }
        public decimal PointsValue { get; set; }
        public int PointsMax { get; set; }
        public decimal Tax { get; set; }
        public decimal Total { get; set; }
        public int Earn { get; set; }
    }

    public static class Book
    {
        public static readonly (string Key, string Name, string Hint)[] Plans =
        {
            ("flex", "Flexible", "Free cancellation until 48 hours before arrival"),
            ("nonref", "Advance saver", "10% off, paid in full, no refund"),
            ("breakfast", "Bed & breakfast", "Flexible rate with breakfast for everyone")
        };

        public static string PlanName(string key)
        {
            return Plans.FirstOrDefault(p => p.Key == key).Name ?? "Flexible";
        }

        public static bool Holds(Booking b)
        {
            return b.Status == "booked" || b.Status == "inhouse";
        }

        public static int Nights(DateTime a, DateTime b)
        {
            return (int)Math.Round((b.Date - a.Date).TotalDays);
        }

        public static decimal Rate(SiteData site, RoomType t, DateTime day)
        {
            decimal r = day.DayOfWeek == DayOfWeek.Friday || day.DayOfWeek == DayOfWeek.Saturday ? t.Weekend : t.Base;
            int pct = site.Seasons.Where(s => day.Date >= s.From.Date && day.Date <= s.To.Date).Select(s => s.Pct).DefaultIfEmpty(0).Max();
            return Math.Round(r * (100 + pct) / 100m, 0);
        }

        public static int Blocked(SiteData site, string type, DateTime day)
        {
            HashSet<string> rooms = new HashSet<string>(site.Rooms.Where(r => r.Type == type).Select(r => r.No));
            return site.Blocks.Where(x => rooms.Contains(x.Room) && day.Date >= x.From.Date && day.Date <= x.To.Date).Select(x => x.Room).Distinct().Count();
        }

        public static int Free(SiteData site, string type, DateTime day, string ignore = "")
        {
            int count = site.Rooms.Count(r => r.Type == type);
            int booked = site.Bookings.Count(b => b.Type == type && b.Id != ignore && Holds(b) && b.In.Date <= day.Date && b.Out.Date > day.Date);
            return count - booked - Blocked(site, type, day);
        }

        public static int Available(SiteData site, string type, DateTime din, DateTime dout, string ignore = "")
        {
            int min = int.MaxValue;
            for (DateTime d = din.Date; d < dout.Date; d = d.AddDays(1))
            {
                min = Math.Min(min, Free(site, type, d, ignore));
            }
            return min == int.MaxValue ? 0 : Math.Max(0, min);
        }

        public static bool RoomFree(SiteData site, string room, DateTime din, DateTime dout, string ignore = "")
        {
            if (site.Bookings.Any(b => b.Room == room && b.Id != ignore && Holds(b) && b.In.Date < dout.Date && b.Out.Date > din.Date))
            {
                return false;
            }
            return !site.Blocks.Any(x => x.Room == room && x.From.Date < dout.Date && x.To.Date >= din.Date);
        }

        public static string CheckDates(DateTime? din, DateTime? dout)
        {
            DateTime today = DateTime.UtcNow.Date;
            if (din == null || dout == null)
            {
                return "Choose your check-in and check-out dates.";
            }
            if (din.Value < today)
            {
                return "Check-in can't be in the past.";
            }
            if (dout.Value <= din.Value)
            {
                return "Check-out must be after check-in.";
            }
            if (Nights(din.Value, dout.Value) > 14)
            {
                return "Online bookings are up to 14 nights. Call us for longer stays.";
            }
            if (din.Value > today.AddDays(365))
            {
                return "Bookings open 12 months ahead.";
            }
            return string.Empty;
        }

        public static Offer? FindOffer(SiteData site, string code)
        {
            string c = (code ?? string.Empty).Trim().ToUpperInvariant();
            return c == string.Empty ? null : site.Offers.FirstOrDefault(o => o.Code.Equals(c, StringComparison.OrdinalIgnoreCase));
        }

        public static Quote Price(SiteData site, string type, DateTime din, DateTime dout, int adults, int children, string plan, string promo, Guest? member, int points)
        {
            Quote q = new Quote();
            q.Type = Ui.TypeOf(site, type);
            if (q.Type == null || !q.Type.Active)
            {
                q.Error = "Choose a room.";
                return q;
            }
            if (adults + children > q.Type.MaxGuests)
            {
                q.Error = q.Type.Name + " sleeps up to " + q.Type.MaxGuests + " guests.";
                return q;
            }
            q.Nights = Nights(din, dout);
            for (DateTime d = din.Date; d < dout.Date; d = d.AddDays(1))
            {
                q.Nightly.Add((d, Rate(site, q.Type, d)));
            }
            decimal sub = q.Nightly.Sum(n => n.Rate);
            if (plan == "nonref")
            {
                sub = Math.Round(sub * 0.9m, 2);
            }
            q.RoomSub = sub;
            if (member != null && member.Member)
            {
                q.MemberPct = Ui.GradeOf(member.Lifetime).Pct;
                q.MemberOff = Math.Round(sub * q.MemberPct / 100m, 2);
            }
            if (plan == "breakfast")
            {
                q.FreeBreakfast = member != null && member.Member && member.Lifetime >= 100000;
                q.Breakfast = q.FreeBreakfast ? 0 : site.Settings.Breakfast * (adults + children) * q.Nights;
            }
            Offer? o = FindOffer(site, promo);
            if (promo.Trim() != string.Empty)
            {
                DateTime today = DateTime.UtcNow.Date;
                if (o == null || !o.Active || today < o.From.Date || today > o.To.Date)
                {
                    q.PromoNote = "That code isn't valid right now.";
                }
                else if (o.Members && (member == null || !member.Member))
                {
                    q.PromoNote = "That code is for SkyHotel Circle members. Sign in to use it.";
                }
                else if (!Fits(o, type))
                {
                    q.PromoNote = o.Code + " isn't valid for " + q.Type.Name + ". It's for " + TypesText(site, o) + ".";
                }
                else if (q.Nights < o.MinNights)
                {
                    q.PromoNote = "That code needs a stay of at least " + o.MinNights + " nights.";
                }
                else
                {
                    q.Offer = o;
                    q.PromoOff = Math.Round((sub - q.MemberOff) * o.Pct / 100m, 2);
                }
            }
            decimal room = sub - q.MemberOff - q.PromoOff;
            if (member != null && member.Member)
            {
                q.PointsMax = Math.Min(member.Balance, (int)Math.Floor(room * 0.5m / 10m) * 1000) / 1000 * 1000;
                q.PointsUsed = Math.Clamp(points / 1000 * 1000, 0, q.PointsMax);
                q.PointsValue = q.PointsUsed / 100m;
            }
            decimal taxable = room + q.Breakfast - q.PointsValue;
            q.Tax = Math.Round(taxable * site.Settings.Tax / 100m, 2);
            q.Total = taxable + q.Tax;
            q.Earn = member != null && member.Member ? (int)Math.Floor(taxable) * (q.Offer?.Bonus ?? 1) : 0;
            return q;
        }

        public static bool Fits(Offer o, string type)
        {
            return o.Types.Count == 0 || o.Types.Contains(type);
        }

        public static string TypesText(SiteData site, Offer o)
        {
            return o.Types.Count == 0 ? "all room types" : string.Join(", ", o.Types.Select(t => Ui.TypeName(site, t)));
        }

        public static string NewCode(SiteData site)
        {
            const string abc = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            string code;
            do
            {
                char[] c = new char[6];
                for (int i = 0; i < 6; i++)
                {
                    c[i] = abc[RandomNumberGenerator.GetInt32(abc.Length)];
                }
                code = "SKH-" + new string(c);
            }
            while (site.Bookings.Any(b => b.Code == code));
            return code;
        }

        public static string NewId()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 12);
        }

        public static DateTime ArrivalTime(SiteData site, Booking b)
        {
            TimeSpan t = TimeSpan.TryParse(site.Settings.CheckIn, CultureInfo.InvariantCulture, out TimeSpan x) ? x : new TimeSpan(15, 0, 0);
            return b.In.Date.Add(t);
        }

        public static bool FreeCancel(SiteData site, Booking b)
        {
            return b.Plan != "nonref" && DateTime.UtcNow < ArrivalTime(site, b).AddHours(-site.Settings.CancelHours);
        }

        public static decimal RefundFor(SiteData site, Booking b)
        {
            if (b.Plan == "nonref")
            {
                return 0;
            }
            if (FreeCancel(site, b))
            {
                return b.Paid;
            }
            int n = Math.Max(1, Nights(b.In, b.Out));
            decimal first = Math.Round(b.Total / n, 2);
            return Math.Max(0, b.Paid - first);
        }

        public static decimal ExtrasTotal(Booking b)
        {
            return b.Extras.Sum(x => x.Amount);
        }

        public static decimal Balance(SiteData site, Booking b)
        {
            decimal extras = ExtrasTotal(b);
            decimal extrasTax = Math.Round(extras * site.Settings.Tax / 100m, 2);
            return b.Total + extras + extrasTax - b.Paid;
        }

        public static int EarnOnStay(Booking b)
        {
            return (int)Math.Floor(b.Total - b.Tax + ExtrasTotal(b)) * Math.Max(1, b.Bonus);
        }

        public static void AddPoints(SiteData site, Guest g, int pts, string kind, string booking, string note, string by)
        {
            g.Balance = Math.Max(0, g.Balance + pts);
            if (kind == "earn" || (kind == "adjust" && pts > 0))
            {
                g.Lifetime += pts;
            }
            else if (kind == "adjust" && pts < 0)
            {
                g.Lifetime = Math.Max(0, g.Lifetime + pts);
            }
            site.Points.Insert(0, new PointEntry { Id = NewId(), Guest = g.Id, When = DateTime.UtcNow, Pts = pts, Kind = kind, Booking = booking, Note = note, By = by });
        }

        public static decimal PerNight(Booking b)
        {
            return Math.Round((b.Total - b.Tax - b.Breakfast) / Math.Max(1, Nights(b.In, b.Out)), 2);
        }

        public static bool Sleeps(Booking b, DateTime day)
        {
            return (b.Status == "booked" || b.Status == "inhouse" || b.Status == "out") && b.In.Date <= day.Date && b.Out.Date > day.Date;
        }

        public static int Pending(SiteData site, Guest g)
        {
            return site.Bookings.Where(b => b.Guest == g.Id && Holds(b)).Sum(EarnOnStay);
        }
    }
}
