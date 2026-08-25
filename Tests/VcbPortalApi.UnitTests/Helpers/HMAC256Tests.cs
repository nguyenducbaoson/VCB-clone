using VcbPortalApi.Helpers;

namespace VcbPortalApi.UnitTests.Helpers
{
    /// <summary>
    /// Hàm ký HMAC-SHA256 dùng khi gọi VCB SSO.
    ///
    /// Loại hàm đáng unit test nhất: đầu vào giống nhau luôn cho đầu ra giống nhau,
    /// sai một chút là cả luồng SSO hỏng mà nhìn code không thấy sai.
    ///
    /// Giá trị mong đợi là vector kiểm thử công khai của RFC 4231 — KHÔNG phải khoá
    /// thật của hệ thống. Đừng bao giờ đưa khoá thật vào test.
    /// </summary>
    public class HMAC256Tests
    {
        [Fact]
        public void HmacSha256_WhenGivenRfc4231Vector_ReturnsExpectedSignature()
        {
            // Arrange - RFC 4231 test case 2
            const string key = "Jefe";
            const string message = "what do ya want for nothing?";

            // Act
            var signature = HMAC256.HmacSha256(message, key);

            // Assert
            signature.Should().Be("5BDCC146BF60754E6A042426089575C75A003F089D2739839DEC58B964EC3843");
        }

        /// <summary>
        /// VCB so khớp chữ ký dạng hex CHỮ HOA. Ký ra chữ thường là bị từ chối, mà lỗi
        /// trả về sẽ không nói rõ nguyên nhân.
        /// </summary>
        [Fact]
        public void HmacSha256_WhenCalled_ReturnsUppercaseHexOf64Chars()
        {
            // Arrange & Act
            var signature = HMAC256.HmacSha256("payload", "secret");

            // Assert
            signature.Should().HaveLength(64);
            signature.Should().MatchRegex("^[0-9A-F]{64}$");
        }

        [Fact]
        public void HmacSha256_WhenCalledTwiceWithSameInput_ReturnsSameSignature()
        {
            // Arrange & Act
            var first = HMAC256.HmacSha256("payload", "secret");
            var second = HMAC256.HmacSha256("payload", "secret");

            // Assert
            first.Should().Be(second);
        }

        [Fact]
        public void HmacSha256_WhenKeyDiffers_ReturnsDifferentSignature()
        {
            // Arrange & Act
            var withKeyA = HMAC256.HmacSha256("payload", "secret-a");
            var withKeyB = HMAC256.HmacSha256("payload", "secret-b");

            // Assert
            withKeyA.Should().NotBe(withKeyB);
        }
    }
}
