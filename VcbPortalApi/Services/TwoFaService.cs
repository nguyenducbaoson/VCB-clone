using Microsoft.Extensions.Options;
using VcbPortalApi.Models.TwoFa;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution thật đã có service này. ĐỪNG chép đè.
// Constructor, ba hằng tên HttpClient, IsEnabled và IsSmsNotifyEnabled được
// CHÉP NGUYÊN VĂN từ ảnh code thật. Các hàm gọi API 2FA thì bỏ, test không dùng.
//
// LƯU Ý KHI VIẾT TEST: `_options = options.Value;` — truyền null vào là NRE ngay
// trong constructor. Dùng Options.Create(new TwoFaOptions()), đừng Mock<IOptions<T>>.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Services
{
    public class TwoFaService
    {
        public const string OAuthHttpClientName = "TwoFaOAuth";
        public const string ApiHttpClientName = "TwoFaApi";
        public const string SmsNotifyHttpClientName = "SmsNotify";

        private readonly TwoFaOptions _options;
        private readonly SmsNotifyOptions _smsOptions;
        private readonly IHttpClientFactory _httpClientFactory;

        public TwoFaService(
            IOptions<TwoFaOptions> options,
            IOptions<SmsNotifyOptions> smsOptions,
            IHttpClientFactory httpClientFactory)
        {
            _options = options.Value;
            _smsOptions = smsOptions.Value;
            _httpClientFactory = httpClientFactory;
        }

        public bool IsEnabled =>
            _options.Enabled
            && !string.IsNullOrWhiteSpace(_options.BaseUrl)
            && !string.IsNullOrWhiteSpace(_options.ClientId)
            && !string.IsNullOrWhiteSpace(_options.ClientSecret);

        public bool IsSmsNotifyEnabled =>
            _smsOptions.Enabled
            && !string.IsNullOrWhiteSpace(_smsOptions.BaseUrl)
            && !string.IsNullOrWhiteSpace(_smsOptions.Username)
            && !string.IsNullOrWhiteSpace(_smsOptions.Password);
    }
}
