using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using VcbPortalApi.Helpers;

namespace VcbPortalApi.UnitTests.Fixtures
{
    /// <summary>
    /// Đọc response của các controller dùng khuôn <see cref="MobileApiError"/>.
    ///
    /// Đây là chỗ DUY NHẤT biết khuôn response. Khuôn ở solution thật khác bản khung
    /// này thì chỉ sửa ở đây, không phải sửa từng test.
    /// </summary>
    public static class MobileApiAssertions
    {
        public static void ShouldBeError(this IActionResult result, string expectedCode)
        {
            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            var body = ok.Value.Should().BeOfType<MobileApiResult>().Subject;

            body.Status.Should().Be("error");
            body.Code.Should().Be(expectedCode);
        }

        public static void ShouldBeUnauthorized(this IActionResult result)
        {
            var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            var body = unauthorized.Value.Should().BeOfType<MobileApiResult>().Subject;

            body.Code.Should().Be("Unauthorized");
        }

        /// <summary>Bóc token trong response thành công rồi trả về map claim.</summary>
        public static Dictionary<string, string> ReadTokenClaims(this IActionResult result) =>
            result.ReadToken().Claims
                  .GroupBy(c => c.Type)
                  .ToDictionary(g => g.Key, g => g.First().Value);

        public static JsonWebToken ReadToken(this IActionResult result)
        {
            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            var body = ok.Value.Should().BeOfType<MobileApiResult>().Subject;

            body.Status.Should().Be("success");

            var token = body.Data!["token"].Should().BeOfType<string>().Subject;
            return new JsonWebTokenHandler().ReadJsonWebToken(token);
        }
    }
}
