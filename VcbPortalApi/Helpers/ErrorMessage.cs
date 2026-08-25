using Microsoft.AspNetCore.Mvc;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution thật đã có ErrorMessage. ĐỪNG chép đè.
//
// Bản thật gần như chắc chắn trả khuôn khác. Chỉ cần giữ đúng nguyên tắc: KHÔNG
// đưa nội dung exception ra client, chỉ ghi vào log phía server.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Helpers
{
    public sealed class ErrorMessage(Exception exception)
    {
        public IActionResult Simplify() =>
            new OkObjectResult(new MobileApiResult
            {
                Status = "error",
                Code = "SystemError"
            });

        /// <summary>Giữ lại để tầng log dùng; không bao giờ đi ra response.</summary>
        public Exception Exception { get; } = exception;
    }
}
