// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution thật đã có bảng mã role. ĐỪNG chép đè, kiểm tra lại giá trị.
//
// GIÁ TRỊ SỐ DƯỚI ĐÂY LÀ PHỎNG ĐOÁN. Không sao: cả code lẫn test đều đọc qua
// hằng/hàm ở đây, mã thật có khác thì test vẫn đúng.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.StaticData.MP
{
    public static class Roles
    {
        public const decimal RoleBid = 1;
        public const decimal RoleMid = 2;
        public const decimal RoleTid = 3;

        // ── Role cán bộ VCB ────────────────────────────────────────────────────
        public const decimal RoleNghiepVu = 10; // mặc định khi job KHÔNG nằm trong white list
        public const decimal RoleTtv = 11;      // thanh toán viên — job trong white list, chưa có chức vụ
        public const decimal RoleKsv = 12;      // kiểm soát viên — job trong white list, có chức vụ

        /// <summary>Tai khoan quan tri — UAT van bat check password voi user nay.</summary>
        public const decimal RoleAdmin = 99;

        public const decimal RoleBca = 20;
        public const decimal RoleShlx = 30;
        public const decimal RoleApi = 40;

        public static bool IsAppRoles(decimal roleId) =>
            roleId is RoleBid or RoleMid or RoleTid;

        public static bool IsBcaRoles(decimal roleId) => roleId == RoleBca;

        public static bool IsVcbRoles(decimal roleId) =>
            roleId is RoleNghiepVu or RoleTtv or RoleKsv;

        public static bool IsShlxRoles(decimal roleId) => roleId == RoleShlx;

        public static bool IsApiRoles(decimal roleId) => roleId == RoleApi;

        public static string? GetRoleName(decimal roleId) => roleId switch
        {
            RoleBid => "BID",
            RoleMid => "MID",
            RoleTid => "TID",
            RoleNghiepVu => "Nghiệp vụ",
            RoleTtv => "Thanh toán viên",
            RoleKsv => "Kiểm soát viên",
            RoleBca => "Bộ công an",
            RoleShlx => "Sát hạch lái xe",
            RoleApi => "API",
            _ => null
        };
    }
}
