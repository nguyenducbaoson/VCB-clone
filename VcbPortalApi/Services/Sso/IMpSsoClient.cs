using VcbPortalApi.Models.SSO;

namespace VcbPortalApi.Services.Sso
{
    /// <summary>
    /// Đầu vào gọi SSO. Mọi giá trị phải lấy từ payload AppMerchant gửi lên,
    /// không tự sinh và không lấy từ DB.
    /// </summary>
    public sealed record MpSsoVerifyInput(string AccessTokenSSO, string ClientIp, AuditObject Audit);

    public interface IMpSsoClient
    {
        Task<MpSsoVerifyResult> ValidateAccessTokenAsync(MpSsoVerifyInput input, CancellationToken ct = default);
    }

    public sealed class MpSsoUnavailableException : Exception
    {
        public MpSsoUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
    }
}
