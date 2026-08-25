using System.Net;
using System.Text.Json;

namespace ApiTests.TestSupport
{
    /// <summary>
    /// Kết quả một lời gọi API.
    ///
    /// Giữ nguyên body dạng chuỗi thay vì parse sẵn thành object: khi test đỏ,
    /// <see cref="Describe"/> in ra đúng những gì API trả về, khỏi phải mở Postman
    /// gọi lại.
    /// </summary>
    public sealed record ApiResult(string Method, string? Path, HttpStatusCode Status, string Body)
    {
        public bool IsSuccess => (int)Status is >= 200 and < 300;

        /// <summary>
        /// Đọc một field trong JSON trả về, KHÔNG phân biệt hoa thường và tìm cả trong
        /// object con. Cố ý viết lỏng để không phụ thuộc khuôn response cụ thể —
        /// "code"/"Code"/"resCode" đều lấy được mà không phải sửa test.
        /// Không có field hoặc body không phải JSON thì trả null.
        /// </summary>
        public string? Field(string name)
        {
            if (string.IsNullOrWhiteSpace(Body)) return null;

            try
            {
                using var document = JsonDocument.Parse(Body);
                return Find(document.RootElement, name);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string? Find(JsonElement node, string name)
        {
            if (node.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in node.EnumerateObject())
                {
                    if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return property.Value.ValueKind switch
                        {
                            JsonValueKind.String => property.Value.GetString(),
                            JsonValueKind.Null => null,
                            _ => property.Value.ToString()
                        };
                    }

                    if (Find(property.Value, name) is { } nested) return nested;
                }
            }
            else if (node.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in node.EnumerateArray())
                    if (Find(item, name) is { } nested) return nested;
            }

            return null;
        }

        /// <summary>
        /// Chuỗi mô tả đầy đủ để đưa vào mọi Assert. Có đủ request lẫn response nên
        /// đọc log Test Explorer là dựng lại được lời gọi.
        /// </summary>
        public string Describe =>
            $"{Method} {Path}\nHTTP {(int)Status} {Status}\nBody: {Truncate(Body)}";

        private static string Truncate(string value) =>
            value.Length <= 1000 ? value : value[..1000] + $"... (con {value.Length - 1000} ky tu)";
    }
}
