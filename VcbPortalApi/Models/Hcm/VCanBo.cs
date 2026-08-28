// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution thật đã có VCanBo. ĐỪNG chép đè.
//
// Namespace lấy theo dòng `using VcbPortalApi.Models.Hcm;` thấy ở đầu
// FepController.cs. HCM = hệ thống nhân sự; đây KHÔNG phải bảng của MP —
// FepController nhận về rồi đồng bộ sang MP_USERS_COMMON + MP_VCB_USERS.
//
// Chỉ dựng những cột mà InsertNewVcbUser / CheckModified đọc tới.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Models.Hcm
{
    public class VCanBo
    {
        public string? SamAccountName { get; set; }
        public string? HoTen { get; set; }
        public decimal? MaCn { get; set; }
        public string? Email { get; set; }
        public string? SdtDiDong { get; set; }
        public DateTime? NamSinh { get; set; }

        /// <summary>KHÔNG nullable: FepController gán thẳng vào MpUserFull.MaCb (int).</summary>
        public int MaCb { get; set; }

        public string? MaJob { get; set; }
        public string? TenJob { get; set; }
        public int? MaChucVu { get; set; }
        public string? TenChucVu { get; set; }
        public int MaDv { get; set; }
        public string? TenDv { get; set; }
        public int? MaPhong { get; set; }
        public string? TenPhong { get; set; }
    }
}
