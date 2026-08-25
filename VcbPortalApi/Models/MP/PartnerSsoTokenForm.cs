using System.ComponentModel.DataAnnotations;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution thật đã có form này. ĐỪNG chép đè.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Models.MP
{
    /// <summary>Body dạng x-www-form-urlencoded của POST /ma/partner/token.</summary>
    public class PartnerSsoTokenForm
    {
        [Required]
        public string? PartnerCode { get; set; }

        /// <summary>User role BID bắt buộc gửi; user role MID thì bỏ qua, lấy MID từ DB.</summary>
        public decimal? Mid { get; set; }

        public decimal? Tid { get; set; }
    }
}
