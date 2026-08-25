using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace VcbPortalApi.ApiTests.TestSupport
{
    /// <summary>
    /// Gọi API thật qua HTTP. Đây là toàn bộ "khung" — mọi test API đều đi qua đây.
    ///
    /// Mặc định gắn bearer token lấy từ VCB_API_TOKEN. Muốn test nhánh không có
    /// token hoặc token sai thì truyền token khác vào constructor.
    /// </summary>
    public sealed class ApiClient : IDisposable
    {
        private readonly HttpClient _http;

        /// <param name="token">
        /// null = KHÔNG gắn header Authorization (để test nhánh 401).
        /// Không truyền = dùng token trong biến môi trường.
        /// </param>
        public ApiClient(string? token = "", TimeSpan? timeout = null)
        {
            _http = new HttpClient
            {
                BaseAddress = new Uri(ApiEnv.BaseUrl + "/"),
                Timeout = timeout ?? TimeSpan.FromSeconds(30)
            };

            var tokenThucDung = token == "" ? ApiEnv.Token : token;

            if (!string.IsNullOrWhiteSpace(tokenThucDung))
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenThucDung);
        }

        /// <summary>POST dạng x-www-form-urlencoded. Bỏ qua field có giá trị null.</summary>
        public async Task<ApiResult> PostFormAsync(string path, params (string Ten, string? GiaTri)[] fields)
        {
            var noiDung = new FormUrlEncodedContent(
                fields.Where(f => f.GiaTri is not null)
                      .Select(f => new KeyValuePair<string, string>(f.Ten, f.GiaTri!)));

            return await GuiAsync(() => _http.PostAsync(path, noiDung));
        }

        public Task<ApiResult> PostJsonAsync(string path, object body) =>
            GuiAsync(() => _http.PostAsJsonAsync(path, body));

        public Task<ApiResult> GetAsync(string path) =>
            GuiAsync(() => _http.GetAsync(path));

        private static async Task<ApiResult> GuiAsync(Func<Task<HttpResponseMessage>> goi)
        {
            using var response = await goi();
            var body = await response.Content.ReadAsStringAsync();

            return new ApiResult(response.StatusCode, body);
        }

        public void Dispose() => _http.Dispose();
    }

    /// <summary>
    /// Kết quả một lời gọi. Giữ nguyên body dạng chuỗi để khi test fail còn đọc được
    /// API trả về cái gì.
    /// </summary>
    public sealed record ApiResult(HttpStatusCode Status, string Body)
    {
        /// <summary>
        /// Đọc một field trong JSON trả về, KHÔNG phân biệt hoa thường và tìm cả trong
        /// object con. Cố ý viết lỏng để không phụ thuộc khuôn response cụ thể —
        /// "code"/"Code"/"resCode" đều lấy được mà không phải sửa test.
        /// Không có field thì trả null.
        /// </summary>
        public string? Field(string ten)
        {
            if (string.IsNullOrWhiteSpace(Body)) return null;

            try
            {
                using var doc = JsonDocument.Parse(Body);
                return Tim(doc.RootElement, ten);
            }
            catch (JsonException)
            {
                return null;   // body không phải JSON
            }
        }

        private static string? Tim(JsonElement node, string ten)
        {
            if (node.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in node.EnumerateObject())
                {
                    if (string.Equals(p.Name, ten, StringComparison.OrdinalIgnoreCase))
                    {
                        return p.Value.ValueKind switch
                        {
                            JsonValueKind.String => p.Value.GetString(),
                            JsonValueKind.Null => null,
                            _ => p.Value.ToString()
                        };
                    }

                    if (Tim(p.Value, ten) is { } sau) return sau;
                }
            }
            else if (node.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in node.EnumerateArray())
                    if (Tim(item, ten) is { } sau) return sau;
            }

            return null;
        }

        /// <summary>Mô tả dùng trong thông báo lỗi: có đủ status và body để debug.</summary>
        public string MoTa => $"HTTP {(int)Status} {Status}\nBody: {CatBot(Body)}";

        private static string CatBot(string s) =>
            s.Length <= 1000 ? s : s[..1000] + $"... (con {s.Length - 1000} ky tu)";
    }
}
