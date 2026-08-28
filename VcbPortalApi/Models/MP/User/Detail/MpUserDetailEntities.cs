// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution thật đã có các entity bảng chi tiết này (scaffold từ Oracle).
// Ở đây chỉ dựng đúng những cột mà constructor MpUserFull và FepController đọc/ghi.
// ĐỪNG chép đè.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Models.MP.User.Detail
{
    /// <summary>MP_VCB_USERS — cán bộ Vietcombank.</summary>
    public class MpVcbUser
    {
        public string UserName { get; set; } = string.Empty;
        public decimal? BranchId { get; set; }
        public int? MaDv { get; set; }
        public string? TenDv { get; set; }
        public int? MaPhong { get; set; }
        public string? TenPhong { get; set; }
        public DateTime? NamSinh { get; set; }
        public int? MaCb { get; set; }
        public string? MaJob { get; set; }
        public string? TenJob { get; set; }
        public int? MaChucVu { get; set; }
        public string? TenChucVu { get; set; }
    }

    /// <summary>MP_BCA_USERS — user Bộ Công an, gắn theo địa bàn.</summary>
    public class MpBcaUser
    {
        public string UserName { get; set; } = string.Empty;
        public decimal? Tinh { get; set; }
        public decimal? Huyen { get; set; }
        public decimal? Xa { get; set; }
    }

    /// <summary>MP_SHLX_USERS — user sát hạch lái xe, gắn theo terminal.</summary>
    public class MpShlxUser
    {
        public string UserName { get; set; } = string.Empty;
        public string? TerminalId { get; set; }
    }

    /// <summary>MP_API_USERS — user chỉ gọi API, gắn theo bid.</summary>
    public class MpApiUser
    {
        public string UserName { get; set; } = string.Empty;
        public decimal? Bid { get; set; }
    }

}
