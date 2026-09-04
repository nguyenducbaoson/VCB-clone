using Microsoft.EntityFrameworkCore;
using VcbPortalApi.DbContext.Oracle;
using VcbPortalApi.Helpers;
using VcbPortalApi.Models.MobileApp;
using VcbPortalApi.UnitTests.Fixtures;

namespace VcbPortalApi.UnitTests.Helpers
{
    /// <summary>
    /// <c>Insert</c> và <c>CountConsecutiveFailuresAsync</c> NHẬN FrontendContext qua
    /// tham số nên test được thẳng, không cần khe nào.
    ///
    /// <c>TryLog</c>/<c>TryLogAsync</c> thì không: chúng tự dựng
    /// <c>new FrontendContext()</c> trong thân hàm.
    ///
    /// Đây là log hành vi người dùng — dữ liệu dùng để điều tra sự cố và đếm số lần
    /// đăng nhập sai liên tiếp, nên cắt sai độ dài hay chuẩn hoá sai là hỏng cả hai
    /// mục đích đó mà không ai biết.
    /// </summary>
    public class UserActionLogHelperTests
    {
        private readonly FrontendContext _context = TestDb.Create<FrontendContext>();

        private const string Action = UserActionLogTypes.Action.LoginMobile;
        private const string UserName = "VATID001";

        private MpAppUserActionLog Written() => _context.MpAppUserActionLogs.AsNoTracking().Single();

        private void InsertAndSave(
            string action = Action,
            string result = UserActionLogTypes.ResultCode.WrongPassword,
            string? userName = UserName,
            string? message = null,
            string? extraData = null,
            string? requestIp = null,
            string? source = null)
        {
            UserActionLogHelper.Insert(_context, action, result, userName, message, extraData, requestIp, source);
            _context.SaveChanges();
        }

        // ── Insert: điều kiện chặn ──────────────────────────────────────────────

        /// <summary>Action là thứ phân loại log — thiếu nó thì bản ghi vô nghĩa.</summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Insert_WhenActionIsBlank_Throws(string? action)
        {
            var act = () => UserActionLogHelper.Insert(_context, action!, UserActionLogTypes.ResultCode.Success);

            act.Should().Throw<ArgumentException>();
        }

        /// <summary>Cột RESULT chỉ chứa 10 ký tự — dài hơn thì chặn ngay, không cắt âm thầm.</summary>
        [Fact]
        public void Insert_WhenResultLongerThanTenChars_Throws()
        {
            var act = () => UserActionLogHelper.Insert(_context, Action, new string('X', 11));

            act.Should().Throw<ArgumentException>();
        }

        /// <summary>Đúng 10 ký tự thì vẫn qua — kiểm chốt biên.</summary>
        [Fact]
        public void Insert_WhenResultIsExactlyTenChars_IsAccepted()
        {
            var act = () => UserActionLogHelper.Insert(_context, Action, new string('X', 10));

            act.Should().NotThrow();
        }

        // ── Insert: hành vi ─────────────────────────────────────────────────────

        /// <summary>
        /// Insert chỉ Add vào context, KHÔNG tự SaveChanges — để bên gọi ghi log
        /// chung transaction với nghiệp vụ. Quên SaveChanges là mất log.
        /// </summary>
        [Fact]
        public void Insert_DoesNotSaveByItself()
        {
            UserActionLogHelper.Insert(_context, Action, UserActionLogTypes.ResultCode.Success, UserName);

            _context.MpAppUserActionLogs.AsNoTracking().Should().BeEmpty();

            _context.SaveChanges();

            _context.MpAppUserActionLogs.AsNoTracking().Should().ContainSingle();
        }

