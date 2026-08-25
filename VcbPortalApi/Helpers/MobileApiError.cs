using Microsoft.AspNetCore.Mvc;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution thật đã có MobileApiError. ĐỪNG chép đè.
//
// Khuôn response thật gần như chắc chắn khác bản này. Chỉ cần giữ đúng nguyên tắc:
// mỗi nhánh lỗi mang một MÃ riêng phân biệt được, để test khẳng định được là code
// rơi vào đúng nhánh mình muốn chứ không phải nhánh khác cũng trả lỗi.
// Khi mang test sang solution thật, sửa lại hàm đọc mã trong TestSupport cho khớp.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Helpers
{
    public sealed class MobileApiResult
    {
        public string Status { get; init; } = "error";
        public string Code { get; init; } = string.Empty;
        public Dictionary<string, object?>? Data { get; init; }
    }

    public static class MobileApiError
    {
        public static IActionResult BaseError(string code = "BaseError") =>
            new OkObjectResult(new MobileApiResult { Status = "error", Code = code });

        public static IActionResult Unauthorized() =>
            new UnauthorizedObjectResult(new MobileApiResult { Status = "error", Code = "Unauthorized" });

        public static IActionResult BaseSuccessWithData(Dictionary<string, object?> data) =>
            new OkObjectResult(new MobileApiResult { Status = "success", Code = "Success", Data = data });
    }
}
