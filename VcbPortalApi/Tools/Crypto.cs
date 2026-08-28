using System.Security.Cryptography;
using System.Text;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution thật đã có Crypto. ĐỪNG chép đè.
//
// Thuật toán dưới đây LÀ PHỎNG ĐOÁN (PBKDF2-SHA256). Test không khẳng định giá
// trị băm cụ thể — chỉ khẳng định các bất biến còn đúng dù thuật toán có khác:
// salt mỗi lần một khác, cùng dữ liệu + cùng salt cho ra cùng hash, mật khẩu
// sinh tự động phải qua được IsStrongPassword.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Tools
{
    public static class Crypto
    {
        private const int HashSize = 32;
        private const int Iterations = 10_000;

        private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        private const string Lower = "abcdefghijkmnpqrstuvwxyz";
        private const string Digit = "23456789";
        private const string Special = "!@#$%^&*";

        // Hai hàm dưới đây CHÉP NGUYÊN VĂN từ ảnh code thật.

        public static byte[] GenerateSalt()
        {
            return GenerateSalt(AppSettings.SaltLength);
        }

        public static byte[] GenerateSalt(int saltLength)
        {
            byte[] salt = new byte[saltLength];
            RandomNumberGenerator.Create().GetBytes(salt);
            return salt;
        }

        public static byte[] GenerateHash(string data, byte[] salt) =>
            Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(data), salt, Iterations, HashAlgorithmName.SHA256, HashSize);

        public static bool ValidateHash(string data, string salt, string hash)
        {
            try
            {
                var expected = Convert.FromBase64String(hash);
                var actual = GenerateHash(data, Convert.FromBase64String(salt));

                return CryptographicOperations.FixedTimeEquals(expected, actual);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        /// <summary>Mật khẩu tạm gửi qua email khi khởi tạo user mới.</summary>
        public static string GeneratePassword()
        {
            var all = Upper + Lower + Digit + Special;

            char[] chars =
            [
                Pick(Upper), Pick(Lower), Pick(Digit), Pick(Special),
                Pick(all), Pick(all), Pick(all), Pick(all), Pick(all), Pick(all)
            ];

            // Xáo trộn để 4 ký tự bắt buộc không luôn nằm ở đầu.
            for (var i = chars.Length - 1; i > 0; i--)
            {
                var j = RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }

            return new string(chars);
        }

        public static bool IsStrongPassword(string? password) =>
            password is { Length: >= 8 } &&
            password.Any(char.IsUpper) &&
            password.Any(char.IsLower) &&
            password.Any(char.IsDigit) &&
            password.Any(c => !char.IsLetterOrDigit(c));

        private static char Pick(string source) => source[RandomNumberGenerator.GetInt32(source.Length)];
    }
}
