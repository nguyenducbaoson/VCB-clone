using System.Text.Json;

namespace Tests.TestSupport
{
    /// <summary>
    /// Đọc nội dung một JWT mà KHÔNG kiểm tra chữ ký.
    ///
    /// Cố ý không kiểm tra chữ ký: test đứng ngoài hệ thống, không có khoá ký, và
    /// việc cần biết là API có đặt đúng claim và đúng hạn hay không. Chuyện chữ ký
    /// có hợp lệ không thì bên tiêu thụ token (partner SDK) tự xác minh.
    ///
    /// Phần payload của JWT chỉ mã hoá base64url chứ không mã hoá thật, nên đọc được
    /// mà không cần khoá.
    /// </summary>
    public static class Jwt
    {
        /// <summary>Trả về map claim. Token sai định dạng thì trả map rỗng.</summary>
        public static Dictionary<string, string> ReadClaims(string? token)
        {
            var claims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(token)) return claims;

            var parts = token.Split('.');
            if (parts.Length < 2) return claims;

            try
            {
                using var document = JsonDocument.Parse(DecodeBase64Url(parts[1]));

                foreach (var property in document.RootElement.EnumerateObject())
                {
                    claims[property.Name] = property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString() ?? ""
                        : property.Value.ToString();
                }
            }
            catch (Exception)
            {
                // Token rác — trả map rỗng, để test tự báo lỗi bằng assert của nó.
            }

            return claims;
        }

        /// <summary>Hạn token (claim exp). Không có exp thì trả null.</summary>
        public static DateTime? ExpiresUtc(string? token)
        {
            var claims = ReadClaims(token);

            return claims.TryGetValue("exp", out var exp) && long.TryParse(exp, out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
                : null;
        }

        private static byte[] DecodeBase64Url(string value)
        {
            value = value.Replace('-', '+').Replace('_', '/');
            value = value.PadRight(value.Length + (4 - value.Length % 4) % 4, '=');

            return Convert.FromBase64String(value);
        }
    }
}
