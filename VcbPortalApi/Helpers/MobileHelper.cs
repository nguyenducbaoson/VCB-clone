using Microsoft.EntityFrameworkCore;
using VcbPortalApi.DbContext;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution thật đã có MobileHelper. ĐỪNG chép đè.
//
// LƯU Ý KHI TEST: các hàm ở đây là static, KHÔNG thay bằng fake được. Muốn điều
// khiển chúng thì phải seed dữ liệu vào DbContext truyền vào.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Helpers
{
    public static class MobileHelper
    {
        /// <summary>mid thuộc bid VÀ tid thuộc mid.</summary>
        public static Task<bool> IsMidTidUnderBidAsync(
            MerchantContext context, decimal bid, decimal mid, decimal tid,
            CancellationToken cancellationToken = default) =>
            context.MpTerminals.AsNoTracking()
                .AnyAsync(x => x.Bid == bid && x.Mid == mid && x.Tid == tid, cancellationToken);

        /// <summary>tid thuộc mid.</summary>
        public static Task<bool> IsTidUnderMidAsync(
            MerchantContext context, decimal mid, decimal tid,
            CancellationToken cancellationToken = default) =>
            context.MpTerminals.AsNoTracking()
                .AnyAsync(x => x.Mid == mid && x.Tid == tid, cancellationToken);

        /// <summary>
        /// Vô hiệu hoá user mobile: đặt MP_USERS_COMMON.STATUS = "D" và xoá dấu vết
        /// thiết bị trong MP_APP_USERS (FCM_TOKEN, FID, DEVICE_ID).
        ///
        /// Trả false khi username rỗng hoặc không tìm thấy user.
        /// Trả true kể cả khi không có gì để đổi — gọi lại nhiều lần vẫn an toàn.
        /// </summary>
        public static async Task<bool> DeactivateMobileUserAsync(
            FrontendContext context,
            string userName,
            CancellationToken cancellationToken = default)
        {
            var normalized = userName.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(normalized))
                return false;

            var common = await context.MpUsersCommons
                .FirstOrDefaultAsync(x => x.UserName == normalized, cancellationToken);

            if (common == null)
                return false;

            var changed = false;
            if (!string.Equals(common.Status, "D", StringComparison.OrdinalIgnoreCase))
            {
                common.Status = "D";
                changed = true;
            }

            var appUser = await context.MpAppUsers
                .FirstOrDefaultAsync(x => x.UserName == normalized, cancellationToken);

            if (appUser != null
                && (appUser.FcmToken != null || appUser.Fid != null || appUser.DeviceId != null))
            {
                appUser.FcmToken = null;
                appUser.Fid = null;
                appUser.DeviceId = null;
                changed = true;
            }

            if (changed)
                await context.SaveChangesAsync(cancellationToken);

            return true;
        }
    }
}
