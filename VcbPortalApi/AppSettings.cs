using Microsoft.IdentityModel.Tokens;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG - solution that da co AppSettings.cs o goc project. DUNG chep de.
// Chi khai lai dung khoa ma luong SSO / phat token dung toi.
//
// Deu la static: TEST PHAI GAN GIA TRI TRONG ARRANGE, neu khong SigningCredentials
// null se lam JsonWebTokenHandler.CreateToken nem exception.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi
{
    public static class AppSettings
    {
        /// <summary>
        /// Secret key HMAC ky ban tin gui VCB SSO. O solution that nap tu cau hinh
        /// luc khoi dong - KHONG hard-code gia tri that vao source.
        /// </summary>
        public static string SsoPrdHmacSecretKey { get; set; } = string.Empty;

        /// <summary>Issuer dat vao token phat cho partner SDK.</summary>
        public static string Issuer { get; set; } = string.Empty;

        /// <summary>Khoa ky token partner SDK.</summary>
        public static SigningCredentials? SigningCredentials { get; set; }

        // ── Ten claim ──────────────────────────────────────────────────────────
        // ControllerCustom doc claim qua cac hang nay chu khong hard-code chuoi.
        // Test cung dung lai chinh cac hang nay khi dung ClaimsIdentity, nen khong
        // phu thuoc gia tri cu the - key co doi thi test van khop.
        // GIA TRI DUOI DAY LA PHONG DOAN, ban that co the khac. Khong sao: mien la
        // ca code lan test cung doc mot cho.
        public const string ClaimUserName = "username";
        public const string ClaimUserFullName = "user_full_name";
        public const string ClaimSessionId = "session_id";
        public const string ClaimRoleId = "role_id";
        public const string ClaimBranchId = "branch_id";
        public const string ClaimBid = "bid";
        public const string ClaimMid = "mid";
        public const string ClaimTid = "tid";
        public const string ClaimJd = "jd";
        public const string ClaimDeviceToken = "device_token";

        public static void Load(IConfiguration configuration)
        {
            SsoPrdHmacSecretKey = configuration["AppSettings:SsoPrdHmacSecretKey"] ?? string.Empty;
            Issuer = configuration["AppSettings:Issuer"] ?? string.Empty;
        }
    }
}
