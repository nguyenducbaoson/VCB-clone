using System.ComponentModel.DataAnnotations;

namespace VcbPortalApi.Models.SSO
{
    /// <summary>
    /// Thông tin thiết bị AppMerchant gửi lên, để BE dựng AuditObject cho SSO.
    /// Ánh xạ 1-1 sang VcbPortalApi.Models.SSO.AuditObject có sẵn.
    /// </summary>
    public sealed class ClientContext
    {
        [Required(ErrorMessage = "client.sessionId là bắt buộc")]
        [MaxLength(64)]
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// IP của THIẾT BỊ khách hàng — không phải IP server.
        /// Đi vào ValidateTokenRequestPayload.clientIP; sai thì SSO trả resCode 12.
        /// </summary>
        [MaxLength(45)]
        public string? ClientIp { get; set; }

        /// <summary>IMEI / device token — vào AuditObject.deviceToken và MP_APP_USERS.DEVICEID.</summary>
        [MaxLength(100)]
        public string? DeviceId { get; set; }

        /// <summary>Mã thiết bị theo hãng, ví dụ iPhone14,5 / SM-J730G.</summary>
        [MaxLength(20)]
        public string? DeviceType { get; set; }

        [MaxLength(15)]
        public string? AppVersion { get; set; }

        /// <summary>IOS hoặc ANDROID.</summary>
        [MaxLength(15)]
        public string? OsType { get; set; }

        [MaxLength(15)]
        public string? OsVersion { get; set; }
    }
}
