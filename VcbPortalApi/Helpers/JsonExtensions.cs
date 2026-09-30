using Newtonsoft.Json;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — bản thật đã có ToJsonString(). ĐỪNG chép đè.
//
// Giữ NGUYÊN tên thuộc tính, không đổi sang camelCase: AcqRequest khai
// data/clientId/requestTime/checkSum viết thường sẵn, và payload gửi sang ACQHUB
// cũng phải giữ đúng tên khoá trong tài liệu.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Helpers
{
    public static class JsonExtensions
    {
        public static string ToJsonString(this object obj) =>
            JsonConvert.SerializeObject(obj);
    }
}
