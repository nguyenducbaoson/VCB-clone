using Microsoft.AspNetCore.Mvc;
using VcbPortalApi.Models.MobileApp;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — CHƯA CÓ ẢNH của file thật. ĐỪNG chép đè.
//
// Suy ra từ chỗ gọi: FepController dùng `new ErrorMessage("wrong_captcha").Simplify()`
// và `new ErrorMessage("Lỗi HCM").Simplify()` — tham số là THÔNG ĐIỆP. Mà
// MobileApiError.BaseError(string? message) cũng nhận thông điệp. Nên nhiều khả
// năng Simplify() chỉ là BaseError(message): code "01", message tuỳ nhánh.
//
// Nguyên tắc phải giữ: KHÔNG đưa nội dung exception ra client, chỉ ghi vào log.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Helpers
{
    public sealed class ErrorMessage
    {
        /// <summary>Lỗi hệ thống — nội dung exception KHÔNG đi ra client.</summary>
        public ErrorMessage(Exception exception)
        {
            Exception = exception;
            _message = null;
        }

        /// <summary>Lỗi nghiệp vụ có thông điệp riêng.</summary>
        public ErrorMessage(string message) => _message = message;

        private readonly string? _message;

        public IActionResult Simplify() =>
            _message is null
                ? MobileApiError.InternalServerError()
                : MobileApiError.BaseError(_message);

        /// <summary>Giữ lại để tầng log dùng; không bao giờ đi ra response.</summary>
        public Exception? Exception { get; }
    }
}
