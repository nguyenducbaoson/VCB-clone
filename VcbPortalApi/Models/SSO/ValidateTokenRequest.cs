using Newtonsoft.Json;
using VcbPortalApi.Helpers;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution VcbPortalApi thật ĐÃ CÓ Models/SSO/ValidateTokenRequest.cs.
// Dựng lại đủ những gì MpSsoClient đang dùng để repo build được. ĐỪNG chép đè.
//
// Property đặt tên chữ thường vì phải serialize đúng khuôn JSON VCB yêu cầu.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Models.SSO
{
    public class AuditObject
    {
        public string sessionId { get; set; } = null!;
        public string clientType { get; set; } = null!;
        public string deviceToken { get; set; } = null!;
        public string deviceType { get; set; } = null!;
        public string appVersion { get; set; } = null!;
        public string osType { get; set; } = null!;
        public string osVersion { get; set; } = null!;

        /// <summary>Chỉ dùng khi clientType = WEB.</summary>
        public string? userAgent { get; set; }
    }

    public class SsoBaseRequestHeader
    {
        public string msgID { get; set; } = null!;
        public string msgName { get; set; } = null!;
        public string appId { get; set; } = null!;
        public string requestTime { get; set; } = null!;
    }

    public class SsoBaseResponseHeader
    {
        public string? msgID { get; set; }
        public string? msgName { get; set; }
        public int resCode { get; set; }
        public string? resMessage { get; set; }
        public string? responseTime { get; set; }
    }

    public class ValidateTokenRequestPayload
    {
        public string clientIP { get; set; } = null!;
        public string accessTokenSSO { get; set; } = null!;
        public AuditObject auditObject { get; set; } = null!;
    }

    /// <summary>
    /// CẦN SỬA Ở SOLUTION THẬT: thêm othersInfo và custClass vào class có sẵn.
    /// Toàn bộ việc đối chiếu username/bid/mid/tid dựa vào othersInfo, bản gốc chưa có.
    /// </summary>
    public class ValidateTokenResponsePayload
    {
        public string? loginURL { get; set; }
        public string? loginTokenSSO { get; set; }
        public string? userId { get; set; }
        public string? userFullName { get; set; }
        public string? userRole { get; set; }
        public string? userOf { get; set; }

        /// <summary>Response mẫu trả số: 24094302. CIF có số 0 đứng đầu sẽ bị mất.</summary>
        public int userCIF { get; set; }

        public Dictionary<string, object>? othersInfo { get; set; }
        public string? custClass { get; set; }
    }

    public class ValidateTokenRequest
    {
        public SsoBaseRequestHeader HeaderObj { get; set; } = new();
        public ValidateTokenRequestPayload PayloadObj { get; set; } = new();

        public string? signature { get; private set; }

        /// <summary>
        /// Chữ ký = HMAC-SHA256(headerJson + payloadJson, secretKey), hex CHỮ HOA.
        /// Ký tay ra chữ thường là bị SSO từ chối.
        /// </summary>
        public void CreateSignature()
        {
            var headerJson = JsonConvert.SerializeObject(HeaderObj);
            var payloadJson = JsonConvert.SerializeObject(PayloadObj);

            signature = HMAC256.HmacSha256(headerJson + payloadJson, AppSettings.SsoPrdHmacSecretKey);
        }

        /// <summary>Body gửi đi: header/payload là CHUỖI JSON lồng, không phải object.</summary>
        public string CreateStringContent() => JsonConvert.SerializeObject(new
        {
            header = JsonConvert.SerializeObject(HeaderObj),
            payload = JsonConvert.SerializeObject(PayloadObj),
            signature
        });
    }
}
