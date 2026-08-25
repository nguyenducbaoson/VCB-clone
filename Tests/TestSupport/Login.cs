using System.Net.Http.Headers;

namespace Tests.TestSupport
{
    /// <summary>
    /// Đăng nhập lấy bearer token, gọi MỘT LẦN cho cả phiên chạy test.
    ///
    /// Nhờ vậy không phải copy token vào biến môi trường thủ công, và không dính
    /// chuyện token hết hạn giữa chừng.
    ///
    /// ─────────────────────────────────────────────────────────────────────────
    /// BA HẰNG DƯỚI ĐÂY LÀ PHỎNG ĐOÁN — SỬA CHO KHỚP API LOGIN THẬT.
    /// Chạy lần đầu mà 401 hàng loạt thì kiểm tra đúng ba chỗ này trước:
    ///   1. LoginPath          — đường dẫn endpoint login
    ///   2. UserField/PassField — tên field trong body
    ///   3. TokenField          — tên field chứa token trong response
    /// ─────────────────────────────────────────────────────────────────────────
    /// </summary>
    public static class Login
    {
        private const string LoginPath = "ma/login";
        private const string UserField = "UserName";
        private const string PassField = "Password";
        private const string TokenField = "token";

        /// <summary>Gửi form urlencoded. API login nhận JSON thì đổi thành true.</summary>
        private const bool SendAsForm = true;

        // Lazy<Task> đảm bảo chỉ đăng nhập đúng một lần dù nhiều test chạy cùng lúc.
        private static readonly Lazy<Task<string>> Cached = new(FetchAsync);

        public static Task<string> TokenAsync() => Cached.Value;

        private static async Task<string> FetchAsync()
        {
            // Đặt sẵn VCB_API_TOKEN thì dùng luôn, bỏ qua đăng nhập. Hữu ích khi API
            // login đang hỏng, hoặc muốn test bằng một user cụ thể đã có token.
            if (ApiEnv.PresetToken is { } preset) return preset;

            var baseUrl = ApiEnv.BaseUrl(ApiEnv.VcbPortalApi)
                ?? throw new InvalidOperationException($"Chua dat {ApiEnv.VcbPortalApi}.");

            using var http = new HttpClient { BaseAddress = new Uri(baseUrl + "/") };

            HttpContent body = SendAsForm
                ? new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    [UserField] = ApiEnv.Username!,
                    [PassField] = ApiEnv.Password!
                })
                : System.Net.Http.Json.JsonContent.Create(new Dictionary<string, string>
                {
                    [UserField] = ApiEnv.Username!,
                    [PassField] = ApiEnv.Password!
                });

            using var response = await http.PostAsync(LoginPath, body);
            var raw = await response.Content.ReadAsStringAsync();

            var result = new ApiResult("POST", baseUrl + "/" + LoginPath, response.StatusCode, raw);
            var token = result.Field(TokenField);

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException(
                    $"Dang nhap that bai, khong lay duoc token.\n{result.Describe}\n\n" +
                    $"Kiem tra trong Login.cs: LoginPath='{LoginPath}', " +
                    $"UserField='{UserField}', PassField='{PassField}', TokenField='{TokenField}'.");
            }

            return token;
        }

        /// <summary>Gắn bearer token vào một request.</summary>
        public static async Task AttachAsync(HttpRequestMessage request) =>
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync());
    }
}
