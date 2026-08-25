// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution thật đã có các entity này. Dựng lại đủ những cột mà
// MobilePartnerController.IssueSsoToken đọc tới. ĐỪNG chép đè.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Models.MP
{
    /// <summary>MP_SESSION — phiên đăng nhập của user mobile.</summary>
    public class MpSession
    {
        public string UserName { get; set; } = string.Empty;
        public string? SessionId { get; set; }
    }

    /// <summary>MP_USERS_COMMON — thông tin chung, nơi giữ Email và RoleId.</summary>
    public class MpUsersCommon
    {
        public string UserName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public decimal? RoleId { get; set; }
    }

    /// <summary>
    /// Bảng phân cấp merchant bên MerchantContext, dùng để kiểm tra
    /// mid có thuộc bid và tid có thuộc mid hay không.
    /// </summary>
    public class MpTerminal
    {
        public int RowId { get; set; }
        public decimal Bid { get; set; }
        public decimal Mid { get; set; }
        public decimal Tid { get; set; }
    }
}
