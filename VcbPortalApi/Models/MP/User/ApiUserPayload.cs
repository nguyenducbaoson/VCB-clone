// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — chép nguyên văn từ ảnh code thật (dòng 37 → 53). ĐỪNG chép đè.
//
// Hai trường dễ hiểu nhầm vì tên không nói ra nghĩa:
//   Company  → TERMINAL_ID của user SHLX. Nhánh SHLX của CreateUsers viết
//              `TerminalId = user.Company.ToUpper().Trunc(10).Trim()`.
//   Password → để null thì CreateUsers tự sinh theo nhóm role
//              ("Shlx@yyMMdd", "bca@yyyy", "vcbp@yyyy").
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Models.MP.User
{
    public class ApiUserPayload
    {
        public string UserName { get; set; } = null!;
        public string? Password { get; set; } = null!;
        public decimal? RoleId { get; set; }
        public decimal? BranchId { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Mobile { get; set; }
        public string? Company { get; set; }
        public decimal? Bid { get; set; }
        public decimal? Mid { get; set; }
        public decimal? Tid { get; set; }
        public decimal? Tinh { get; set; }
        public decimal? Huyen { get; set; }
        public decimal? Xa { get; set; }
    }
}
