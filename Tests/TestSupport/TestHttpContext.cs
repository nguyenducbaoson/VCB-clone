using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using VcbPortalApi;

namespace Tests.TestSupport
{
    /// <summary>
    /// Dựng HttpContext giả cho MỌI controller kế thừa ControllerCustom.
    ///
    /// Lý do cần: các property của ControllerCustom (CurrentUserName, CurrentUserRoleId,
    /// CurrentUserBid/Mid/Tid, CurrentUserSessionId) đều là `protected` và đọc từ
    /// `User.FindFirstValue(AppSettings.Claim*)`. Test KHÔNG gán trực tiếp được —
    /// chỉ điều khiển gián tiếp bằng cách gắn đúng claim vào HttpContext.User.
    ///
    /// Claim key lấy từ chính hằng `AppSettings.Claim*`, không hard-code chuỗi, nên
    /// key có đổi thì test vẫn khớp.
    ///
    /// Dùng cho controller mới chỉ cần một dòng:
    ///
    ///     var controller = new XController(ctx) { ControllerContext = Build(userName: "VATID001") };
    /// </summary>
    public static class TestHttpContext
    {
        /// <summary>Khoá ký chỉ dùng trong test. HS256 cần tối thiểu 256 bit.</summary>
        public static readonly SigningCredentials TestSigningCredentials = new(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes("khoa-ky-chi-dung-cho-test-day-du-32-byte!!")),
            SecurityAlgorithms.HmacSha256);

        public const string TestIssuer = "vcb-portal-test";

        /// <summary>
        /// Dựng ControllerContext kèm claim và bearer token.
        ///
        /// userName = null  -> không gắn claim nào, để test nhánh Unauthorized.
        /// coBearerToken = false -> không gắn header Authorization.
        /// </summary>
        public static ControllerContext Build(
            string? userName = "VATID001",
            decimal? roleId = null,
            decimal? bid = null,
            decimal? mid = null,
            decimal? tid = null,
            string? sessionId = null,
            DateTime? tokenExpiresUtc = null,
            bool coBearerToken = true,
            string? remoteIp = "10.0.0.5",
            params Claim[] claimThem)
        {
            // AppSettings là static nên phải gán trước mỗi lần chạy, nếu không
            // CreateToken trong controller sẽ nổ vì SigningCredentials null.
            AppSettings.Issuer = TestIssuer;
            AppSettings.SigningCredentials = TestSigningCredentials;

            var httpContext = new DefaultHttpContext();

            if (remoteIp is not null)
                httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(remoteIp);

            if (userName is not null)
            {
                var claims = new List<Claim> { new(AppSettings.ClaimUserName, userName) };

                if (roleId is not null) claims.Add(new Claim(AppSettings.ClaimRoleId, roleId.Value.ToString()));
                if (bid is not null) claims.Add(new Claim(AppSettings.ClaimBid, bid.Value.ToString()));
                if (mid is not null) claims.Add(new Claim(AppSettings.ClaimMid, mid.Value.ToString()));
                if (tid is not null) claims.Add(new Claim(AppSettings.ClaimTid, tid.Value.ToString()));
                if (sessionId is not null) claims.Add(new Claim(AppSettings.ClaimSessionId, sessionId));

                claims.AddRange(claimThem);

                httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
            }

            if (coBearerToken)
            {
                var expires = tokenExpiresUtc ?? DateTime.UtcNow.AddMinutes(30);
                httpContext.Request.Headers.Authorization = "Bearer " + TaoBearerToken(expires);
            }

            return new ControllerContext { HttpContext = httpContext };
        }

        /// <summary>Bearer token của user, chỉ cần đúng hạn vì controller chỉ đọc exp.</summary>
        public static string TaoBearerToken(DateTime expiresUtc) =>
            new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = TestIssuer,
                Audience = "mobile-app",
                Expires = expiresUtc,
                SigningCredentials = TestSigningCredentials
            });
    }
}
