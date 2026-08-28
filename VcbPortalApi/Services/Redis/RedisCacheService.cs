using System.Text.Json;
using StackExchange.Redis;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG. Thân hàm GetByKeysAsync được CHÉP NGUYÊN VĂN từ ảnh code thật.
//
// HAI THỨ TÔI ĐOÁN, sửa lại cho khớp rồi test vẫn chạy nguyên:
//   1. Tên class (đặt theo tên file RedisCacheService.cs thấy trên tab VS) và
//      namespace (theo dòng using trong FepController).
//   2. GetAsync<T> và GetByIndexAsync<T>: ảnh chỉ thấy PHẦN ĐUÔI của
//      GetByIndexAsync, không thấy chữ ký. Đuôi đó là:
//          var indexKey = $"{redisKeyPrefix}:{primaryKeyFieldName}";
//          var indexDict = await cacheService.GetAsync<Dictionary<string,string>>(indexKey);
//          if (indexDict == null || indexDict.Count == 0) return [];
//          return await cacheService.GetByKeysAsync<T>(redisKeyPrefix, indexDict.Keys);
//      Nhưng Authenticate gọi ba tham số: GetByIndexAsync<VCanBo>("v_canbo",
//      "TaiKhoanDomain", userName) — tham số thứ ba không xuất hiện trong đuôi
//      đó, nên nhiều khả năng nó nằm trong indexKey. Tôi ghép value vào indexKey.
//
// Test cho Authenticate KHÔNG phụ thuộc cách ghép key: nó mock IDatabase ở mức
// StringGetAsync(RedisKey) và StringGetAsync(RedisKey[]) — hai overload khác nhau,
// nên chỉ cần đúng "một lần đọc index, một lần đọc item" là khớp.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Services.Redis
{
    public static class RedisCacheService
    {
        private static readonly JsonSerializerOptions DefaultOptions =
            new() { PropertyNameCaseInsensitive = true };

        /// <summary>Đọc một khoá đơn rồi deserialize. Không có khoá thì trả về default.</summary>
        public static async Task<T?> GetAsync<T>(this IDatabase redisDb, string key)
        {
            var value = await redisDb.StringGetAsync(key);

            if (value.IsNull)
                return default;

            return JsonSerializer.Deserialize<T>(value.ToString(), DefaultOptions);
        }

        /// <summary>
        /// Lấy danh sách đối tượng T theo một trường đánh index.
        /// Index là một Dictionary {pk → giá trị}, lưu ở khoá riêng.
        /// </summary>
        public static async Task<List<T>> GetByIndexAsync<T>(
            this IDatabase redisDb, string redisKeyPrefix, string primaryKeyFieldName, string value)
        {
            var indexKey = $"{redisKeyPrefix}:{primaryKeyFieldName}:{value}";

            var indexDict = await redisDb.GetAsync<Dictionary<string, string>>(indexKey);

            if (indexDict == null || indexDict.Count == 0)
                return [];

            return await redisDb.GetByKeysAsync<T>(redisKeyPrefix, indexDict.Keys);
        }

        /// <summary>
        /// Lấy danh sách các đối tượng T từ IDatabase theo danh sách Primary Keys (PK).
        /// Key item dạng: {redisKeyPrefix}:{pk}
        /// </summary>
        public static async Task<List<T>> GetByKeysAsync<T>(
            this IDatabase redisDb, string redisKeyPrefix, IEnumerable<string> primaryKeys)
        {
            var keys = primaryKeys.Select(pk => (RedisKey)$"{redisKeyPrefix}:{pk}").ToArray();

            if (keys.Length == 0) return [];

            var values = await redisDb.StringGetAsync(keys);

            var result = new List<T>();

            foreach (var val in values)
            {
                if (!val.IsNull)
                {
                    var item = JsonSerializer.Deserialize<T>(val.ToString(), DefaultOptions);

                    if (item != null)
                        result.Add(item);
                }
            }

            return result;
        }
    }
}
