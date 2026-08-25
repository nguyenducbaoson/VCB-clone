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

            _token = token;
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
                if (_token == "")
                    await Login.AttachAsync(request);   // đăng nhập một lần, cache lại
                else if (_token is not null)
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
}
