// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution thật đã có bảng mã role. ĐỪNG chép đè.
//
// CÁC DẢI SỐ LẤY TỪ COMMENT TRONG FrontendContext THẬT:
//     MpAppUsers   ROLEID 1 2 3
//     MpBcaUsers   ROLEID 21 22 23 24 25
//     MpShlxUsers  ROLEID 28 29
//     MpApiUsers   ROLEID 41
//     MpVcbUsers   ROLEID 11,12,13,19,31,32,35,51,52,53,54
//
// CÒN ĐOÁN: trong dải VCB thì số nào là nghiệp vụ / TTV / KSV / admin. Không sao —
// code lẫn test đều đọc qua hằng, gán lại đúng số là xong, không phải sửa test.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.StaticData.MP
{
    public static class Roles
    {
        // ── Mobile App (MP_APP_USERS) ──────────────────────────────────────────
        public const decimal RoleBid = 1;
        public const decimal RoleMid = 2;
        public const decimal RoleTid = 3;

        // ── Cán bộ VCB (MP_VCB_USERS) ──────────────────────────────────────────
        public const decimal RoleNghiepVu = 11; // job KHÔNG nằm trong white list
        public const decimal RoleTtv = 12;      // trong white list, chưa có chức vụ
        public const decimal RoleKsv = 13;      // trong white list, có chức vụ

        /// <summary>
        /// Tài khoản quản trị. PHẢI nằm trong nhóm VCB: Authenticate viết
        /// <c>UserType == UserType.VCB &amp;&amp; RoleId != Roles.RoleAdmin</c> — nếu
        /// admin không phải role VCB thì vế <c>!= RoleAdmin</c> là code chết.
        /// </summary>
        public const decimal RoleAdmin = 19;

        // ── Các loại còn lại ───────────────────────────────────────────────────
        public const decimal RoleBca = 21;
        public const decimal RoleShlx = 28;
        public const decimal RoleApi = 41;

        public static bool IsAppRoles(decimal roleId) =>
            roleId is RoleBid or RoleMid or RoleTid;

        public static bool IsBcaRoles(decimal roleId) =>
            roleId is 21 or 22 or 23 or 24 or 25;

        public static bool IsVcbRoles(decimal roleId) =>
            roleId is 11 or 12 or 13 or 19 or 31 or 32 or 35 or 51 or 52 or 53 or 54;

        public static bool IsShlxRoles(decimal roleId) =>
            roleId is 28 or 29;

        public static bool IsApiRoles(decimal roleId) => roleId == RoleApi;

        public static string? GetRoleName(decimal roleId) => roleId switch
        {
            RoleBid => "BID",
            RoleMid => "MID",
            RoleTid => "TID",
            RoleNghiepVu => "Nghiệp vụ",
            RoleTtv => "Thanh toán viên",
            RoleKsv => "Kiểm soát viên",
            RoleAdmin => "Quản trị",
            RoleBca => "Bộ công an",
            RoleShlx => "Sát hạch lái xe",
            RoleApi => "API",
            _ => null
        };
    }
}
