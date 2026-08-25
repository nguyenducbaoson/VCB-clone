using Microsoft.AspNetCore.Mvc;
using VcbPortalApi.Helpers;

namespace VcbPortalApi.UnitTests.Fixtures
{
    /// <summary>
    /// Đọc response của controller trả khuôn <see cref="MobileApiError"/>.
    ///
    /// KHÔNG dùng chung cho mọi controller — chỉ cho nhóm dùng khuôn này. Controller
    /// trả khuôn khác thì viết bộ assert riêng cho nhóm đó.
    ///
    /// Đây là chỗ DUY NHẤT biết cấu trúc response. Khuôn ở solution thật khác bản
    /// khung này thì chỉ sửa ở đây, không phải sửa từng test.
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
    }
}
