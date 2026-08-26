using Microsoft.AspNetCore.Mvc;
using VcbPortalApi.Helpers;

namespace VcbPortalApi.UnitTests.Fixtures
{
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
