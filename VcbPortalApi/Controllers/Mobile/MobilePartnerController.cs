using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using VcbPortalApi.DbContext;
using VcbPortalApi.Helpers;
using VcbPortalApi.Models.MP;
using VcbPortalApi.Services;
using VcbPortalApi.StaticData.MP;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — chép lại MobilePartnerController của solution thật để viết test.
// Toàn bộ logic nằm trong action, không tách service, nên test phải đánh thẳng vào
// action. Xem VcbPortalApi.Tests/Controllers/Mobile/MobilePartnerControllerTests.cs.
//
// Điểm khác duy nhất so với bản thật: entity MpAppUser ở repo này đặt property là
// UserName (bản thật là UserName).
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Controllers.Mobile
{
    [Authorize(Policy = "MobileAppPolicy")]
    [Route(BuildSettings.FixedEndpoint + "/ma/partner")]
    [ApiController]
    public class MobilePartnerController(
        FrontendContext context,
        MerchantContext merchantContext,
        MpAppUserStatusService mpAppUserStatusService) : ControllerCustom
    {
        private const string ClaimSessionId = "session_id";
        private const string ClaimPartnerCode = "partner_code";
        private const string SsoAudience = "mobile-partner-sdk";
        private const string Mid = "mid";
        private const string Tid = "tid";

        [HttpPost("token")]
        [Consumes("application/x-www-form-urlencoded")]
        public async Task<IActionResult> IssueSsoToken([FromForm] PartnerSsoTokenForm form)
        {
            if (!ModelState.IsValid)
                return MobileApiError.BaseError("InvalidParameters");

            var userName = CurrentUserName.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(userName))
                return MobileApiError.Unauthorized();

            var partnerCode = (form.PartnerCode ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(partnerCode))
                return MobileApiError.BaseError("PartnerCodeEmpty");

            if (!TryGetBearerTokenExpiresUtc(out var bearerExpiresUtc))
                return MobileApiError.Unauthorized();

            if (bearerExpiresUtc <= DateTime.UtcNow)
                return MobileApiError.Unauthorized();

            var session = await context.MpSessions.AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserName == userName);

            if (session == null || string.IsNullOrWhiteSpace(session.SessionId))
                return MobileApiError.BaseError();

            var common = await context.MpUsersCommons.AsNoTracking()
                .Where(x => x.UserName == userName)
                .Select(x => new { x.Email, x.RoleId })
                .FirstOrDefaultAsync();

            var app = await context.MpAppUsers.AsNoTracking()
                .Where(x => x.UserName == userName)
                .Select(x => new { x.Bid, x.Mid })
                .FirstOrDefaultAsync();

            var email = (common?.Email ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(email))
                return MobileApiError.BaseError();

            var roleId = common?.RoleId;
            var bId = app?.Bid;
            var mId = app?.Mid;

            if (roleId == Roles.RoleBid)
            {
                if (form.Mid is not > 0 || form.Tid is not > 0)
                    return MobileApiError.BaseError("MidOrMidEmptyUserBid");

                if (bId is not > 0)
                    return MobileApiError.BaseError("UserBidInvalid");

                // mid thuộc bid + tid thuộc mid (master), không cần có user APP
                if (!await MobileHelper.IsMidTidUnderBidAsync(
                        merchantContext, bId.Value, form.Mid.Value, form.Tid.Value,
                        cancellationToken: HttpContext.RequestAborted))
                {
                    return MobileApiError.BaseError("MidOrTidNotExistUserBid");
                }
            }
            else if (roleId == Roles.RoleMid)
            {
                if (form.Tid is not > 0)
                    return MobileApiError.BaseError("TidEmptyUserMid");

                if (mId is not > 0)
                    return MobileApiError.BaseError("UserMidInvalid");

                if (!await MobileHelper.IsTidUnderMidAsync(
                        merchantContext, mId.Value, form.Tid.Value,
                        cancellationToken: HttpContext.RequestAborted))
                {
                    return MobileApiError.BaseError("TidNotExistUserMid");
                }
            }

            var claimsIdentity = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, userName),
                new Claim(JwtRegisteredClaimNames.UniqueName, userName),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(ClaimSessionId, session.SessionId.Trim()),
                new Claim(ClaimPartnerCode, partnerCode),
            ]);

            // Claim mid/tid theo giá trị client truyền lên (không lấy mid/tid của user APP)
            if (roleId == Roles.RoleBid)
            {
                claimsIdentity.AddClaim(new Claim(Mid, form.Mid!.Value.ToString()));
                claimsIdentity.AddClaim(new Claim(Tid, form.Tid!.Value.ToString()));
            }
            else if (roleId == Roles.RoleMid)
            {
                claimsIdentity.AddClaim(new Claim(Mid, mId!.Value.ToString()));
                claimsIdentity.AddClaim(new Claim(Tid, form.Tid!.Value.ToString()));
            }

            var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = AppSettings.Issuer,
                Audience = SsoAudience,
                Subject = claimsIdentity,
                Expires = bearerExpiresUtc,
                SigningCredentials = AppSettings.SigningCredentials,
            });

            return MobileApiError.BaseSuccessWithData(new Dictionary<string, object?> { ["token"] = token });
        }
    }
}
