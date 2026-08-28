using Microsoft.AspNetCore.Mvc;
using VcbPortalApi.Models.MobileApp;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution thật đã có HttpError (FepController gọi HttpError.BaseError()).
// CHƯA CÓ ẢNH của file thật. Dựng lại bằng cách uỷ thác cho MobileApiError.BaseError,
// vì đó là khuôn lỗi chuẩn của hệ thống. ĐỪNG chép đè.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Helpers
{
    public static class HttpError
    {
        public static IActionResult BaseError(string? message = null) => MobileApiError.BaseError(message);
    }
}
