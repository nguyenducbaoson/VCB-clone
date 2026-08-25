using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VcbPortalApi.Controllers.Mobile;
using VcbPortalApi.DbContext;
using VcbPortalApi.Models.MP;
using VcbPortalApi.Models.SSO;
using VcbPortalApi.Services;
using VcbPortalApi.UnitTests.Fixtures;
using VcbPortalApi.UnitTests.Helpers;

namespace VcbPortalApi.UnitTests.Controllers.Mobile
{
    /// <summary>
    /// POST /ma/deactive — <see cref="MobileController.Deactive"/>.
    ///
    /// Action mỏng, logic thật nằm trong <c>MobileHelper.DeactivateMobileUserAsync</c>.
    /// Helper là <c>static</c> nên không thay bằng mock được — điều khiển nó bằng dữ
    /// liệu seed vào <see cref="FrontendContext"/>.
    ///
    /// Vì vậy test ở đây kiểm tra hai thứ cùng lúc: action trả đúng gì, và DB đổi đúng
    /// như thế nào. Assert vào DB là phần quan trọng hơn — action chỉ trả <c>Ok()</c>
    /// rỗng nên nhìn response không biết được nó đã làm gì.
    /// </summary>
    public class MobileControllerTests
    {
        private readonly FrontendContext _context = TestDb.Create<FrontendContext>();

        private MobileController CreateController(string? userName = TestDataHelper.DefaultUserName) =>
            new(_context,
                TestDb.Create<MerchantContext>(),
                new UserAppConfigService(_context),
                new TwoFaService(_context))
            {
                ControllerContext = TestHttpContext.Build(userName)
            };

        private MpUsersCommon? ReloadCommon(string userName = TestDataHelper.DefaultUserName) =>
            _context.MpUsersCommons.AsNoTracking().FirstOrDefault(x => x.UserName == userName);

        private MpAppUser? ReloadAppUser(string userName = TestDataHelper.DefaultUserName) =>
            _context.MpAppUsers.AsNoTracking().FirstOrDefault(x => x.UserName == userName);

        // ── Không vô hiệu hoá được ──────────────────────────────────────────────

        [Fact]
        public async Task Deactive_WhenNoIdentity_ReturnsBaseError()
        {
            // Arrange — khong gan claim nen CurrentUserName rong
            _context.Seed(TestDataHelper.CreateUsersCommon());
            var controller = CreateController(userName: null);

            // Act
            var result = await controller.Deactive();

            // Assert
            result.ShouldBeError("BaseError");
        }

        [Fact]
        public async Task Deactive_WhenUserNotFound_ReturnsBaseError()
        {
            // Arrange — co tinh KHONG seed MpUsersCommon
            var controller = CreateController();

            // Act
            var result = await controller.Deactive();

            // Assert
            result.ShouldBeError("BaseError");
        }

        /// <summary>
        /// Helper chuẩn hoá username bằng <c>ToUpperInvariant()</c> rồi so khớp CHÍNH XÁC
        /// với cột trong DB. Dữ liệu lưu chữ thường sẽ không bao giờ khớp — user không
        /// vô hiệu hoá được mà không rõ lý do.
        ///
        /// Test này ghi lại hành vi hiện tại. Nếu nghiệp vụ muốn so không phân biệt hoa
        /// thường thì phải sửa truy vấn, và test này sẽ đỏ để nhắc.
        /// </summary>
        [Fact]
        public async Task Deactive_WhenUsernameInDbIsLowercase_ReturnsBaseError()
        {
            // Arrange
            _context.Seed(TestDataHelper.CreateUsersCommon(userName: "vatid001"));
            var controller = CreateController();

            // Act
            var result = await controller.Deactive();

            // Assert
            result.ShouldBeError("BaseError");
        }

        // ── Vô hiệu hoá thành công ──────────────────────────────────────────────

        [Fact]
        public async Task Deactive_WhenUserActive_SetsStatusToDAndReturnsOk()
        {
            // Arrange
            _context.Seed(TestDataHelper.CreateUsersCommon(status: "A"));
            var controller = CreateController();

            // Act
            var result = await controller.Deactive();

            // Assert
            result.Should().BeOfType<OkResult>();
            ReloadCommon()!.Status.Should().Be("D");
        }

        /// <summary>
        /// Xoá dấu vết thiết bị là mục đích chính của API này: user vô hiệu hoá rồi thì
        /// không được nhận push notification và không được coi là thiết bị đã tin cậy nữa.
        /// </summary>
        [Fact]
        public async Task Deactive_WhenUserHasDeviceInfo_ClearsFcmTokenFidAndDeviceId()
        {
            // Arrange
            _context.Seed(TestDataHelper.CreateUsersCommon());
            _context.Seed(TestDataHelper.CreateAppUser(
                fcmToken: "fcm-token-cu", fid: "fid-cu", deviceId: "device-cu"));
            var controller = CreateController();

            // Act
            var result = await controller.Deactive();

            // Assert
            result.Should().BeOfType<OkResult>();

            var appUser = ReloadAppUser()!;
            appUser.FcmToken.Should().BeNull();
            appUser.Fid.Should().BeNull();
            appUser.DeviceId.Should().BeNull();
        }

