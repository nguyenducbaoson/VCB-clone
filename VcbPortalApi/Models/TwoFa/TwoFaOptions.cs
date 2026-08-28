// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution thật đã có hai lớp option này. ĐỪNG chép đè.
// Chỉ dựng đúng các field mà TwoFaService.IsEnabled / IsSmsNotifyEnabled đọc tới.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Models.TwoFa
{
    public class TwoFaOptions
    {
        public bool Enabled { get; set; }
        public string? BaseUrl { get; set; }
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
    }

    public class SmsNotifyOptions
    {
        public bool Enabled { get; set; }
        public string? BaseUrl { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
    }
}
