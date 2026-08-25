using VcbPortalApi.Models.MP;
using VcbPortalApi.Models.SSO;
using VcbPortalApi.Services;
using VcbPortalApi.StaticData.MP;

namespace VcbPortalApi.UnitTests.Helpers
{
    /// <summary>
    /// Nhà máy dựng dữ liệu mẫu. Mỗi tham số có giá trị mặc định hợp lệ, test chỉ đặt
    /// lại đúng thứ nó quan tâm:
    ///
    ///     var user = TestDataHelper.CreateAppUser(bid: null);   // chi thieu BID
    ///
    /// Nhờ vậy Arrange ngắn, và người đọc thấy ngay đâu là dữ liệu có ý nghĩa với test.
    ///
    /// Giá trị mặc định lấy từ dữ liệu THẬT trên UAT (bảng MP_APP_PARTNER_CARD_REG),
    /// không phải nghĩ ra — dữ liệu mẫu không giống thực tế thì test chỉ chứng minh
    /// code đúng với giả định của mình.
    /// </summary>
    public static class TestDataHelper
    {
        public const string DefaultUserName = "VATID001";
        public const decimal DefaultBid = 68000000000160;
        public const decimal DefaultMid = 68100000000097;
        public const decimal DefaultTid = 40000001;
        public const string DefaultPartner = MpPartner.PhonePos;
        public const string DefaultEmail = "user@vcb.com.vn";
        public const string DefaultSessionId = "session-1";

        public static MpAppUser CreateAppUser(
            string userName = DefaultUserName,
            decimal? bid = DefaultBid,
            decimal? mid = DefaultMid,
            decimal? phoneposStatus = null,
            decimal? visaacceptStatus = null,
            decimal? finoneStatus = null,
            string? deviceId = null) => new()
            {
                Username = userName,
                Bid = bid,
                Mid = mid,
                PhoneposStatus = phoneposStatus,
                VisaacceptStatus = visaacceptStatus,
                FinoneStatus = finoneStatus,
                Deviceid = deviceId
            };

        public static MpSession CreateSession(
            string userName = DefaultUserName,
            string? sessionId = DefaultSessionId) => new()
            {
                UserName = userName,
                SessionId = sessionId
            };

        public static MpUsersCommon CreateUsersCommon(
            string userName = DefaultUserName,
            string? email = DefaultEmail,
            decimal? roleId = Roles.RoleMid) => new()
            {
                UserName = userName,
                Email = email,
                RoleId = roleId
            };

        public static MpTerminal CreateTerminal(
            int rowId = 1,
            decimal bid = DefaultBid,
            decimal mid = DefaultMid,
            decimal tid = DefaultTid) => new()
            {
                RowId = rowId,
                Bid = bid,
                Mid = mid,
                Tid = tid
            };

        public static PartnerSsoTokenForm CreatePartnerTokenForm(
            string? partnerCode = DefaultPartner,
            decimal? mid = DefaultMid,
            decimal? tid = DefaultTid) => new()
            {
                PartnerCode = partnerCode,
                Mid = mid,
                Tid = tid
            };

        /// <summary>
        /// Các dòng đăng ký partner của một user. Bảng thật không có khoá chính nên
        /// entity dùng khoá giả RowId — helper tự đánh số cho khỏi vướng.
        ///
        /// Bộ đếm là static và tăng dần toàn cục, KHÔNG reset mỗi lần gọi: một test
        /// seed cho nhiều user sẽ gọi hàm này nhiều lần, đánh số lại từ 1 là trùng khoá.
        /// </summary>
        public static MpAppPartnerCardReg[] CreateRegistrations(
            string userName, params (string Partner, string? Status)[] rows) =>
            [.. rows.Select(r => new MpAppPartnerCardReg
            {
                RowId = Interlocked.Increment(ref _nextRowId),
                Username = userName,
                Partner = r.Partner,
                Status = r.Status,
                CreateTime = new DateTime(2026, 1, 1)
            })];

        private static int _nextRowId;
    }
}
