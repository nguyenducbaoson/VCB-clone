// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution VcbPortalApi thật ĐÃ CÓ các entity này (scaffold từ Oracle).
// Dựng lại ở đây để repo build và test chạy được. ĐỪNG chép đè lên solution thật.
//
// Tên property theo quy tắc scaffold EF từ tên cột: USERNAME→Username,
// ROLE_ID→RoleId, PHONEPOS_STATUS→PhoneposStatus... Nếu entity thật đặt khác,
// compiler sẽ chỉ thẳng vào chỗ cần sửa.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Models.SSO
{
    /// <summary>MP_APP_USERS</summary>
    public class MpAppUser
    {
        public string? UserName { get; set; }
        public decimal? RoleId { get; set; }
        public decimal? Bid { get; set; }
        public decimal? Mid { get; set; }
        public decimal? Tid { get; set; }
        public string? FcmToken { get; set; }
        public string? Fid { get; set; }
        public string? DeviceId { get; set; }
        public string? Os { get; set; }
        public string? Note { get; set; }
        public decimal? BranchId { get; set; }

        // Ba cột trạng thái partner. NULL nghĩa là "Chưa đăng ký", không phải 0.
        public decimal? PhoneposStatus { get; set; }
        public decimal? VisaacceptStatus { get; set; }
        public decimal? FinoneStatus { get; set; }
    }

    /// <summary>MP_APP_PARTNER_CARD_REG. Một username có thể có nhiều dòng, nhiều partner.</summary>
    public class MpAppPartnerCardReg
    {
        /// <summary>
        /// CHỈ CÓ Ở BẢN KHUNG. Bảng thật không có khoá chính, nhưng EF bắt buộc entity
        /// phải có khoá thì mới Add() được dữ liệu mẫu lúc test. Code nghiệp vụ không dùng.
        /// </summary>
        public int RowId { get; set; }

        public string? UserName { get; set; }
        public decimal Bid { get; set; }
        public decimal Mid { get; set; }
        public decimal Tid { get; set; }
        public string? CardNumber { get; set; }
        public string? VcbToken { get; set; }

        /// <summary>VARCHAR2(2) — là chuỗi chứ không phải số. Đây là lý do phải parse.</summary>
        public string? Status { get; set; }

        public string? Partner { get; set; }
        public string? Channel { get; set; }
        public string? DeviceId { get; set; }
        public string? Note { get; set; }
        public DateTime CreateTime { get; set; }
        public DateTime? UpdateTime { get; set; }
    }

    /// <summary>MP_SSO_LOG</summary>
    public class MpSsoLog
    {
        public decimal Id { get; set; }
        public DateTime? CreateTime { get; set; }
        public string? Token { get; set; }
        public string? RealIp { get; set; }
        public string? UaPlatform { get; set; }
        public string? UaName { get; set; }
        public string? UserId { get; set; }
        public string? UserFullName { get; set; }
        public string? UserRole { get; set; }
        public string? UserOf { get; set; }
        public string? UserCif { get; set; }
        public decimal? Bid { get; set; }
        public string? Response { get; set; }
    }
}
