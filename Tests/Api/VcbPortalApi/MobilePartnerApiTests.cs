using System.Net;
using Tests.TestSupport;

// Namespace la Tests.Api chu khong phai Tests.Api.VcbPortalApi (theo ten thu muc):
// mot namespace ten VcbPortalApi long trong Tests.Api se che khuat namespace goc
// VcbPortalApi cua project API, lam cac using ben trong khong phan giai dung.
namespace Tests.Api
{
    /// <summary>
    /// POST /ma/partner/token — gọi API THẬT đang chạy trên môi trường đã cấu hình.
    ///
    /// MẪU CHUẨN: copy file này cho endpoint mới. Ba nhóm test, viết theo thứ tự:
    ///   1. Không có quyền   -> 401
    ///   2. Đầu vào sai      -> đúng mã lỗi nghiệp vụ
    ///   3. Đường thành công -> đúng dữ liệu trả về
    ///
    /// Test gọi API thật nên KHÔNG dựng được trạng thái DB tuỳ ý. Nhánh nào chỉ xảy
    /// ra khi dữ liệu trong DB ở trạng thái đặc biệt (user chưa có session, email
    /// rỗng…) thì không kiểm tra được từ đây — chấp nhận, đổi lại được chạy trên
    /// đúng stack thật: routing, [Authorize], middleware, Oracle, cấu hình.
    /// </summary>
    public class MobilePartnerApiTests
    {
        private const string Endpoint = "ma/partner/token";

        // ── 1. Không có quyền ───────────────────────────────────────────────────

        [ApiFact]
        public async Task IssueSsoToken_NoBearerToken_Returns401()
        {
            var api = new ApiClient(token: null);   // không gắn Authorization

            var result = await api.PostFormAsync(Endpoint,
                ("PartnerCode", ApiEnv.Partner),
                ("Tid", ApiEnv.Tid));

            Assert.True(result.Status == HttpStatusCode.Unauthorized,
                $"Goi khong co token phai bi tu choi 401.\n{result.Describe}");
        }

        [ApiFact]
        public async Task IssueSsoToken_MalformedBearerToken_Returns401()
        {
            var api = new ApiClient(token: "not-a-valid-jwt");

            var result = await api.PostFormAsync(Endpoint,
                ("PartnerCode", ApiEnv.Partner),
                ("Tid", ApiEnv.Tid));

            Assert.True(result.Status == HttpStatusCode.Unauthorized,
                $"Token rac phai bi tu choi 401.\n{result.Describe}");
        }

        // ── 2. Đầu vào sai ──────────────────────────────────────────────────────

        [ApiTheory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task IssueSsoToken_MissingPartnerCode_ReturnsPartnerCodeEmpty(string? partnerCode)
        {
            var api = new ApiClient();

            var result = await api.PostFormAsync(Endpoint,
                ("PartnerCode", partnerCode),
                ("Tid", ApiEnv.Tid));

            Assert.True(result.Field("code")?.Contains("PartnerCode", StringComparison.OrdinalIgnoreCase) == true,
                $"Thieu partnerCode phai tra ma loi PartnerCodeEmpty.\n{result.Describe}");
        }

        [ApiFact]
        public async Task IssueSsoToken_MissingTid_DoesNotIssueToken()
        {
            var api = new ApiClient();

            var result = await api.PostFormAsync(Endpoint,
                ("PartnerCode", ApiEnv.Partner));   // không gửi Tid

            Assert.True(result.Field("token") is null,
                $"Thieu tid ma van phat token la sai.\n{result.Describe}");
        }

        // ── 3. Đường thành công ─────────────────────────────────────────────────

        [ApiFact]
        public async Task IssueSsoToken_ValidRequest_ReturnsToken()
        {
            var api = new ApiClient();

            var result = await api.PostFormAsync(Endpoint,
                ("PartnerCode", ApiEnv.Partner),
                ("Mid", ApiEnv.Mid),
                ("Tid", ApiEnv.Tid));

            Assert.True(result.IsSuccess, $"Mong doi HTTP 2xx.\n{result.Describe}");
            Assert.False(string.IsNullOrWhiteSpace(result.Field("token")),
                $"Response khong co token.\n{result.Describe}");
        }

        [ApiFact]
        public async Task IssueSsoToken_ReturnedToken_ContainsExpectedClaims()
        {
            var api = new ApiClient();

            var result = await api.PostFormAsync(Endpoint,
                ("PartnerCode", ApiEnv.Partner),
                ("Mid", ApiEnv.Mid),
                ("Tid", ApiEnv.Tid));

            var claims = Jwt.ReadClaims(result.Field("token"));

            Assert.True(claims.Count > 0, $"Khong doc duoc claim trong token.\n{result.Describe}");
            Assert.True(claims.ContainsKey("session_id"), "Token thieu claim session_id.");
            Assert.True(claims.ContainsKey("partner_code"), "Token thieu claim partner_code.");
            Assert.Equal(ApiEnv.Partner, claims["partner_code"]);

            if (ApiEnv.Tid is { } tid)
                Assert.Equal(tid, claims.GetValueOrDefault("tid"));
        }

        /// <summary>
        /// Token cấp cho partner không được sống lâu hơn bearer token của user.
        /// Sai chỗ này là session thu hồi rồi mà partner SDK vẫn dùng được.
        /// </summary>
        [ApiFact]
        public async Task IssueSsoToken_PartnerTokenDoesNotOutliveUserToken()
        {
            var api = new ApiClient();

            var result = await api.PostFormAsync(Endpoint,
                ("PartnerCode", ApiEnv.Partner),
                ("Mid", ApiEnv.Mid),
                ("Tid", ApiEnv.Tid));

            var partnerExpiry = Jwt.ExpiresUtc(result.Field("token"));
            var userExpiry = Jwt.ExpiresUtc(ApiEnv.Token);

            Assert.True(partnerExpiry is not null, $"Token tra ve khong co han (exp).\n{result.Describe}");
            Assert.True(userExpiry is not null,
                "VCB_API_TOKEN khong doc duoc han — kiem tra lai token dat trong bien moi truong.");

            Assert.True(partnerExpiry <= userExpiry!.Value.AddSeconds(2),
                $"Token partner het han {partnerExpiry:O}, sau ca token user {userExpiry:O}. " +
                $"Thu hoi session roi ma partner SDK van dung duoc.");
        }
    }
}
