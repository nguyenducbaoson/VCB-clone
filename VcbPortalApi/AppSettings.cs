// FILE KHUNG - solution that da co AppSettings.cs o goc project. DUNG chep de.
namespace VcbPortalApi
{
    public static class AppSettings
    {
        /// <summary>
        /// Secret key HMAC ky ban tin gui VCB SSO. O solution that nap tu cau hinh
        /// luc khoi dong - KHONG hard-code gia tri that vao source.
        /// </summary>
        public static string SsoPrdHmacSecretKey { get; set; } = string.Empty;

        public static void Load(IConfiguration configuration)
        {
            SsoPrdHmacSecretKey = configuration["AppSettings:SsoPrdHmacSecretKey"] ?? string.Empty;
        }
    }
}
