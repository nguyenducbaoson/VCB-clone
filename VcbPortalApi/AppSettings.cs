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

        // ── Đồng bộ cán bộ VCB (FepController) ─────────────────────────────────

        /// <summary>
        /// Danh sách mã JD được cấp role giao dịch (TTV/KSV). Ngoài danh sách này
        /// thì user chỉ là RoleNghiepVu. So khớp bằng ToUpper().Trim().
        ///
        /// KHAI ĐÚNG NHƯ SOLUTION THẬT: <c>static readonly List</c>. `readonly` chỉ
        /// khoá việc gán lại tham chiếu, KHÔNG khoá nội dung — Load() nạp bằng
        /// Clear()+AddRange(), test cũng phải mutate chứ không gán lại được.
        /// </summary>
        public static readonly List<string> JdWhiteList = [];

        /// <summary>
        /// Tên ghi vào cột USER_UPDATE khi hệ thống tự đồng bộ, không phải người.
        ///
        /// KHAI ĐÚNG NHƯ SOLUTION THẬT: <c>const</c> — không gán được, kể cả trong test.
        /// GIÁ TRỊ LÀ PHỎNG ĐOÁN. Không sao: code lẫn test đều đọc qua chính hằng này,
        /// sửa lại cho khớp bản thật thì test vẫn xanh.
        /// </summary>
        public const string SystemUser = "SYSTEM";

        /// <summary>
        /// Tài khoản quản trị. Ở UAT, mọi user KHÁC tài khoản này đều được bỏ qua
        /// bước check password — xem nhánh <c>BuildSettings.IsUat</c> trong Authenticate.
        /// GIÁ TRỊ LÀ PHỎNG ĐOÁN; code lẫn test đều đọc qua chính hằng này.
        /// </summary>
        public static readonly string AdminUsername = "ADMIN";

        /// <summary>
        /// Độ dài salt (byte) — <c>Crypto.GenerateSalt()</c> đọc từ đây.
        /// GIÁ TRỊ LÀ PHỎNG ĐOÁN; không test nào phụ thuộc con số này.
        /// </summary>
        public const int SaltLength = 16;

        /// <summary>
        /// FILE KHUNG — bản thật dùng NLog. Ở đây chỉ cần đúng một hàm
        /// <c>Error(Exception)</c> mà UserActionLogHelper gọi trong khối catch.
        /// Mặc định nuốt luôn; test nào cần thì gán lại để đọc.
        /// </summary>
        public interface IAppLogger
        {
            void Error(Exception exception);
            void Warn(string message);
        }

        private sealed class NullLogger : IAppLogger
        {
            public void Error(Exception exception) { }
            public void Warn(string message) { }
        }

        public static IAppLogger Logger { get; set; } = new NullLogger();

        public static void Load(IConfiguration configuration)
        {
            SsoPrdHmacSecretKey = configuration["AppSettings:SsoPrdHmacSecretKey"] ?? string.Empty;
            Issuer = configuration["AppSettings:Issuer"] ?? string.Empty;
        }
    }
}
