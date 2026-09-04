using System.Text.Json;
using StackExchange.Redis;
using VcbPortalApi.Services.Redis;

namespace VcbPortalApi.UnitTests.Services.Redis
{
    /// <summary>
    /// <c>GetByKeysAsync</c> chỉ phụ thuộc <c>IDatabase</c> — mock thẳng, không cần
    /// Redis thật. Test dừng ở mức cơ bản: ghép key đúng khuôn, đọc được dữ liệu,
    /// và key không có trong cache thì bỏ qua.
    /// </summary>
    public class RedisCacheServiceTests
    {
        private const string Prefix = "MP:TERMINAL";

        private readonly Mock<IDatabase> _redis = new(MockBehavior.Strict);
        private string[]? _capturedKeys;

        private sealed class TerminalDto
        {
            public string? Tid { get; set; }
            public decimal Bid { get; set; }
        }

        private void RedisReturns(params RedisValue[] values) =>
            _redis.Setup(x => x.StringGetAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
                  .Callback<RedisKey[], CommandFlags>((keys, _) =>
                      _capturedKeys = keys.Select(k => k.ToString()).ToArray())
                  .ReturnsAsync(values);

        private Task<List<TerminalDto>> GetByKeys(params string[] primaryKeys) =>
            _redis.Object.GetByKeysAsync<TerminalDto>(Prefix, primaryKeys);

        private static RedisValue Json(string tid, decimal bid) =>
            $$"""{"tid":"{{tid}}","bid":{{bid}}}""";

        /// <summary>Không có PK nào thì thoát sớm, không đi một vòng vô ích sang Redis.</summary>
        [Fact]
        public async Task GetByKeysAsync_WhenNoPrimaryKeys_ReturnsEmptyWithoutCallingRedis()
        {
            // Arrange — cố tình KHÔNG setup StringGetAsync

            // Act
            var result = await GetByKeys();

            // Assert
            result.Should().BeEmpty();
            _redis.Verify(x => x.StringGetAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()), Times.Never);
        }

        /// <summary>Key item có khuôn {prefix}:{pk}.</summary>
        [Fact]
        public async Task GetByKeysAsync_BuildsKeyAsPrefixColonPrimaryKey()
        {
            // Arrange
            RedisReturns(RedisValue.Null, RedisValue.Null);

            // Act
            await GetByKeys("0001", "0002");

            // Assert
            _capturedKeys.Should().Equal("MP:TERMINAL:0001", "MP:TERMINAL:0002");
        }

        /// <summary>Mọi key đều có dữ liệu thì trả về đủ, đúng thứ tự key.</summary>
        [Fact]
        public async Task GetByKeysAsync_WhenAllKeysHit_ReturnsAllItems()
        {
            // Arrange
            RedisReturns(Json("T01", 11), Json("T02", 22));

            // Act
            var result = await GetByKeys("0001", "0002");

            // Assert
            result.Select(x => x.Tid).Should().Equal("T01", "T02");
            result.Select(x => x.Bid).Should().Equal(11, 22);
        }

        /// <summary>Key không có trong cache trả về null — bỏ qua, không thêm phần tử null.</summary>
        [Fact]
        public async Task GetByKeysAsync_WhenSomeKeysMiss_SkipsThem()
        {
            // Arrange
            RedisReturns(RedisValue.Null, Json("T02", 22));

            // Act
            var result = await GetByKeys("0001", "0002");

            // Assert
            result.Should().ContainSingle();
            result[0].Tid.Should().Be("T02");
        }

        /// <summary>
        /// Hàm ghép key bằng nội suy chuỗi, KHÔNG chuẩn hoá gì: PK có khoảng trắng
        /// thừa thì key sai và luôn miss. Bên gọi phải tự trim.
        /// </summary>
        [Fact]
        public async Task GetByKeysAsync_DoesNotTrimPrimaryKeys()
        {
            RedisReturns(RedisValue.Null);

            await GetByKeys(" 0001 ");

            _capturedKeys.Should().Equal("MP:TERMINAL: 0001 ");
        }

        /// <summary>Ô Redis chứa đúng chữ "null" — deserialize ra null, phải bị loại.</summary>
        [Fact]
        public async Task GetByKeysAsync_WhenValueIsJsonNullLiteral_SkipsItem()
        {
            RedisReturns("null", Json("T02", 22));

            var result = await GetByKeys("0001", "0002");

            result.Should().ContainSingle();
            result[0].Tid.Should().Be("T02");
        }

