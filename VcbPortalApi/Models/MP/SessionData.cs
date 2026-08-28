using VcbPortalApi.Models.MP.User;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution thật đã có SessionData. ĐỪNG chép đè.
// Dựng lại đúng chữ ký mà Authenticate gọi:
//     new SessionData(HttpContext, mpUserFull, null)
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Models.MP
{
    public class SessionData(HttpContext httpContext, MpUserFull user, string? sessionId)
    {
        public string UserName { get; } = user.UserName;
        public decimal RoleId { get; } = user.RoleId;
        public string? SessionId { get; } = sessionId;
        public string? RequestIp { get; } = httpContext.Connection.RemoteIpAddress?.ToString();
    }
}
