using System.Net;
using Microsoft.AspNetCore.Mvc;
using VcbPortalApi.Models.MobileApp;
using VcbPortalApi.UnitTests.Fixtures;

namespace VcbPortalApi.UnitTests.Models
{
    /// <summary>
    /// MobileApiError là hợp đồng response của toàn bộ API mobile: mỗi nhánh phải
    /// ra đúng cặp (code, HTTP status). App di động phân luồng theo <c>code</c>, nên
    /// gán nhầm một mã là app xử lý sai — kiểu lỗi không compiler nào bắt được.
    ///
    /// Đây cũng là chỗ khoá lại quy ước: lỗi nghiệp vụ trả 400, KHÔNG trả 200.
    /// </summary>
    public class MobileApiErrorTests
    {
        public static TheoryData<IActionResult, string, HttpStatusCode> ErrorResponses => new()
        {
            { MobileApiError.BaseError(), MobileApiError.CodeBaseError, HttpStatusCode.BadRequest },
            { MobileApiError.Unauthorized(), MobileApiError.CodeUnauthorized, HttpStatusCode.Unauthorized },
            { MobileApiError.NotFound(), MobileApiError.CodeNotFound, HttpStatusCode.NotFound },
            { MobileApiError.InternalServerError(), MobileApiError.CodeInternalServerError, HttpStatusCode.InternalServerError },
            { MobileApiError.TryAgainLater(), MobileApiError.CodeTryAgainLater, HttpStatusCode.InternalServerError },
            { MobileApiError.OtpAuthenticationError(), MobileApiError.OtpError, HttpStatusCode.BadRequest },
            { MobileApiError.OtpSendFailed(), MobileApiError.OtpSendError, HttpStatusCode.BadRequest },
        };

        /// <summary>Mỗi nhánh lỗi có đúng một cặp (code, status) riêng.</summary>
        [Theory]
        [MemberData(nameof(ErrorResponses))]
        public void ErrorFactories_MapToTheirOwnCodeAndStatus(
            IActionResult result, string expectedCode, HttpStatusCode expectedStatus)
        {
            result.ShouldBeError(expectedCode, expectedStatus);
        }

        /// <summary>Không nhánh lỗi nào được trùng mã với nhánh khác.</summary>
        [Fact]
        public void ErrorCodes_AreAllDistinct()
        {
            var codes = ErrorResponses
                .Select(row => (string)row[1]!)
                .ToList();

            codes.Should().OnlyHaveUniqueItems();
        }

        /// <summary>Thành công là code "0" + HTTP 200, khác hẳn mọi nhánh lỗi.</summary>
        [Fact]
        public void BaseSuccess_ReturnsSuccessCodeAndOk()
        {
            var result = MobileApiError.BaseSuccess();

            result.ShouldBeSuccess();
            result.ShouldHaveMessage("thành công");
        }

        /// <summary>Không truyền message thì dùng câu mặc định của từng nhánh.</summary>
        [Fact]
        public void BaseError_WithoutMessage_UsesDefaultMessage()
        {
            var result = MobileApiError.BaseError();

            result.ShouldHaveMessage("Thông tin không chính xác. Quý khách vui lòng kiểm tra lại.");
        }

        /// <summary>Truyền message thì message đó thay câu mặc định, code giữ nguyên.</summary>
        [Fact]
        public void BaseError_WithMessage_KeepsCodeAndOverridesMessage()
        {
            var result = MobileApiError.BaseError("Sai OTP roi ban oi");

            result.ShouldBeError(MobileApiError.CodeBaseError);
            result.ShouldHaveMessage("Sai OTP roi ban oi");
        }

        /// <summary>BaseErrorWithCode cho phép chọn mã, vẫn giữ HTTP 400.</summary>
        [Fact]
        public void BaseErrorWithCode_UsesGivenCodeAndStaysBadRequest()
        {
            var result = MobileApiError.BaseErrorWithCode(MobileApiError.UserLocked);

            result.ShouldBeError(MobileApiError.UserLocked, HttpStatusCode.BadRequest);
        }

        /// <summary>
        /// BaseSuccessWithData trộn dữ liệu nghiệp vụ vào cùng body với code/message.
        /// Vì là Dictionary nên khoá nghiệp vụ tên "code" sẽ BỊ GHI ĐÈ — đặt tên
        /// trường trùng là mất dữ liệu, không có cảnh báo nào.
        /// </summary>
        [Fact]
        public void BaseSuccessWithData_MergesFieldsAlongsideCodeAndMessage()
        {
            var result = MobileApiError.BaseSuccessWithData(new Dictionary<string, object?>
            {
                ["token"] = "abc123",
                ["expiresIn"] = 1800
            });

            result.ShouldBeSuccess();

            var body = result.ShouldBeApiResponse();
            body.ReadField("token").Should().Be("abc123");
            body.ReadField("expiresIn").Should().Be("1800");
        }

        /// <summary>Ghi lại hành vi ghi đè nói trên, để ai đó đổi thứ tự gán thì test đỏ.</summary>
        [Fact]
        public void BaseSuccessWithData_WhenFieldNamedCode_IsOverwritten()
        {
            var result = MobileApiError.BaseSuccessWithData(new Dictionary<string, object?>
            {
                ["code"] = "gia-tri-nghiep-vu"
            });

            result.ShouldBeApiResponse().ReadField("code").Should().Be(MobileApiError.CodeBaseSuccess);
        }

        /// <summary>BaseErrorWithCodeAndData: trả kèm dữ liệu nhưng vẫn là lỗi 400.</summary>
        [Fact]
        public void BaseErrorWithCodeAndData_KeepsErrorCodeAndCarriesData()
        {
            var result = MobileApiError.BaseErrorWithCodeAndData(
                MobileApiError.DeviceChanged,
                new Dictionary<string, object?> { ["deviceId"] = "device-01" });

            result.ShouldBeError(MobileApiError.DeviceChanged, HttpStatusCode.BadRequest);
            result.ShouldBeApiResponse().ReadField("deviceId").Should().Be("device-01");
        }
    }
}
