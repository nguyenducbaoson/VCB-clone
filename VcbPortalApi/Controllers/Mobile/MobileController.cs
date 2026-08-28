using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VcbPortalApi.DbContext.Oracle;
using VcbPortalApi.Helpers;
using VcbPortalApi.Models.MobileApp;
using VcbPortalApi.Services;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — chép lại phần MobileController của solution thật cần cho test.
// Chỉ giữ action Deactive và GetMinAppVersion; bản thật còn nhiều action khác.
// ĐỪNG chép đè.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Controllers.Mobile
{
    [Authorize(Policy = "MobileAppPolicy")]
    [Route(BuildSettings.FixedEndpoint + "/ma")]
    [ApiController]
    public class MobileController(
        FrontendContext context,
        MerchantContext merchantContext,
        UserAppConfigService userAppConfigService,
        TwoFaService twoFaService) : ControllerCustom
    {
        [AllowAnonymous]
        [HttpGet]
        [Route("app/min_version")]
        public async Task<IActionResult> GetMinAppVersion(CancellationToken cancellationToken)
        {
            var minVersion = await userAppConfigService.GetMinAppVersionAsync(cancellationToken);
            return Ok(new UserAppMinVersionDto { MinVersion = minVersion });
        }

        [HttpPost]
        [Route("deactive")]
        public async Task<IActionResult> Deactive()
        {
            try
            {
                if (!await MobileHelper.DeactivateMobileUserAsync(context, CurrentUserName))
                    return MobileApiError.BaseError();

                return Ok();
            }
            catch (Exception ex)
            {
                return new ErrorMessage(ex).Simplify();
            }
        }
    }
}
