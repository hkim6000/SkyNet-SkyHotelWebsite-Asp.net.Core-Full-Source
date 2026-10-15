namespace SkyHotel.Models
{
    public class SiteData
    {
        public DateTime Anchor { get; set; }
        public HotelSettings Settings { get; set; } = new HotelSettings();
        public List<RoomType> Types { get; set; } = new List<RoomType>();
        public List<HotelRoom> Rooms { get; set; } = new List<HotelRoom>();
        public List<Season> Seasons { get; set; } = new List<Season>();
        public List<RoomBlock> Blocks { get; set; } = new List<RoomBlock>();
        public List<Offer> Offers { get; set; } = new List<Offer>();
        public List<Booking> Bookings { get; set; } = new List<Booking>();
        public List<Guest> Guests { get; set; } = new List<Guest>();
        public List<PointEntry> Points { get; set; } = new List<PointEntry>();
        public List<Review> Reviews { get; set; } = new List<Review>();
        public Dictionary<string, ContentBlock> Content { get; set; } = new Dictionary<string, ContentBlock>();
        public List<string> Gallery { get; set; } = new List<string>();
        public List<MediaItem> Media { get; set; } = new List<MediaItem>();
    }

    public class HotelSettings
    {
        public string Name { get; set; } = "SkyHotel";
        public string Tagline { get; set; } = string.Empty;
        public string Accent { get; set; } = "#B0874A";
        public string Theme { get; set; } = "light";
        public decimal Tax { get; set; } = 12m;
        public string CheckIn { get; set; } = "15:00";
        public string CheckOut { get; set; } = "11:00";
        public int CancelHours { get; set; } = 48;
        public decimal Breakfast { get; set; } = 24m;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Town { get; set; } = string.Empty;
        public string Country { get; set; } = "United States";
        public decimal Deposit { get; set; } = 150m;
    }

    public class RoomType
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Short { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public int Size { get; set; }
        public string Beds { get; set; } = string.Empty;
        public string View { get; set; } = string.Empty;
        public int MaxGuests { get; set; } = 2;
        public decimal Base { get; set; }
        public decimal Weekend { get; set; }
        public List<string> Images { get; set; } = new List<string>();
        public string Video { get; set; } = string.Empty;
        public List<string> Amenities { get; set; } = new List<string>();
        public int Order { get; set; }
        public bool Active { get; set; } = true;
    }

    public class HotelRoom
    {
        public string No { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int Floor { get; set; }
        public string Status { get; set; } = "clean";
        public string Note { get; set; } = string.Empty;
        public DateTime Changed { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class Season
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int Pct { get; set; }
    }

    public class RoomBlock
    {
        public string Id { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string By { get; set; } = string.Empty;
    }

    public class Offer
    {
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public int Pct { get; set; }
        public int Bonus { get; set; } = 1;
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int MinNights { get; set; } = 1;
        public bool Members { get; set; }
        public bool Active { get; set; } = true;
        public string Image { get; set; } = string.Empty;
        public List<string> Types { get; set; } = new List<string>();
        public int Used { get; set; }
    }

    public class Charge
    {
        public string What { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime When { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class Booking
    {
        public string Id { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Guest { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
        public DateTime In { get; set; }
        public DateTime Out { get; set; }
        public int Adults { get; set; } = 2;
        public int Children { get; set; }
        public string Plan { get; set; } = "flex";
        public decimal RoomTotal { get; set; }
        public decimal Discount { get; set; }
        public string Promo { get; set; } = string.Empty;
        public int Bonus { get; set; } = 1;
        public decimal MemberOff { get; set; }
        public decimal Breakfast { get; set; }
        public int PointsUsed { get; set; }
        public decimal PointsValue { get; set; }
        public decimal Tax { get; set; }
        public decimal Total { get; set; }
        public decimal Paid { get; set; }
        public decimal Refund { get; set; }
        public string Card { get; set; } = string.Empty;
        public string Last4 { get; set; } = string.Empty;
        public string Status { get; set; } = "booked";
        public string Source { get; set; } = "web";
        public string Requests { get; set; } = string.Empty;
        public string Arrival { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string Agency { get; set; } = string.Empty;
        public string AgencyRef { get; set; } = string.Empty;
        public bool PayAtFront { get; set; }
        public string PaidBy { get; set; } = string.Empty;
        public string Plate { get; set; } = string.Empty;
        public string Valet { get; set; } = string.Empty;
        public string LateOut { get; set; } = string.Empty;
        public decimal Deposit { get; set; }
        public string DepositBy { get; set; } = string.Empty;
        public decimal DepositBack { get; set; }
        public string By { get; set; } = string.Empty;
        public List<Charge> Extras { get; set; } = new List<Charge>();
        public int PointsEarned { get; set; }
        public DateTime Created { get; set; }
        public DateTime? CheckedIn { get; set; }
        public DateTime? CheckedOut { get; set; }
        public DateTime? Cancelled { get; set; }
    }

    public class Guest
    {
        public string Id { get; set; } = string.Empty;
        public string First { get; set; } = string.Empty;
        public string Last { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Passport { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public bool Vip { get; set; }
        public bool Member { get; set; }
        public string MemberNo { get; set; } = string.Empty;
        public DateTime? Joined { get; set; }
        public string Hash { get; set; } = string.Empty;
        public int Balance { get; set; }
        public int Lifetime { get; set; }
        public bool Demo { get; set; }
        public DateTime Created { get; set; }
    }

    public class PointEntry
    {
        public string Id { get; set; } = string.Empty;
        public string Guest { get; set; } = string.Empty;
        public DateTime When { get; set; }
        public int Pts { get; set; }
        public string Kind { get; set; } = "earn";
        public string Booking { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string By { get; set; } = string.Empty;
    }

    public class Review
    {
        public string Id { get; set; } = string.Empty;
        public string Guest { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Booking { get; set; } = string.Empty;
        public int Rating { get; set; } = 5;
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime When { get; set; }
        public string Status { get; set; } = "pending";
        public string Reply { get; set; } = string.Empty;
    }

    public class ContentBlock
    {
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public string Video { get; set; } = string.Empty;
        public List<ContentItem> Items { get; set; } = new List<ContentItem>();
    }

    public class ContentItem
    {
        public string Name { get; set; } = string.Empty;
        public string Meta { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    public class MediaItem
    {
        public string Id { get; set; } = string.Empty;
        public string Kind { get; set; } = "photo";
        public string Title { get; set; } = string.Empty;
        public string Full { get; set; } = string.Empty;
        public string Thumb { get; set; } = string.Empty;
        public int Seconds { get; set; }
        public string By { get; set; } = string.Empty;
        public DateTime When { get; set; }
        public bool Builtin { get; set; }
    }

    public class Staff
    {
        public string Id { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public bool Admin { get; set; }
        public bool Active { get; set; } = true;
        public bool Demo { get; set; }
        public Dictionary<string, string> Perms { get; set; } = new Dictionary<string, string>();
    }

    public class Activity
    {
        public DateTime When { get; set; }
        public string User { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public bool Ok { get; set; } = true;
    }

    public class Session
    {
        public string Tok { get; set; } = string.Empty;
        public string Sbx { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string Kind { get; set; } = "staff";
        public DateTime Exp { get; set; }
    }
}
