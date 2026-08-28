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

    // MP_USERS_COMMON: xem VcbPortalApi.Models.MP.User.MpUserCommon.
    //
    // TRƯỚC ĐÂY repo này có thêm một entity rút gọn tên MpUsersCommon cho riêng
    // luồng mobile. Solution thật chỉ có MỘT entity cho bảng này, và bản rút gọn
    // đã che mất một lỗi thật: MpUserCommon.Salt/Password/UHash khai không
    // nullable nên EF coi là bắt buộc — seed thiếu ba cột đó là DbUpdateException.
    // Test ở repo này xanh trong khi mang sang solution thật thì đỏ. Đã gộp lại.

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
