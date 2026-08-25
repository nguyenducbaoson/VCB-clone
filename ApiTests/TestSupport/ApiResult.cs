using System.Net;
using System.Text.Json;

namespace ApiTests.TestSupport
{
    /// <summary>
    /// Kết quả một lời gọi API.
    ///
    /// Giữ nguyên body dạng chuỗi thay vì parse sẵn thành object: khi test đỏ,
    /// <see cref="MoTa"/> in ra đúng những gì API trả về, khỏi phải mở Postman gọi lại.
    /// </summary>
    public sealed record ApiResult(string Method, string? Path, HttpStatusCode Status, string Body)
    {
        public bool ThanhCong => (int)Status is >= 200 and < 300;

        /// <summary>
        /// Đọc một field trong JSON trả về, KHÔNG phân biệt hoa thường và tìm cả trong
        /// object con. Cố ý viết lỏng để không phụ thuộc khuôn response cụ thể —
        /// "code"/"Code"/"resCode" đều lấy được mà không phải sửa test.
        /// Không có field hoặc body không phải JSON thì trả null.
        /// </summary>
        public string? Field(string ten)
        {
            if (string.IsNullOrWhiteSpace(Body)) return null;

            try
            {
                using var doc = JsonDocument.Parse(Body);
                return Tim(doc.RootElement, ten);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string? Tim(JsonElement node, string ten)
        {
            if (node.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in node.EnumerateObject())
                {
                    if (string.Equals(p.Name, ten, StringComparison.OrdinalIgnoreCase))
                    {
                        return p.Value.ValueKind switch
                        {
                            JsonValueKind.String => p.Value.GetString(),
                            JsonValueKind.Null => null,
                            _ => p.Value.ToString()
                        };
                    }

                    if (Tim(p.Value, ten) is { } sau) return sau;
                }
            }
            else if (node.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in node.EnumerateArray())
                    if (Tim(item, ten) is { } sau) return sau;
            }

            return null;
        }

        /// <summary>
        /// Chuỗi mô tả đầy đủ để đưa vào mọi Assert. Có đủ request lẫn response nên
        /// đọc log Test Explorer là dựng lại được lời gọi.
        /// </summary>
        public string MoTa =>
            $"{Method} {Path}\nHTTP {(int)Status} {Status}\nBody: {CatBot(Body)}";

        private static string CatBot(string s) =>
            s.Length <= 1000 ? s : s[..1000] + $"... (con {s.Length - 1000} ky tu)";
    }
}
