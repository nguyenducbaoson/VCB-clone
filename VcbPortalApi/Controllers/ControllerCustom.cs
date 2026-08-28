using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using VcbPortalApi.Models.MP;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — chép lại phần ControllerCustom mà MobilePartnerController dùng tới.
// Bản thật còn nhiều thành viên khác (CurrentUserTinh/Huyen/Xa, GetRealIp,
// GetAuditData, SendAcqRequest...). ĐỪNG chép đè.
//
// ĐIỂM QUYẾT ĐỊNH CÁCH VIẾT TEST: mọi property đều đọc claim qua
// User.FindFirstValue(AppSettings.Claim*), tức KEY CLAIM NẰM Ở AppSettings.
// Nhờ vậy test không cần biết chuỗi key thật là gì — cứ dùng lại chính hằng đó
// khi dựng ClaimsIdentity thì luôn khớp, kể cả sau này key có đổi.
// Xem VcbPortalApi.Tests/TestSupport/MobileTestKit.CreateController.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Controllers
{
    public class ControllerCustom : ControllerBase
    {
        protected string CurrentUserName
        {
            get
            {
                var userName = User.FindFirstValue(AppSettings.ClaimUserName);
                return userName ?? "";
            }
        }

        protected string CurrentUserSessionId
        {
            get
            {
                var sessionId = User.FindFirstValue(AppSettings.ClaimSessionId);
                return sessionId ?? "";
            }
        }

        protected int CurrentUserRoleId
        {
            get
            {
                var role = User.FindFirstValue(AppSettings.ClaimRoleId);
                if (string.IsNullOrEmpty(role)) return 0;

                int.TryParse(role, out var roleId);
                return roleId;
            }
        }

        protected decimal CurrentUserBid
        {
            get
            {
                var claim = User.FindFirstValue(AppSettings.ClaimBid);
                return string.IsNullOrEmpty(claim) ? decimal.Zero : Convert.ToDecimal(claim);
            }
        }

        protected decimal CurrentUserMid
        {
            get
            {
                var claim = User.FindFirstValue(AppSettings.ClaimMid);
                return string.IsNullOrEmpty(claim) ? decimal.Zero : Convert.ToDecimal(claim);
            }
        }

        protected decimal CurrentUserTid
        {
            get
            {
                var claim = User.FindFirstValue(AppSettings.ClaimTid);
                return string.IsNullOrEmpty(claim) ? decimal.Zero : Convert.ToDecimal(claim);
            }
        }

        /// <summary>
        /// CHƯA ĐỐI CHIẾU VỚI BẢN THẬT — phần này nằm ngoài đoạn đã xem.
        /// Bản dựng ở đây đọc hạn từ JWT trong header Authorization.
        ///
        /// Nếu bản thật đọc claim "exp" trong User thay vì parse header, thì sửa
        /// MobileTestKit.CreateController: gắn thêm Claim("exp", ...) thay vì gắn header.
        /// Toàn bộ 20 test giữ nguyên.
        /// </summary>
        protected bool TryGetBearerTokenExpiresUtc(out DateTime expiresUtc)
        {
            expiresUtc = default;

            var header = Request.Headers.Authorization.ToString();
            if (string.IsNullOrWhiteSpace(header) ||
                !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var raw = header["Bearer ".Length..].Trim();
            if (string.IsNullOrWhiteSpace(raw)) return false;

            try
            {
                var token = new JsonWebTokenHandler().ReadJsonWebToken(raw);
                if (token.ValidTo == default) return false;

                expiresUtc = token.ValidTo;
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ── Hai thành viên FepController.Authenticate dùng tới ──────────────────
        // Bản thật đã có cả hai (ghi chú đầu file). Dựng lại đúng chữ ký.

        /// <summary>
        /// IP thật của client. Bản thật đọc thêm X-Forwarded-For vì API đứng sau
        /// reverse proxy — giữ nguyên tắc đó ở đây.
        /// </summary>
        protected string? GetRealIp() =>
            HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
            ?? HttpContext.Connection.RemoteIpAddress?.ToString();

        /// <summary>
        /// Phát access token. Giữ nguyên tên viết sai chính tả của bản thật
        /// (Genarate, không phải Generate) để code chép sang không phải sửa.
        /// </summary>
        protected string GenarateToken(SessionData session, string? maJob, string? terminalId)
        {
            var claims = new List<Claim>
            {
                new(AppSettings.ClaimUserName, session.UserName),
                new(AppSettings.ClaimRoleId, session.RoleId.ToString(CultureInfo.InvariantCulture))
            };

            if (!string.IsNullOrEmpty(maJob))
                claims.Add(new Claim(AppSettings.ClaimJd, maJob));

            if (!string.IsNullOrEmpty(terminalId))
                claims.Add(new Claim("terminal_id", terminalId));

            return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = AppSettings.Issuer,
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(30),
                SigningCredentials = AppSettings.SigningCredentials
            });
        }
    }
}
