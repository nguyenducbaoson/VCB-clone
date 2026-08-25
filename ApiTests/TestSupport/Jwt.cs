using System.Text;
using System.Text.Json;

namespace ApiTests.TestSupport
{
    /// <summary>
    /// Đọc nội dung một JWT mà KHÔNG kiểm tra chữ ký.
    ///
    /// Cố ý không kiểm tra chữ ký: test đứng ngoài hệ thống, không có khoá ký, và
    /// việc cần biết là API có đặt đúng claim và đúng hạn hay không. Chuyện chữ ký
    /// có hợp lệ không thì bên tiêu thụ token (partner SDK) tự xác minh.
    /// </summary>
    public static class Jwt
    {
        /// <summary>Trả về map claim. Token sai định dạng thì trả map rỗng.</summary>
        public static Dictionary<string, string> DocClaim(string? token)
        {
            var ket = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(token)) return ket;

            var phan = token.Split('.');
            if (phan.Length < 2) return ket;

            try
            {
                using var doc = JsonDocument.Parse(GiaiMaBase64Url(phan[1]));

                foreach (var p in doc.RootElement.EnumerateObject())
                {
                    ket[p.Name] = p.Value.ValueKind == JsonValueKind.String
                        ? p.Value.GetString() ?? ""
                        : p.Value.ToString();
                }
            }
            catch (Exception)
            {
                // Token rác — trả map rỗng, để test tự báo lỗi bằng assert của nó.
            }

            return ket;
        }

        /// <summary>Hạn token (claim exp). Không có exp thì trả null.</summary>
        public static DateTime? HanUtc(string? token)
        {
            var claims = DocClaim(token);

            return claims.TryGetValue("exp", out var exp) && long.TryParse(exp, out var giay)
                ? DateTimeOffset.FromUnixTimeSeconds(giay).UtcDateTime
                : null;
        }

        private static byte[] GiaiMaBase64Url(string s)
        {
            s = s.Replace('-', '+').Replace('_', '/');
            s = s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');

            return Convert.FromBase64String(s);
        }
    }
}
