// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution thật đã có SimpleCaptcha. CHƯA CÓ ẢNH của file thật.
// Chỉ giữ đúng chữ ký Validate(userEnteredCode, captchaId) mà Authenticate gọi.
// ĐỪNG chép đè.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Tools
{
    public class SimpleCaptcha
    {
        public bool Validate(string? userEnteredCaptchaCode, string? captchaId) =>
            throw new NotImplementedException("Ban khung khong kiem tra captcha that.");
    }
}