        [Theory]
        [InlineData("fcm-con-lai", null, null)]
        [InlineData(null, "fid-con-lai", null)]
        [InlineData(null, null, "device-con-lai")]
        public async Task Deactive_WhenOnlyOneDeviceFieldSet_ClearsAllThree(
            string? fcmToken, string? fid, string? deviceId)
        {
            // Arrange
            _context.Seed(TestDataHelper.CreateUsersCommon());
            _context.Seed(TestDataHelper.CreateAppUser(fcmToken: fcmToken, fid: fid, deviceId: deviceId));
            var controller = CreateController();

            // Act
            await controller.Deactive();

            // Assert
            var appUser = ReloadAppUser()!;
            appUser.FcmToken.Should().BeNull();
            appUser.Fid.Should().BeNull();
            appUser.DeviceId.Should().BeNull();
        }

        [Fact]
        public async Task Deactive_WhenUserHasNoAppUserRow_StillSetsStatusAndReturnsOk()
        {
            // Arrange — chi co MpUsersCommon, khong co MpAppUser
            _context.Seed(TestDataHelper.CreateUsersCommon());
            var controller = CreateController();

            // Act
            var result = await controller.Deactive();

            // Assert
            result.Should().BeOfType<OkResult>();
            ReloadCommon()!.Status.Should().Be("D");
        }

        // ── Gọi lại nhiều lần ───────────────────────────────────────────────────

        /// <summary>
        /// Client bấm nhầm hai lần, hoặc retry sau timeout — lần thứ hai không được lỗi.
        /// </summary>
        [Fact]
        public async Task Deactive_WhenCalledTwice_SecondCallStillReturnsOk()
        {
            // Arrange
            _context.Seed(TestDataHelper.CreateUsersCommon());
            _context.Seed(TestDataHelper.CreateAppUser());
            var controller = CreateController();

            // Act
            await controller.Deactive();
            var result = await controller.Deactive();

            // Assert
            result.Should().BeOfType<OkResult>();
            ReloadCommon()!.Status.Should().Be("D");
        }

        /// <summary>
        /// Đã ở trạng thái "D" và không còn dấu vết thiết bị thì không có gì để đổi.
        /// Helper vẫn trả true nên action trả Ok — không được coi là lỗi.
        /// </summary>
        [Fact]
        public async Task Deactive_WhenNothingToChange_ReturnsOk()
        {
            // Arrange
            _context.Seed(TestDataHelper.CreateUsersCommon(status: "D"));
            _context.Seed(TestDataHelper.CreateAppUser(fcmToken: null, fid: null, deviceId: null));
            var controller = CreateController();

            // Act
            var result = await controller.Deactive();

            // Assert
            result.Should().BeOfType<OkResult>();
        }

        /// <summary>
        /// So sánh status dùng OrdinalIgnoreCase, nên "d" chữ thường cũng coi là đã vô
        /// hiệu hoá — không ghi đè thành "D".
        /// </summary>
        [Fact]
        public async Task Deactive_WhenStatusIsLowercaseD_LeavesItUnchanged()
        {
            // Arrange
            _context.Seed(TestDataHelper.CreateUsersCommon(status: "d"));
            var controller = CreateController();

            // Act
            var result = await controller.Deactive();

            // Assert
            result.Should().BeOfType<OkResult>();
            ReloadCommon()!.Status.Should().Be("d");
        }

        // ── Không được đụng user khác ───────────────────────────────────────────

        /// <summary>
        /// Bắt lỗi kiểu quên mệnh đề WHERE — lỗi mà các test trên không phát hiện được
        /// vì chỉ có một user trong DB.
        /// </summary>
        [Fact]
        public async Task Deactive_WhenOtherUsersExist_DoesNotTouchThem()
        {
            // Arrange
            _context.Seed(TestDataHelper.CreateUsersCommon());
            _context.Seed(TestDataHelper.CreateAppUser());
            _context.Seed(TestDataHelper.CreateUsersCommon(userName: "VATID002", status: "A"));
            _context.Seed(TestDataHelper.CreateAppUser(userName: "VATID002", deviceId: "device-cua-nguoi-khac"));
            var controller = CreateController();

            // Act
            await controller.Deactive();

            // Assert
            ReloadCommon("VATID002")!.Status.Should().Be("A");
            ReloadAppUser("VATID002")!.DeviceId.Should().Be("device-cua-nguoi-khac");
        }
    }
}
