namespace Tests.TestSupport
{
    /// <summary>
    /// Cấu hình môi trường cần test. Lấy hết từ BIẾN MÔI TRƯỜNG, không có gì trong source.
    ///
    /// Mỗi service một base URL riêng, nên một bộ test phục vụ được cả solution:
    ///
    ///   VCB_API_BASEURL    https://uat-host/api/v1     cho VcbPortalApi
    ///   ADDREDIS_BASEURL   https://uat-host:5001       cho AddRedis
    ///   VCB_API_TOKEN      bearer token cua user da dang nhap
    ///   VCB_API_MID        mid dung cho test (user role BID moi can)
    ///   VCB_API_TID        tid dung cho test
    ///   VCB_API_PARTNER    partner code, mac dinh PHONEPOS
    ///
    /// Service nào chưa đặt base URL thì test của service đó SKIP, các service khác
    /// vẫn chạy bình thường.
    ///
    /// Đổi môi trường = đổi biến, không sửa code. Cùng bộ test chĩa vào local, UAT
    /// hay production đều được.
    /// </summary>
    public static class ApiEnv
    {
        // Tên biến môi trường cũng là "khoá" của service. Dùng làm tham số cho
        // [ApiFact] và ApiClient: [ApiFact(ApiEnv.AddRedis)].
        public const string VcbPortalApi = "VCB_API_BASEURL";
        public const string AddRedis = "ADDREDIS_BASEURL";

        public static string? Token { get; } = Read("VCB_API_TOKEN");

        public static string Partner { get; } = Read("VCB_API_PARTNER") ?? "PHONEPOS";
        public static string? Mid { get; } = Read("VCB_API_MID");
        public static string? Tid { get; } = Read("VCB_API_TID");

        /// <summary>Base URL của một service. null nếu chưa cấu hình.</summary>
        public static string? BaseUrl(string service) => Read(service)?.TrimEnd('/');

        public static bool IsReady(string service) => BaseUrl(service) is not null;

        public static string SkipReason(string service) =>
            $"Chua dat {service} nen bo qua test cua service nay. " +
            $"Dat bien do (va VCB_API_TOKEN neu endpoint can dang nhap) roi chay lai.";

        private static string? Read(string name) =>
            Environment.GetEnvironmentVariable(name) is { } value && !string.IsNullOrWhiteSpace(value)
                ? value.Trim()
                : null;
    }

    /// <summary>
    /// Như [Fact] nhưng tự SKIP khi service tương ứng chưa cấu hình.
    /// Mặc định là VcbPortalApi; service khác thì <c>[ApiFact(ApiEnv.AddRedis)]</c>.
    /// </summary>
    public sealed class ApiFactAttribute : FactAttribute
    {
        public ApiFactAttribute(string service = ApiEnv.VcbPortalApi)
        {
            if (!ApiEnv.IsReady(service)) Skip = ApiEnv.SkipReason(service);
        }
    }

    /// <summary>Bản [Theory] tương ứng.</summary>
    public sealed class ApiTheoryAttribute : TheoryAttribute
    {
        public ApiTheoryAttribute(string service = ApiEnv.VcbPortalApi)
        {
            if (!ApiEnv.IsReady(service)) Skip = ApiEnv.SkipReason(service);
        }
    }
}