        /// <summary>UserName, Action và Source được viết HOA và cắt khoảng trắng.</summary>
        [Fact]
        public void Insert_NormalisesUserNameActionAndSourceToUpper()
        {
            InsertAndSave(
                action: "  login_mobile  ",
                userName: "  vatid001  ",
                source: "  web  ");

            var log = Written();
            log.Action.Should().Be("LOGIN_MOBILE");
            log.UserName.Should().Be("VATID001");
            log.Source.Should().Be("WEB");
        }

        /// <summary>
        /// Message/ExtraData/RequestIp KHÔNG được chuẩn hoá hoa thường — chỉ cắt độ
        /// dài. Giữ nguyên vì đó là dữ liệu để đọc, không phải khoá tra cứu.
        /// </summary>
        [Fact]
        public void Insert_KeepsMessageAndIpAsTyped()
        {
            InsertAndSave(message: "  Wrong password  ", requestIp: "10.0.0.1");

            var log = Written();
            log.Message.Should().Be("  Wrong password  ");
            log.RequestIp.Should().Be("10.0.0.1");
        }

        /// <summary>
        /// Mỗi cột một giới hạn riêng. Cắt sai chỗ nào là Oracle ném ORA-12899 lúc
        /// ghi, mà log lại nằm trong khối catch nên hỏng lặng lẽ.
        /// </summary>
        [Fact]
        public void Insert_TruncatesEachFieldToItsColumnLength()
        {
            InsertAndSave(
                action: new string('A', 80),
                userName: new string('U', 150),
                message: new string('M', 700),
                extraData: new string('E', 2500),
                requestIp: new string('I', 150),
                source: new string('S', 40));

            var log = Written();
            log.Action.Should().HaveLength(50);
            log.UserName.Should().HaveLength(100);
            log.Message.Should().HaveLength(500);
            log.ExtraData.Should().HaveLength(2000);
            log.RequestIp.Should().HaveLength(100);
            log.Source.Should().HaveLength(20);
        }

        /// <summary>Tham số tuỳ chọn bỏ trống thì để null, không đổi thành chuỗi rỗng.</summary>
        [Fact]
        public void Insert_WhenOptionalFieldsOmitted_LeavesThemNull()
        {
            InsertAndSave(userName: null);

            var log = Written();
            log.UserName.Should().BeNull();
            log.Message.Should().BeNull();
            log.ExtraData.Should().BeNull();
            log.RequestIp.Should().BeNull();
            log.Source.Should().BeNull();
        }

        /// <summary>CreateTime do chính hàm đặt, bên gọi không phải truyền.</summary>
        [Fact]
        public void Insert_StampsCreateTime()
        {
            var before = DateTime.Now.AddSeconds(-1);

            InsertAndSave();

            Written().CreateTime.Should().BeAfter(before);
        }

        // ── CountConsecutiveFailuresAsync ───────────────────────────────────────

        private void SeedLog(string result, DateTime createTime, string userName = UserName, string action = Action)
        {
            _context.MpAppUserActionLogs.Add(new MpAppUserActionLog
            {
                CreateTime = createTime,
                UserName = userName,
                Action = action,
                Result = result
            });

            _context.SaveChanges();
        }

        private Task<int> CountFailures(string userName = UserName, string action = Action) =>
            UserActionLogHelper.CountConsecutiveFailuresAsync(_context, userName, action);

        [Theory]
        [InlineData("", Action)]
        [InlineData("   ", Action)]
        [InlineData(UserName, "")]
        [InlineData(UserName, "   ")]
        public async Task CountConsecutiveFailuresAsync_WhenUserNameOrActionBlank_ReturnsZero(
            string userName, string action)
        {
            SeedLog(UserActionLogTypes.ResultCode.WrongPassword, DateTime.Now);

            var count = await CountFailures(userName, action);

            count.Should().Be(0);
        }

        [Fact]
        public async Task CountConsecutiveFailuresAsync_WhenNoLogs_ReturnsZero()
        {
            var count = await CountFailures();

            count.Should().Be(0);
        }

