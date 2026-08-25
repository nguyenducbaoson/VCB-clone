using VcbPortalApi;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using VcbPortalApi.Controllers.Mobile;
using VcbPortalApi.DbContext;
using VcbPortalApi.Helpers;
using VcbPortalApi.Services;

namespace Tests.TestSupport
{
    /// <summary>
    /// Đồ nghề dùng chung cho test của MobilePartnerController.
    ///
    /// Controller này để hết logic trong action nên test phải đánh thẳng vào action,
    /// và phải dựng đủ bối cảnh mà action đọc tới:
    ///   CurrentUserName             <- HttpContext.User (claim sub)
    ///   TryGetBearerTokenExpiresUtc <- header Authorization: Bearer &lt;jwt&gt;
    ///   AppSettings.SigningCredentials, AppSettings.Issuer  <- static, gán trong Arrange
    ///
    /// Cả CurrentUserName lẫn TryGetBearerTokenExpiresUtc đều là protected nên KHÔNG
    /// gán trực tiếp được — chỉ điều khiển gián tiếp qua HttpContext như dưới đây.
    /// </summary>
    public static class MobileTestKit
    {
        /// <summary>Khoá ký chỉ dùng trong test. HS256 cần tối thiểu 256 bit.</summary>
        private static readonly SigningCredentials TestSigningCredentials = new(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes("khoa-ky-chi-dung-cho-test-day-du-32-byte!!")),
            SecurityAlgorithms.HmacSha256);

        public const string TestIssuer = "vcb-portal-test";

        /// <summary>
        /// Dựng controller kèm HttpContext giả.
        ///
        /// userName = null  -> không gắn claim nào, dùng để test nhánh Unauthorized.
        /// bearer   = null  -> không gắn header Authorization, test nhánh không đọc được hạn.
        /// </summary>
        public static MobilePartnerController CreateController(
            FrontendContext frontend,
            MerchantContext merchant,
            string? userName = "VATID001",
            DateTime? tokenExpiresUtc = null,
            bool coHeaderAuthorization = true,
            params Claim[] claimThem)
        {
            // Static nên phải gán trước mỗi lần chạy, nếu không CreateToken sẽ nổ.
            AppSettings.Issuer = TestIssuer;
            AppSettings.SigningCredentials = TestSigningCredentials;

            var controller = new MobilePartnerController(
                frontend,
                merchant,
                new MpAppUserStatusService(TestDb.Create(), NullLogger<MpAppUserStatusService>.Instance));

            var httpContext = new DefaultHttpContext();

            if (userName is not null)
            {
                // Dùng lại chính hằng mà ControllerCustom đọc, không hard-code chuỗi.
                // Key claim ở solution thật có khác thì test vẫn khớp, khỏi phải sửa.
                Claim[] claims = [new Claim(AppSettings.ClaimUserName, userName), .. claimThem];

                httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
            }

            if (coHeaderAuthorization)
            {
                var expires = tokenExpiresUtc ?? DateTime.UtcNow.AddMinutes(30);
                httpContext.Request.Headers.Authorization = "Bearer " + TaoBearerToken(expires);
            }

            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        /// <summary>Bearer token của user, chỉ cần đúng hạn vì action chỉ đọc exp.</summary>
        public static string TaoBearerToken(DateTime expiresUtc) =>
            new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = TestIssuer,
                Audience = "mobile-app",
                Expires = expiresUtc,
                SigningCredentials = TestSigningCredentials
            });

        // ── Đọc kết quả trả về ──────────────────────────────────────────────────
        // Ba hàm dưới đây là chỗ DUY NHẤT biết khuôn response. Khi mang sang solution
        // thật, khuôn MobileApiError khác thì chỉ sửa ở đây, không phải sửa 14 test.

        public static void AssertLoi(IActionResult result, string maLoiMongDoi)
        {
            var ok = Assert.IsType<OkObjectResult>(result);
            var body = Assert.IsType<MobileApiResult>(ok.Value);

            Assert.Equal("error", body.Status);
            Assert.Equal(maLoiMongDoi, body.Code);
        }

        public static void AssertUnauthorized(IActionResult result)
        {
            var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
            var body = Assert.IsType<MobileApiResult>(unauthorized.Value);

            Assert.Equal("Unauthorized", body.Code);
        }

        /// <summary>Bóc token trong response thành công rồi trả về map claim.</summary>
        public static Dictionary<string, string> DocClaimTuToken(IActionResult result)
        {
            var ok = Assert.IsType<OkObjectResult>(result);
            var body = Assert.IsType<MobileApiResult>(ok.Value);

            Assert.Equal("success", body.Status);

            var token = Assert.IsType<string>(body.Data!["token"]);
            var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);

            // Claim trùng key thì lấy giá trị đầu — action không sinh key trùng.
            return jwt.Claims
                .GroupBy(c => c.Type)
                .ToDictionary(g => g.Key, g => g.First().Value);
        }

        public static JsonWebToken DocToken(IActionResult result)
        {
            var ok = Assert.IsType<OkObjectResult>(result);
            var body = Assert.IsType<MobileApiResult>(ok.Value);
            var token = Assert.IsType<string>(body.Data!["token"]);

            return new JsonWebTokenHandler().ReadJsonWebToken(token);
        }
    }
}
