// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution thật đã có SignInPayload. ĐỪNG chép đè.
// Dựng lại đúng bốn field mà Authenticate đọc tới (dòng 26, 27, 35).
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Models.MP
{
    public class SignInPayload
    {
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? UserEnteredCaptchaCode { get; set; }
        public string? CaptchaId { get; set; }
    }
}