        /// <summary>
        /// Đếm từ bản ghi MỚI NHẤT lùi lại, dừng ở lần thành công đầu tiên. Nhờ vậy
        /// đăng nhập đúng một lần là chuỗi thất bại được đặt lại về 0.
        /// </summary>
        [Fact]
        public async Task CountConsecutiveFailuresAsync_StopsAtMostRecentSuccess()
        {
            var now = DateTime.Now;

            SeedLog(UserActionLogTypes.ResultCode.WrongPassword, now.AddMinutes(-5));
            SeedLog(UserActionLogTypes.ResultCode.WrongPassword, now.AddMinutes(-4));
            SeedLog(UserActionLogTypes.ResultCode.Success, now.AddMinutes(-3));
            SeedLog(UserActionLogTypes.ResultCode.WrongPassword, now.AddMinutes(-2));
            SeedLog(UserActionLogTypes.ResultCode.WrongPassword, now.AddMinutes(-1));

            var count = await CountFailures();

            count.Should().Be(2, "chi dem toi lan thanh cong gan nhat");
        }

        /// <summary>Lần gần nhất thành công thì chuỗi thất bại bằng 0.</summary>
        [Fact]
        public async Task CountConsecutiveFailuresAsync_WhenNewestIsSuccess_ReturnsZero()
        {
            var now = DateTime.Now;

            SeedLog(UserActionLogTypes.ResultCode.WrongPassword, now.AddMinutes(-2));
            SeedLog(UserActionLogTypes.ResultCode.Success, now.AddMinutes(-1));

            var count = await CountFailures();

            count.Should().Be(0);
        }

        /// <summary>Tên đăng nhập và action so khớp sau khi viết HOA — nhập thường vẫn ra đúng.</summary>
        [Fact]
        public async Task CountConsecutiveFailuresAsync_MatchesUserNameAndActionIgnoringCase()
        {
            SeedLog(UserActionLogTypes.ResultCode.WrongPassword, DateTime.Now);

            var count = await CountFailures(userName: "  vatid001  ", action: Action.ToLowerInvariant());

            count.Should().Be(1);
        }

        /// <summary>Log của user khác không được tính vào chuỗi của user này.</summary>
        [Fact]
        public async Task CountConsecutiveFailuresAsync_IgnoresOtherUsers()
        {
            var now = DateTime.Now;

            SeedLog(UserActionLogTypes.ResultCode.WrongPassword, now.AddMinutes(-1), userName: "NGUOI_KHAC");
            SeedLog(UserActionLogTypes.ResultCode.WrongPassword, now, userName: UserName);

            var count = await CountFailures();

            count.Should().Be(1);
        }

        /// <summary>Log của action khác cũng không được tính lẫn.</summary>
        [Fact]
        public async Task CountConsecutiveFailuresAsync_IgnoresOtherActions()
        {
            var now = DateTime.Now;

            SeedLog(UserActionLogTypes.ResultCode.WrongPassword, now.AddMinutes(-1), action: "DOI_MAT_KHAU");
            SeedLog(UserActionLogTypes.ResultCode.WrongPassword, now, action: Action);

            var count = await CountFailures();

            count.Should().Be(1);
        }

        /// <summary>
        /// Hàm chỉ lấy 50 bản ghi gần nhất. Ai đó sai mật khẩu quá 50 lần liên tiếp
        /// thì con số trả về CHẶN Ở 50, không phải số thật — bên gọi phải hiểu đó là
        /// "ít nhất 50" chứ không phải "đúng 50".
        /// </summary>
        [Fact]
        public async Task CountConsecutiveFailuresAsync_CapsAtFiftyMostRecent()
        {
            var now = DateTime.Now;

            for (var i = 0; i < 60; i++)
                SeedLog(UserActionLogTypes.ResultCode.WrongPassword, now.AddMinutes(-i));

            var count = await CountFailures();

            count.Should().Be(50);
        }
    }
}