        /// <summary>
        /// KHÔNG có try/catch quanh Deserialize: một ô dữ liệu hỏng làm hỏng cả lô,
        /// kể cả những key đọc được. Ghi lại hành vi hiện tại — muốn bỏ qua ô hỏng
        /// thay vì ném thì phải sửa hàm, và test này sẽ đỏ để nhắc.
        /// </summary>
        [Fact]
        public async Task GetByKeysAsync_WhenOneValueIsInvalidJson_ThrowsAndLosesWholeBatch()
        {
            RedisReturns(Json("T01", 11), "khong-phai-json");

            var act = () => GetByKeys("0001", "0002");

            await act.Should().ThrowAsync<JsonException>();
        }

        [Fact]
        public async Task GetByKeysAsync_MatchesPropertyNamesIgnoringCase()
        {
            // Arrange
            RedisReturns("""{"TID":"T01","BID":11}""");

            // Act
            var result = await GetByKeys("0001");

            // Assert
            result.Should().ContainSingle();
            result[0].Tid.Should().Be("T01");
            result[0].Bid.Should().Be(11);
        }

        // ══ GetAsync ════════════════════════════════════════════════════════════

        private void SingleKeyReturns(RedisValue value) =>
            _redis.Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                  .ReturnsAsync(value);

        /// <summary>Khoá không có trong cache thì trả về default, không ném.</summary>
        [Fact]
        public async Task GetAsync_WhenKeyMissing_ReturnsDefault()
        {
            SingleKeyReturns(RedisValue.Null);

            var result = await _redis.Object.GetAsync<TerminalDto>("MP:TERMINAL:0001");

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetAsync_WhenKeyHit_DeserializesValue()
        {
            SingleKeyReturns(Json("T01", 11));

            var result = await _redis.Object.GetAsync<TerminalDto>("MP:TERMINAL:0001");

            result!.Tid.Should().Be("T01");
            result.Bid.Should().Be(11);
        }

        // ══ GetByIndexAsync ═════════════════════════════════════════════════════
        //
        // CHÚ Ý: ảnh code thật chỉ có phần ĐUÔI của hàm này, phần ghép indexKey là
        // tôi dựng lại. Nên các test dưới đây khoá HỢP ĐỒNG (không có index thì trả
        // rỗng, có index thì đọc tiếp item theo pk) chứ KHÔNG khoá chuỗi key cụ thể.

        private void IndexReturns(RedisValue index, params RedisValue[] items)
        {
            _redis.Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                  .ReturnsAsync(index);

            _redis.Setup(x => x.StringGetAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
                  .ReturnsAsync(items);
        }

        private Task<List<TerminalDto>> GetByIndex(string value = "VATID001") =>
            _redis.Object.GetByIndexAsync<TerminalDto>(Prefix, "TaiKhoanDomain", value);

        /// <summary>Không có index thì thoát sớm, KHÔNG đi tiếp bước đọc item.</summary>
        [Fact]
        public async Task GetByIndexAsync_WhenIndexMissing_ReturnsEmptyWithoutReadingItems()
        {
            SingleKeyReturns(RedisValue.Null);

            var result = await GetByIndex();

            result.Should().BeEmpty();
            _redis.Verify(x => x.StringGetAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()), Times.Never);
        }

        /// <summary>Index rỗng cũng vậy — đây là nhánh `indexDict.Count == 0`.</summary>
        [Fact]
        public async Task GetByIndexAsync_WhenIndexIsEmpty_ReturnsEmptyWithoutReadingItems()
        {
            SingleKeyReturns("{}");

            var result = await GetByIndex();

            result.Should().BeEmpty();
            _redis.Verify(x => x.StringGetAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()), Times.Never);
        }

        /// <summary>Có index thì đọc tiếp item theo đúng số khoá trong index.</summary>
        [Fact]
        public async Task GetByIndexAsync_WhenIndexHasKeys_ReturnsItems()
        {
            IndexReturns("""{"0001":"VATID001","0002":"VATID001"}""", Json("T01", 11), Json("T02", 22));

            var result = await GetByIndex();

            result.Select(x => x.Tid).Should().Equal("T01", "T02");
        }

        /// <summary>
        /// Index có khoá nhưng item đã bị xoá khỏi cache: danh sách trả về NGẮN HƠN
        /// index. Bên gọi đếm Count để phân luồng (Authenticate làm đúng vậy) phải
        /// hiểu con số đó là số item ĐỌC ĐƯỢC, không phải số khoá trong index.
        /// </summary>
        [Fact]
        public async Task GetByIndexAsync_WhenIndexedItemMissing_ReturnsFewerThanIndexed()
        {
            IndexReturns("""{"0001":"VATID001","0002":"VATID001"}""", RedisValue.Null, Json("T02", 22));

            var result = await GetByIndex();

            result.Should().ContainSingle("index co 2 khoa nhung chi doc duoc 1 item");
        }
    }
}
