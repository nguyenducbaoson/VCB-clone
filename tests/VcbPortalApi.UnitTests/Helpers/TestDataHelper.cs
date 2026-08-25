using VcbPortalApi.Models.MP;
using VcbPortalApi.Models.SSO;

namespace VcbPortalApi.UnitTests.Helpers
{
    /// <summary>
    /// Nhà máy dựng dữ liệu mẫu. Mỗi tham số có giá trị mặc định hợp lệ, test chỉ đặt
    /// lại đúng thứ nó quan tâm:
    ///
    ///     TestDataHelper.CreateUsersCommon(status: "D")   // chi doi status
    ///
    /// Nhờ vậy Arrange ngắn, và người đọc thấy ngay đâu là dữ liệu có ý nghĩa với test.
    /// </summary>
    public static class TestDataHelper
    {
        /// <summary>
        /// Viết HOA vì code chuẩn hoá username bằng <c>ToUpperInvariant()</c> trước khi
        /// so khớp. Seed chữ thường thì truy vấn không tìm thấy — xem test
        /// <c>Deactive_WhenUsernameInDbIsLowercase_ReturnsBaseError</c>.
        /// </summary>
        public const string DefaultUserName = "VATID001";

        public static MpUsersCommon CreateUsersCommon(
            string userName = DefaultUserName,
            string? email = "user@vcb.com.vn",
            decimal? roleId = 2,
            string? status = "A") => new()
            {
                UserName = userName,
                Email = email,
                RoleId = roleId,
                Status = status
            };

        public static MpAppUser CreateAppUser(
            string userName = DefaultUserName,
            string? fcmToken = "fcm-token-cu",
            string? fid = "fid-cu",
            string? deviceId = "device-cu") => new()
            {
                UserName = userName,
                FcmToken = fcmToken,
                Fid = fid,
                DeviceId = deviceId
            };
    }
}
