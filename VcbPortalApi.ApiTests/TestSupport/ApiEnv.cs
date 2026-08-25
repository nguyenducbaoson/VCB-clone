namespace VcbPortalApi.ApiTests.TestSupport
{
    /// <summary>
    /// Cấu hình môi trường cần test. Lấy hết từ BIẾN MÔI TRƯỜNG, không có gì trong source.
    ///
    ///   VCB_API_BASEURL   bắt buộc  https://uat-host/api/v1
    ///   VCB_API_TOKEN     bắt buộc  bearer token của một user đã đăng nhập
    ///   VCB_API_MID       tuỳ       mid dùng cho test (user role BID mới cần)
    ///   VCB_API_TID       tuỳ       tid dùng cho test
    ///   VCB_API_PARTNER   tuỳ       partner code, mặc định PHONEPOS
    ///
    /// Đổi môi trường = đổi biến, không sửa code. Cùng một bộ test chĩa vào local,
    /// UAT hay production đều được.
    /// </summary>
    public static class ApiEnv
    {
        public static string? BaseUrl { get; } = Doc("VCB_API_BASEURL")?.TrimEnd('/');
        public static string? Token { get; } = Doc("VCB_API_TOKEN");

        public static string Partner { get; } = Doc("VCB_API_PARTNER") ?? "PHONEPOS";
        public static string? Mid { get; } = Doc("VCB_API_MID");
        public static string? Tid { get; } = Doc("VCB_API_TID");

        public static bool IsConfigured =>
            !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(Token);

        public static string LyDoSkip =>
            "Chua dat VCB_API_BASEURL va VCB_API_TOKEN nen bo qua test goi API that. " +
            "Dat 2 bien do roi chay lai de test tren UAT.";

        private static string? Doc(string ten) =>
            Environment.GetEnvironmentVariable(ten) is { } v && !string.IsNullOrWhiteSpace(v)
                ? v.Trim()
                : null;
    }

    /// <summary>Như [Fact] nhưng tự SKIP khi chưa cấu hình môi trường.</summary>
    public sealed class ApiFactAttribute : FactAttribute
    {
        public ApiFactAttribute()
        {
            if (!ApiEnv.IsConfigured) Skip = ApiEnv.LyDoSkip;
        }
    }

    /// <summary>Bản [Theory] tương ứng.</summary>
    public sealed class ApiTheoryAttribute : TheoryAttribute
    {
        public ApiTheoryAttribute()
        {
            if (!ApiEnv.IsConfigured) Skip = ApiEnv.LyDoSkip;
        }
    }
}
