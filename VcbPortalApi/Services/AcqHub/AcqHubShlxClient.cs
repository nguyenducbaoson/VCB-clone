using System.Text;
using Microsoft.Extensions.Options;
using VcbPortalApi.Helpers;
using VcbPortalApi.Models.AcqHub;

namespace VcbPortalApi.Services.AcqHub
{
    public interface IAcqHubShlxClient
    {
        Task<ShlxConfigResponse> ConfigAsync(List<ShlxConfigItem> items, CancellationToken ct = default);
    }

    /// <summary>
    /// Gọi /api/acqhub/configpartner/v1/shlxconfig của ACQHUB.
    ///
    /// Phong bì dựng bằng <see cref="AcqRequest"/> — LỚP SẴN CÓ dùng chung cho mọi
    /// endpoint ACQHUB. Không tự tính checkSum ở đây: hai chỗ tính là hai chỗ phải
    /// nhớ sửa khi ACQHUB đổi công thức, và lần quên đầu tiên sẽ là lần luồng này
    /// bị từ chối mà không ai hiểu tại sao.
    ///
    /// TRONG SOLUTION THẬT: bỏ hẳn lớp này, gọi thẳng SendAcqRequest của
    /// ControllerCustom — nó làm đúng việc này và đang chạy ở 13 chỗ khác.
    /// Xem THEM-VAO-AcqhParamController.md.
    ///
    /// VÌ SAO KHÔNG ĐỂ ANGULAR GỌI THẲNG ACQHUB:
    ///   - checkSum tính từ khoá bí mật. Để khoá trong JavaScript thì ai mở DevTools
    ///     cũng lấy được rồi tự đăng ký terminal giả.
    ///   - Host ACQHUB là IP nội bộ, trình duyệt người dùng không tới được.
    ///   - ACQHUB không bật CORS cho domain portal.
    /// </summary>
    public sealed class AcqHubShlxClient(
        HttpClient http,
        IOptions<AcqHubShlxOptions> options,
        ILogger<AcqHubShlxClient> logger) : IAcqHubShlxClient
    {
        private readonly AcqHubShlxOptions _options = options.Value;

        public async Task<ShlxConfigResponse> ConfigAsync(
            List<ShlxConfigItem> items, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_options.BaseUrl))
                throw new InvalidOperationException(
                    "Thieu AcqHub:BaseUrl trong appsettings cua moi truong dang chay.");

            var url = _options.ShlxConfigUrl;

            // apiname = đoạn cuối URL viết thường ("shlxconfig"). Nó nằm TRONG chuỗi
            // ký, nên gọi sai URL là checkSum sai theo — không thể vô tình gọi nhầm
            // chỗ mà vẫn qua được.
            var request = new AcqRequest(
                ApiNameFromUrl(url), new ShlxConfigRequest { items = items });

            var res = await http.PostAsync(
                url,
                new StringContent(request.ToJsonString(), Encoding.UTF8,
                    new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")),
                ct);

            res.EnsureSuccessStatusCode();

            var parsed = await res.Content.ReadFromJsonAsync<ShlxConfigResponse>(ct)
                ?? throw new HttpRequestException("Response ACQHUB rong.");

            // results rỗng nghĩa là ACQHUB từ chối trước khi xử lý terminal nào —
            // thường do checkSum sai. Ghi lại requestId để nhờ ACQHUB tra log phía họ.
            if (parsed.results.Count == 0)
                logger.LogWarning(
                    "ACQHUB khong xu ly terminal nao. code={Code} message={Message} requestId={RequestId}",
                    parsed.code, parsed.message, parsed.requestId);

            return parsed;
        }

        internal static string ApiNameFromUrl(string url) =>
            url[(url.LastIndexOf('/') + 1)..].ToLower();
    }
}
