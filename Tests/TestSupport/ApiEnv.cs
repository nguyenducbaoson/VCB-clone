namespace Tests.TestSupport
{
    /// <summary>
    /// Cấu hình môi trường cần test. Lấy hết từ BIẾN MÔI TRƯỜNG, không có gì trong source.
    ///
    ///   VCB_API_BASEURL    http://localhost:5000/api/v1   API dang chay o dau
    ///   ADDREDIS_BASEURL   http://localhost:5001          cho AddRedis
    ///   VCB_API_USERNAME   user de dang nhap lay token
    ///   VCB_API_PASSWORD   mat khau
    ///   VCB_API_TOKEN      (tuy chon) dat san token, bo qua buoc dang nhap
    ///
    /// BASEURL chỉ có nghĩa là "API đang chạy ở đâu" — local hay UAT đều được.
    /// Chạy local là trường hợp thường ngày; trước khi deploy thì đổi sang UAT.
    ///
    /// CHỈ đặt ở đây những gì ĐỔI THEO MÔI TRƯỜNG hoặc là BÍ MẬT.
    ///
    /// Dữ liệu test (mid, tid, partner code…) là HẰNG SỐ trong chính file test —
    /// chúng gắn với endpoint chứ không gắn với môi trường, và không phải bí mật.
    /// Nhét hết vào biến môi trường thì 20 endpoint sẽ thành 50 biến, không ai nhớ nổi
    /// và quên một cái là test đỏ với thông báo khó hiểu.
    ///
    /// Service nào chưa đặt base URL thì test của service đó SKIP, các service khác
    /// vẫn chạy bình thường.
    /// </summary>
    public static class ApiEnv
    {
        // Tên biến môi trường cũng là "khoá" của service. Dùng làm tham số cho
        // [ApiFact] và ApiClient: [ApiFact(ApiEnv.AddRedis)].
        public const string VcbPortalApi = "VCB_API_BASEURL";
        public const string AddRedis = "ADDREDIS_BASEURL";

        public static string? Username { get; } = Read("VCB_API_USERNAME");
        public static string? Password { get; } = Read("VCB_API_PASSWORD");

        /// <summary>
        /// Token đặt sẵn, để bỏ qua bước đăng nhập. Bình thường để trống — bộ test tự
        /// gọi API login (xem <see cref="Login"/>), nhờ vậy không phải copy token thủ
        /// công và không dính chuyện token hết hạn giữa chừng.
        /// </summary>
        public static string? PresetToken { get; } = Read("VCB_API_TOKEN");

        /// <summary>Base URL của một service. null nếu chưa cấu hình.</summary>
        public static string? BaseUrl(string service) => Read(service)?.TrimEnd('/');

        /// <summary>
        /// Đủ điều kiện chạy test của service này chưa: có base URL, và có cách lấy
        /// token (username+password, hoặc token đặt sẵn).
        /// </summary>
        public static bool IsReady(string service) =>
            BaseUrl(service) is not null && CanAuthenticate;

        private static bool CanAuthenticate =>
            PresetToken is not null || (Username is not null && Password is not null);

        public static string SkipReason(string service)
        {
            if (BaseUrl(service) is null)
                return $"Chua dat {service} nen bo qua test cua service nay. " +
                       $"Dat bien do (tro toi API dang chay) roi chay lai.";

            return "Chua dat VCB_API_USERNAME va VCB_API_PASSWORD nen khong dang nhap " +
                   "lay token duoc. Dat 2 bien do, hoac dat san VCB_API_TOKEN.";
        }

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
