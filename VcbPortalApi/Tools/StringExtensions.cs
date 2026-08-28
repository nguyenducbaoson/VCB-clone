using System.Text.RegularExpressions;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution thật đã có mấy extension này (FepController gọi
// KeepSafe / KeepNumber / Trunc / KeepEmailAddressSafe). ĐỪNG chép đè.
//
// Quy tắc lọc dưới đây là PHỎNG ĐOÁN theo tên hàm. Test FepController chỉ dùng
// dữ liệu vào đã sạch sẵn, nên có sửa lại luật lọc thì test vẫn xanh.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Tools
{
    public static class StringExtensions
    {
        // CỐ Ý KHÔNG chống null ở đây: tham số khai là string (không nullable), gọi
        // với null là bên gọi sai hợp đồng. Thêm `?? ""` sẽ nuốt mất lỗi
        // FepController.CheckModified đang có — xem test CheckModified_WhenCanBoEmailIsNull.

        /// <summary>Bỏ ký tự điều khiển / ký tự dễ gây injection, cắt khoảng trắng thừa.</summary>
        public static string KeepSafe(this string value) =>
            Regex.Replace(value, @"[^\w\.\-@\+ ]", "").Trim();

        /// <summary>Chỉ giữ chữ số — số điện thoại nhân sự hay có dấu chấm, dấu cách.</summary>
        public static string KeepNumber(this string value) =>
            Regex.Replace(value, @"\D", "");

        /// <summary>Cắt còn tối đa <paramref name="length"/> ký tự. Ngắn hơn thì giữ nguyên.</summary>
        public static string Trunc(this string value, int length) =>
            value.Length <= length ? value : value[..length];

        /// <summary>Như KeepSafe nhưng giữ lại các ký tự hợp lệ của địa chỉ email.</summary>
        public static string KeepEmailAddressSafe(this string value) =>
            Regex.Replace(value, @"[^\w\.\-@\+]", "").Trim();
    }
}
