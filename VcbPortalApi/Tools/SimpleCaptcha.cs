// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution thật đã có SimpleCaptcha. ĐỪNG chép đè.
// Chỉ giữ đúng chữ ký Validate(userEnteredCode, captchaId) mà Authenticate gọi.
//
// KHÔNG CÓ TRONG BẢN THẬT: delegate Validator. Authenticate gọi
// `new SimpleCaptcha().Validate(...)` — dựng thẳng trong thân hàm, không tiêm được.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Tools
{
    public class SimpleCaptcha
    {
        public static Func<string?, string?, bool> Validator { get; set; } = RealValidate;

        public bool Validate(string? userEnteredCaptchaCode, string? captchaId) =>
            Validator(userEnteredCaptchaCode, captchaId);

        private static bool RealValidate(string? userEnteredCaptchaCode, string? captchaId) =>
            throw new NotImplementedException("Ban khung khong kiem tra captcha that.");
    }
}
