using System.Security.Cryptography;
using System.Text;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution thật đã có helper này trong Helpers/. ĐỪNG chép đè.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Helpers
{
    public static class HMAC256
    {
        /// <summary>
        /// HMAC-SHA256 trả hex CHỮ HOA. VCB SSO so khớp chữ ký dạng này
        /// (response mẫu: "C0DA13F969...", 64 ký tự).
        /// </summary>
        public static string HmacSha256(string message, string secretKey)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey ?? string.Empty));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message ?? string.Empty));

            return Convert.ToHexString(hash);
        }
    }
}
