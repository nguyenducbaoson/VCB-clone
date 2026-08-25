using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Tests.TestSupport
{
    /// <summary>
    /// Gọi API thật qua HTTP. Mọi test đều đi qua đây.
    ///
    /// HttpClient được DÙNG CHUNG cho cả phiên chạy test, mỗi service một cái.
    /// Đừng đổi thành tạo mới mỗi lần: mỗi HttpClient giữ một connection pool riêng,
    /// socket đóng rồi vẫn nằm ở TIME_WAIT khoảng 4 phút, chạy vài chục test là cạn
    /// cổng và bắt đầu lỗi "address in use" rải rác — rất khó lần ra.
    ///
    /// Vì client dùng chung nên KHÔNG đặt Authorization mặc định trên nó; token gắn
    /// theo từng request. Nhờ vậy test nhánh 401 (không token / token sai) vẫn dùng
    /// chung một client với các test khác.
    /// </summary>
    public sealed class ApiClient
    {
        private static readonly ConcurrentDictionary<string, HttpClient> SharedClients = new();

        private readonly HttpClient _http;
        private readonly string? _token;

        /// <param name="service">Hằng trong <see cref="ApiEnv"/>. Mặc định VcbPortalApi.</param>
        /// <param name="token">
        /// Bỏ trống = dùng token trong biến môi trường.
        /// null     = KHÔNG gắn header Authorization, để test nhánh 401.
        /// chuỗi    = dùng đúng token đó.
        /// </param>
        public ApiClient(string service = ApiEnv.VcbPortalApi, string? token = "")
        {
            _http = SharedClients.GetOrAdd(service, key => new HttpClient
            {
                BaseAddress = new Uri(ApiEnv.BaseUrl(key) + "/"),
                Timeout = TimeSpan.FromSeconds(30)
            });

            _token = token == "" ? ApiEnv.Token : token;
        }

        /// <summary>POST dạng x-www-form-urlencoded. Bỏ qua field có giá trị null.</summary>
        public Task<ApiResult> PostFormAsync(string path, params (string Name, string? Value)[] fields) =>
            SendAsync(new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = new FormUrlEncodedContent(
                    fields.Where(f => f.Value is not null)
                          .Select(f => new KeyValuePair<string, string>(f.Name, f.Value!)))
            });

        public Task<ApiResult> PostJsonAsync(string path, object body) =>
            SendAsync(new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = JsonContent.Create(body)
            });

        public Task<ApiResult> GetAsync(string path) =>
            SendAsync(new HttpRequestMessage(HttpMethod.Get, path));

        private async Task<ApiResult> SendAsync(HttpRequestMessage request)
        {
            using (request)
            {
                if (_token is not null)
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

                using var response = await _http.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                return new ApiResult(
                    Method: request.Method.Method,
                    Path: request.RequestUri?.ToString(),
                    Status: response.StatusCode,
                    Body: body);
            }
        }
    }

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
