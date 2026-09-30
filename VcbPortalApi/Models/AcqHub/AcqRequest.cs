using System.Text;
using VcbPortalApi.Helpers;
using VcbPortalApi.Tools;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — chép nguyên văn từ ảnh code thật. ĐỪNG chép đè.
//
// Phong bì gửi sang ACQHUB. Ba thứ dễ đoán sai, cả ba đều nằm ở đây:
//   - requestTime là MILI giây và kiểu long (JSON ra SỐ, không có dấu nháy)
//   - chuỗi ký ngăn bằng dấu |, năm phần, data đứng TRƯỚC clientId
//   - apiname cũng nằm trong chuỗi ký — gọi sai URL là checkSum sai theo
//
// Tên thuộc tính viết thường vì ToJsonString() giữ nguyên tên — đổi sang
// PascalCase là bản tin sai khoá.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Models.AcqHub
{
    public class AcqRequest
    {
#pragma warning disable
        public string data { get; set; } = "";
        public string clientId { get; set; } = AppSettings.AcqClientId;
        public long requestTime { get; set; }
        public string? checkSum { get; set; }
#pragma warning restore

        public AcqRequest(string apiname, object payload)
        {
            data = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload.ToJsonString()));
            requestTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            checkSum = Crypto.Sha256EncryptString(
                $"{data}|{clientId}|{requestTime}|{apiname}|{AppSettings.AcqSecretKey}");
        }
    }
}
