using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SkyHotel.Models;

namespace SkyHotel.Core
{
    public sealed class Area
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Page { get; init; } = string.Empty;
        public string Extra { get; init; } = string.Empty;
        public bool AdminOnly { get; init; }
        public bool NoEdit { get; init; }
    }

    public static class Access
    {
        public static readonly Area[] Areas =
        {
            new Area { Key = "frontdesk", Name = "Check-in / Check-out", Page = "AdminFrontDesk" },
            new Area { Key = "bookings", Name = "Bookings", Page = "AdminBookings", Extra = "Cancel & refund" },
            new Area { Key = "calendar", Name = "Calendar", Page = "AdminCalendar" },
            new Area { Key = "housekeeping", Name = "Housekeeping", Page = "AdminHousekeeping" },
            new Area { Key = "rates", Name = "Rooms & Rates", Page = "AdminRooms", Extra = "Block dates" },
            new Area { Key = "offers", Name = "Offers", Page = "AdminOffers" },
            new Area { Key = "members", Name = "Members", Page = "AdminMembers", Extra = "Adjust points" },
            new Area { Key = "guests", Name = "Guests", Page = "AdminGuests" },
            new Area { Key = "reviews", Name = "Reviews", Page = "AdminReviews" },
            new Area { Key = "content", Name = "Content", Page = "AdminContent" },
            new Area { Key = "media", Name = "Media library", Page = "AdminMedia" },
            new Area { Key = "reports", Name = "Reports", Page = "AdminReports", NoEdit = true },
            new Area { Key = "activity", Name = "Activity log", Page = "AdminActivity", NoEdit = true },
            new Area { Key = "settings", Name = "Settings", Page = "AdminSettings", AdminOnly = true },
            new Area { Key = "users", Name = "Users", Page = "AdminUsers", AdminOnly = true }
        };

        public static Area? AreaOf(string key)
        {
            return Areas.FirstOrDefault(a => a.Key == key);
        }

        public static string Level(Staff u, string area)
        {
            if (u.Admin)
            {
                return "VEX";
            }
            Area? a = AreaOf(area);
            if (a == null || a.AdminOnly)
            {
                return string.Empty;
            }
            string p = u.Perms.TryGetValue(area, out string? v) ? v ?? string.Empty : string.Empty;
            string r = string.Empty;
            if (p.Contains('V') || p.Contains('E') || p.Contains('X'))
            {
                r += "V";
            }
            if (p.Contains('E') && !a.NoEdit)
            {
                r += "E";
            }
            if (p.Contains('X') && p.Contains('E') && a.Extra != string.Empty)
            {
                r += "X";
            }
            return r;
        }

        public static bool Can(Staff u, string area, char letter)
        {
            return Level(u, area).Contains(letter);
        }

        public static string Clean(string area, string value)
        {
            Area? a = AreaOf(area);
            if (a == null || a.AdminOnly)
            {
                return string.Empty;
            }
            string v = value ?? string.Empty;
            string r = string.Empty;
            if (v.Contains('V') || v.Contains('E'))
            {
                r = "V";
            }
            if (v.Contains('E') && !a.NoEdit)
            {
                r += "E";
                if (v.Contains('X') && a.Extra != string.Empty)
                {
                    r += "X";
                }
            }
            return r;
        }

        public static string HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] dk = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, 100000, HashAlgorithmName.SHA256, 32);
            return "pbkdf2$100000$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(dk);
        }

        public static bool Verify(string password, string stored)
        {
            string[] parts = (stored ?? string.Empty).Split('$');
            if (parts.Length != 4 || parts[0] != "pbkdf2" || !int.TryParse(parts[1], out int iter) || iter < 10000 || iter > 1000000)
            {
                return false;
            }
            try
            {
                byte[] salt = Convert.FromBase64String(parts[2]);
                byte[] want = Convert.FromBase64String(parts[3]);
                byte[] got = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iter, HashAlgorithmName.SHA256, want.Length);
                return CryptographicOperations.FixedTimeEquals(got, want);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public static bool VerifySha(string password, string hex)
        {
            string hash = (hex ?? string.Empty).ToLowerInvariant();
            if (!Regex.IsMatch(hash, "^[a-f0-9]{64}$"))
            {
                return false;
            }
            string given = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant();
            return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(given), Encoding.ASCII.GetBytes(hash));
        }

        public static bool SafeImage(string src)
        {
            return Regex.IsMatch(src ?? string.Empty, "^images/[A-Za-z0-9/_.-]+\\.(webp|jpg|jpeg|png)$") && !src!.Contains("..");
        }

        public static bool SafeVideo(string src)
        {
            return Regex.IsMatch(src ?? string.Empty, "^images/[A-Za-z0-9/_.-]+\\.(mp4|webm)$") && !src!.Contains("..");
        }

        public static bool Luhn(string digits)
        {
            int sum = 0;
            bool dbl = false;
            for (int i = digits.Length - 1; i >= 0; i--)
            {
                int d = digits[i] - '0';
                if (d < 0 || d > 9)
                {
                    return false;
                }
                if (dbl)
                {
                    d *= 2;
                    if (d > 9)
                    {
                        d -= 9;
                    }
                }
                sum += d;
                dbl = !dbl;
            }
            return digits.Length >= 12 && sum % 10 == 0;
        }

        public static string Brand(string digits)
        {
            if (digits.StartsWith('4'))
            {
                return "Visa";
            }
            if (Regex.IsMatch(digits, "^(5[1-5]|2[2-7])"))
            {
                return "Mastercard";
            }
            if (Regex.IsMatch(digits, "^3[47]"))
            {
                return "Amex";
            }
            if (digits.StartsWith("6011") || digits.StartsWith("65"))
            {
                return "Discover";
            }
            return "Card";
        }
    }
}
