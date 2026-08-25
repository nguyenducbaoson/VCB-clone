using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using VcbPortalApi.Controllers.Mobile;
using VcbPortalApi.DbContext;
using VcbPortalApi.Helpers;
using VcbPortalApi.Services;

namespace Tests.TestSupport
{
    /// <summary>
    /// Phần riêng của MobilePartnerController.
    ///
    /// Những gì DÙNG CHUNG cho mọi controller đã nằm ở TestHttpContext (claim, bearer
    /// token, AppSettings) và TestDb (DbContext bộ nhớ). File này chỉ còn hai thứ
    /// đặc thù: cách dựng controller, và cách đọc khuôn response của MobileApiError.
    ///
    /// Controller mới thì viết một file tương tự, ngắn cỡ này.
    /// </summary>
    public static class MobileTestKit
    {
        public static MobilePartnerController CreateController(
            FrontendContext frontend,
            MerchantContext merchant,
            ControllerContext? context = null) =>
            new(frontend,
                merchant,
                new MpAppUserStatusService(TestDb.Create<VcbPortalDbContext>(),
                                           NullLogger<MpAppUserStatusService>.Instance))
            {
                ControllerContext = context ?? TestHttpContext.Build()
            };

        // ── Đọc kết quả trả về ──────────────────────────────────────────────────
        // Ba hàm dưới đây là chỗ DUY NHẤT biết khuôn response. Khuôn MobileApiError
        // ở solution thật khác thì chỉ sửa ở đây, không phải sửa từng test.

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

        public static Dictionary<string, string> DocClaimTuToken(IActionResult result)
        {
            var jwt = DocToken(result);

            return jwt.Claims
                .GroupBy(c => c.Type)
                .ToDictionary(g => g.Key, g => g.First().Value);
        }

        public static JsonWebToken DocToken(IActionResult result)
        {
            var ok = Assert.IsType<OkObjectResult>(result);
            var body = Assert.IsType<MobileApiResult>(ok.Value);

            Assert.Equal("success", body.Status);

            var token = Assert.IsType<string>(body.Data!["token"]);
            return new JsonWebTokenHandler().ReadJsonWebToken(token);
        }
    }
}
