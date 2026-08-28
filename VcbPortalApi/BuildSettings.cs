// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — chép nguyên văn từ ảnh code thật. ĐỪNG chép đè.
//
// HỆ QUẢ QUAN TRỌNG CHO TEST: `Env` là `private const`, tức HẰNG LÚC BIÊN DỊCH.
// Không có cách nào đổi môi trường lúc chạy — muốn chạy nhánh khác thì phải sửa
// dòng đó rồi build lại. Và giá trị đang commit trong source là BuildEnv.Dev,
// nên IsDev == true.
//
// Trong FepController.Authenticate, IsDev == true làm HAI KHỐI thành code chết:
//   - `if (IsUat || IsDev)` → captcha KHÔNG BAO GIỜ được kiểm
//   - `if (IsDev || ...)`   → mật khẩu KHÔNG BAO GIỜ được kiểm
// Xem FepControllerTests, phần "Nhánh không chạm tới được".
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi
{
    public class BuildSettings
    {
        private const BuildEnv Env = BuildEnv.Dev;
        public const string MainEndpoint = "apimp";
        public const string FixedEndpoint = "apimp";

        //DEV+UAT apimp
        //Pilot apivp
        //PROD apimp apibca

        public const string Version = "2.0.0.5";
        public const string VersionDate = "10/08/2026 23:30";
        public const string MinAppVersion = "1.0.3";

        public const bool Swagger = true;
        public const bool StressTest = false;
        public const bool Debug = false;

        public const string ReportEndpoint = FixedEndpoint + "/" + "DXXRDV";

        public enum BuildEnv
        {
            Dev,
            Uat,
            Pilot,
            Prod,
        }

        public static bool IsDev => Env == BuildEnv.Dev;
        public static bool IsUat => Env == BuildEnv.Uat;
        public static bool IsPilot => Env == BuildEnv.Pilot;
        public static bool IsProd => Env == BuildEnv.Prod;
        public static string FilesLocation => AppDomain.CurrentDomain.BaseDirectory + "Files\\" + Env.ToString().ToLower() + "\\";
    }
}
