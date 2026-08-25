using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace VcbPortalApi.UnitTests.Fixtures
{
    /// <summary>
    /// Dựng HttpContext giả cho mọi controller kế thừa ControllerCustom.
    ///
    /// Các property của ControllerCustom (CurrentUserName, CurrentUserRoleId,
    /// CurrentUserBid/Mid/Tid…) đều là <c>protected</c> và đọc từ
    /// <c>User.FindFirstValue(AppSettings.Claim*)</c>. Test không gán trực tiếp được —
    /// chỉ điều khiển gián tiếp bằng cách gắn đúng claim vào HttpContext.User.
    ///
    /// Claim key lấy từ chính hằng <c>AppSettings.Claim*</c>, không hard-code chuỗi,
    /// nên key có đổi thì test vẫn khớp.
    /// </summary>
    public static class TestHttpContext
    {
        /// <summary>Khoá ký chỉ dùng trong test. HS256 cần tối thiểu 256 bit.</summary>
        private static readonly SigningCredentials TestSigningCredentials = new(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes("khoa-ky-chi-dung-cho-test-day-du-32-byte!!")),
            SecurityAlgorithms.HmacSha256);

        public const string TestIssuer = "vcb-portal-test";

        /// <param name="userName">null = không gắn claim nào, để test nhánh Unauthorized.</param>
        /// <param name="coBearerToken">false = không gắn header Authorization.</param>
        /// <param name="claimThem">Claim bổ sung, ví dụ để giả lập token mang dữ liệu cũ.</param>
        public static ControllerContext Build(
            string? userName = "VATID001",
            DateTime? tokenExpiresUtc = null,
            bool coBearerToken = true,
            params Claim[] claimThem)
        {
            // AppSettings là static nên phải gán trước mỗi lần chạy, nếu không
            // CreateToken trong controller sẽ nổ vì SigningCredentials null.
            AppSettings.Issuer = TestIssuer;
            AppSettings.SigningCredentials = TestSigningCredentials;

            var httpContext = new DefaultHttpContext();

            if (userName is not null)
            {
                Claim[] claims = [new(AppSettings.ClaimUserName, userName), .. claimThem];
                httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
            }

            if (coBearerToken)
            {
                var expires = tokenExpiresUtc ?? DateTime.UtcNow.AddMinutes(30);
                httpContext.Request.Headers.Authorization = "Bearer " + CreateBearerToken(expires);
            }

            return new ControllerContext { HttpContext = httpContext };
        }

        /// <summary>Bearer token của user, chỉ cần đúng hạn vì controller chỉ đọc exp.</summary>
        private static string CreateBearerToken(DateTime expiresUtc) =>
            new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = TestIssuer,
                Audience = "mobile-app",
                Expires = expiresUtc,
                SigningCredentials = TestSigningCredentials
            });
    }
}
