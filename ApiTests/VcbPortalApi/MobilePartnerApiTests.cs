using System.Net;
using ApiTests.TestSupport;

namespace ApiTests.VcbPortalApi
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
        public async Task IssueSsoToken_KhongGuiToken_TraVe401()
        {
            var api = new ApiClient(token: null);   // không gắn Authorization

            var res = await api.PostFormAsync(Endpoint,
                ("PartnerCode", ApiEnv.Partner),
                ("Tid", ApiEnv.Tid));

            Assert.True(res.Status == HttpStatusCode.Unauthorized,
                $"Goi khong co token phai bi tu choi 401.\n{res.MoTa}");
        }

        [ApiFact]
        public async Task IssueSsoToken_TokenRac_TraVe401()
        {
            var api = new ApiClient(token: "day-khong-phai-jwt");

            var res = await api.PostFormAsync(Endpoint,
                ("PartnerCode", ApiEnv.Partner),
                ("Tid", ApiEnv.Tid));

            Assert.True(res.Status == HttpStatusCode.Unauthorized,
                $"Token rac phai bi tu choi 401.\n{res.MoTa}");
        }

        // ── 2. Đầu vào sai ──────────────────────────────────────────────────────

        [ApiTheory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task IssueSsoToken_ThieuPartnerCode_TraVeLoiPartnerCodeEmpty(string? partnerCode)
        {
            var api = new ApiClient();

            var res = await api.PostFormAsync(Endpoint,
                ("PartnerCode", partnerCode),
                ("Tid", ApiEnv.Tid));

            Assert.True(res.Field("code")?.Contains("PartnerCode", StringComparison.OrdinalIgnoreCase) == true,
                $"Thieu partnerCode phai tra ma loi PartnerCodeEmpty.\n{res.MoTa}");
        }

        [ApiFact]
        public async Task IssueSsoToken_ThieuTid_KhongPhatToken()
        {
            var api = new ApiClient();

            var res = await api.PostFormAsync(Endpoint,
                ("PartnerCode", ApiEnv.Partner));   // không gửi Tid

            Assert.True(res.Field("token") is null,
                $"Thieu tid ma van phat token la sai.\n{res.MoTa}");
        }

        // ── 3. Đường thành công ─────────────────────────────────────────────────

        [ApiFact]
        public async Task IssueSsoToken_HopLe_TraVeToken()
        {
            var api = new ApiClient();

            var res = await api.PostFormAsync(Endpoint,
                ("PartnerCode", ApiEnv.Partner),
                ("Mid", ApiEnv.Mid),
                ("Tid", ApiEnv.Tid));

            Assert.True(res.Status == HttpStatusCode.OK, $"Mong doi HTTP 200.\n{res.MoTa}");
            Assert.False(string.IsNullOrWhiteSpace(res.Field("token")),
                $"Response khong co token.\n{res.MoTa}");
        }

        [ApiFact]
        public async Task IssueSsoToken_TokenTraVe_CoDayDuClaim()
        {
            var api = new ApiClient();

            var res = await api.PostFormAsync(Endpoint,
                ("PartnerCode", ApiEnv.Partner),
                ("Mid", ApiEnv.Mid),
                ("Tid", ApiEnv.Tid));

            var claims = Jwt.DocClaim(res.Field("token"));

            Assert.True(claims.Count > 0, $"Khong doc duoc claim trong token.\n{res.MoTa}");
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
        public async Task IssueSsoToken_TokenPartnerKhongSongLauHonTokenUser()
        {
            var api = new ApiClient();

            var res = await api.PostFormAsync(Endpoint,
                ("PartnerCode", ApiEnv.Partner),
                ("Mid", ApiEnv.Mid),
                ("Tid", ApiEnv.Tid));

            var hanPartner = Jwt.HanUtc(res.Field("token"));
            var hanUser = Jwt.HanUtc(ApiEnv.Token);

            Assert.True(hanPartner is not null, $"Token tra ve khong co han (exp).\n{res.MoTa}");
            Assert.True(hanUser is not null,
                "VCB_API_TOKEN khong doc duoc han — kiem tra lai token dat trong bien moi truong.");

            Assert.True(hanPartner <= hanUser!.Value.AddSeconds(2),
                $"Token partner het han {hanPartner:O}, sau ca token user {hanUser:O}. " +
                $"Thu hoi session roi ma partner SDK van dung duoc.");
        }
    }
}
