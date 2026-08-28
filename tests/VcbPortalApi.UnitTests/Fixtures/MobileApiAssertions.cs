using System.Net;
using VcbPortalApi.Models.MobileApp;
using Microsoft.AspNetCore.Mvc;

namespace VcbPortalApi.UnitTests.Fixtures
{
    /// <summary>
    /// Mọi response của MobileApiError đều là
    /// <c>ObjectResult { StatusCode = ..., Value = { code, message } }</c> — kể cả
    /// nhánh thành công. Value là kiểu vô danh hoặc Dictionary nên phải đọc động,
    /// không ép kiểu được.
    /// </summary>
    public static class MobileApiAssertions
    {
        /// <summary>Khớp mã lỗi ở trường <c>code</c>, và bắt buộc HTTP status khác 200.</summary>
        public static void ShouldBeError(this IActionResult result, string expectedCode)
        {
            var objectResult = result.ShouldBeApiResponse();

            objectResult.StatusCode.Should().NotBe((int)HttpStatusCode.OK, "loi thi khong duoc tra 200");
            objectResult.ReadField("code").Should().Be(expectedCode);
        }

        /// <summary>Khớp cả mã lỗi lẫn HTTP status.</summary>
        public static void ShouldBeError(this IActionResult result, string expectedCode, HttpStatusCode expectedStatus)
        {
            result.ShouldBeError(expectedCode);
            result.ShouldBeApiResponse().StatusCode.Should().Be((int)expectedStatus);
        }

        /// <summary>Nhánh thành công: code "0" và HTTP 200.</summary>
        public static void ShouldBeSuccess(this IActionResult result)
        {
            var objectResult = result.ShouldBeApiResponse();

            objectResult.StatusCode.Should().Be((int)HttpStatusCode.OK);
            objectResult.ReadField("code").Should().Be(MobileApiError.CodeBaseSuccess);
        }

        public static void ShouldHaveMessage(this IActionResult result, string expected) =>
            result.ShouldBeApiResponse().ReadField("message").Should().Be(expected);

        public static void ShouldHaveAccessToken(this IActionResult result) =>
            result.ShouldBeApiResponse().ReadField("accessToken").Should().NotBeNullOrEmpty();

        public static ObjectResult ShouldBeApiResponse(this IActionResult result) =>
            result.Should().BeAssignableTo<ObjectResult>().Subject;

        /// <summary>
        /// Đọc một trường của body. Body có ba dạng: kiểu vô danh (WithResponse),
        /// Dictionary (BaseSuccessWithData / BaseErrorWithCodeAndData), và kiểu vô
        /// danh do controller tự dựng (Ok(new { accessToken = ... })).
        /// </summary>
        public static string? ReadField(this ObjectResult result, string fieldName)
        {
            var value = result.Value;

            value.Should().NotBeNull("response phai co body");

            if (value is IDictionary<string, object?> dictionary)
                return dictionary.TryGetValue(fieldName, out var item) ? item?.ToString() : null;

            return value!.GetType().GetProperty(fieldName)?.GetValue(value)?.ToString();
        }
    }
}
