using VcbPortalApi.Helpers;

namespace Tests.Helpers
{
    /// <summary>
    /// Unit test cho hàm ký HMAC-SHA256 dùng khi gọi VCB SSO.
    ///
    /// Đây là loại hàm đáng unit test nhất: đầu vào giống nhau luôn cho đầu ra giống
    /// nhau, sai một chút là cả luồng SSO hỏng, mà nhìn code thì không thấy sai.
    ///
    /// Giá trị mong đợi dưới đây là vector kiểm thử công khai của RFC 4231 — không
    /// phải khoá thật của hệ thống. Đừng bao giờ đưa khoá thật vào test.
    /// </summary>
    public class HMAC256Tests
    {
        [Fact]
        public void HmacSha256_MatchesRfc4231TestVector()
        {
            // RFC 4231 test case 2: key = "Jefe", data = "what do ya want for nothing?"
            var actual = HMAC256.HmacSha256("what do ya want for nothing?", "Jefe");

            Assert.Equal(
                "5BDCC146BF60754E6A042426089575C75A003F089D2739839DEC58B964EC3843",
                actual);
        }

        /// <summary>
        /// VCB so khớp chữ ký dạng hex CHỮ HOA. Ký ra chữ thường là bị từ chối,
        /// mà lỗi trả về sẽ không nói rõ nguyên nhân.
        /// </summary>
        [Fact]
        public void HmacSha256_ReturnsUppercaseHex()
        {
            var actual = HMAC256.HmacSha256("payload", "secret");

            Assert.Equal(actual.ToUpperInvariant(), actual);
            Assert.Equal(64, actual.Length);
            Assert.All(actual, c => Assert.True(char.IsAsciiHexDigitUpper(c), $"Ky tu '{c}' khong phai hex chu hoa."));
        }

        [Fact]
        public void HmacSha256_IsDeterministic()
        {
            var first = HMAC256.HmacSha256("payload", "secret");
            var second = HMAC256.HmacSha256("payload", "secret");

            Assert.Equal(first, second);
        }

        [Fact]
        public void HmacSha256_DifferentKeyGivesDifferentSignature()
        {
            var withKeyA = HMAC256.HmacSha256("payload", "secret-a");
            var withKeyB = HMAC256.HmacSha256("payload", "secret-b");

            Assert.NotEqual(withKeyA, withKeyB);
        }
    }
}
