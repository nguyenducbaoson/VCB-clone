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

        /// <summary>JSON viết hoa vẫn khớp property nhờ PropertyNameCaseInsensitive.</summary>
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
    }
}
