using System.Net;
using Microsoft.AspNetCore.Mvc;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — CHÉP NGUYÊN VĂN từ ảnh code thật. ĐỪNG chép đè.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Models.MobileApp;

/// <summary>Response lỗi chuẩn mobile — body dạng { "code": "01", "message": "..." }.</summary>
public static class MobileApiError
{
    public const string CodeBaseSuccess = "0";
    public const string CodeBaseError = "01";
    public const string CodeUnauthorized = "02";
    public const string CodeNotFound = "03";
    public const string CodeInternalServerError = "04";
    public const string CodeTryAgainLater = "05";
    public const string UserLocked = "06";
    public const string UserNotFound = "07";
    public const string PasswordWrong = "08";
    public const string DeviceActivationRequired = "09";
    public const string AccountNotActivated = "10";
    public const string DeviceChanged = "11";
    public const string PasswordExpired = "12";
    public const string OtpError = "13";
    /// <summary>Gửi SMS OTP thất bại.</summary>
    public const string OtpSendError = "14";

    private static IActionResult WithResponse(string code, string message, HttpStatusCode statusCode) =>
        new ObjectResult(new { code, message }) { StatusCode = (int)statusCode };

    public static IActionResult BaseSuccess(string? message = null) =>
        WithResponse(CodeBaseSuccess, message ?? "thành công", HttpStatusCode.OK);

    public static IActionResult BaseSuccessWithData(Dictionary<string, object?> fields, string? message = null)
    {
        var body = new Dictionary<string, object?>(fields)
        {
            ["code"] = CodeBaseSuccess,
            ["message"] = message ?? "thành công",
        };

        return new ObjectResult(body) { StatusCode = (int)HttpStatusCode.OK };
    }

    public static IActionResult BaseErrorWithCode(string code, string? message = null) =>
        WithResponse(code, message ?? "Thông tin không chính xác. Quý khách vui lòng kiểm tra lại.", HttpStatusCode.BadRequest);

    public static IActionResult BaseErrorWithCodeAndData(
        string code,
        Dictionary<string, object?> fields,
        string? message = null)
    {
        var body = new Dictionary<string, object?>(fields)
        {
            ["code"] = code,
            ["message"] = message ?? "Thông tin không chính xác. Quý khách vui lòng kiểm tra lại.",
        };

        return new ObjectResult(body) { StatusCode = (int)HttpStatusCode.BadRequest };
    }

    public static IActionResult BaseError(string? message = null) =>
        WithResponse(CodeBaseError, message ?? "Thông tin không chính xác. Quý khách vui lòng kiểm tra lại.", HttpStatusCode.BadRequest);

    public static IActionResult Unauthorized(string? message = null) =>
        WithResponse(CodeUnauthorized, message ?? "Không có quyền", HttpStatusCode.Unauthorized);

    public static IActionResult NotFound(string? message = null) =>
        WithResponse(CodeNotFound, message ?? "Không tìm thấy", HttpStatusCode.NotFound);

    public static IActionResult InternalServerError(string? message = null) =>
        WithResponse(CodeInternalServerError, message ?? "Lỗi hệ thống", HttpStatusCode.InternalServerError);

    public static IActionResult TryAgainLater(string? message = null) =>
        WithResponse(CodeTryAgainLater, message ?? "Có lỗi xảy ra. Vui lòng thử lại sau", HttpStatusCode.InternalServerError);

    /// <summary>Xác thực OTP thất bại (verify).</summary>
    public static IActionResult OtpAuthenticationError(string? message = null) =>
        WithResponse(OtpError, message ?? "Lỗi xác thực OTP", HttpStatusCode.BadRequest);

    /// <summary>Gửi SMS OTP thất bại (challenge).</summary>
    public static IActionResult OtpSendFailed(string? message = null) =>
        WithResponse(OtpSendError, message ?? "Gửi SMS OTP thất bại", HttpStatusCode.BadRequest);
}
