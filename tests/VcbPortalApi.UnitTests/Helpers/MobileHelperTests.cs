using Microsoft.EntityFrameworkCore;
using VcbPortalApi.DbContext;
using VcbPortalApi.Helpers;
using VcbPortalApi.Models.MP;
using VcbPortalApi.Models.MP.User;
using VcbPortalApi.Models.SSO;
using VcbPortalApi.UnitTests.Fixtures;

namespace VcbPortalApi.UnitTests.Helpers
{
    public class MobileHelperTests
    {
        private readonly FrontendContext _context = TestDb.Create<FrontendContext>();

        private Task<bool> Deactivate(string userName = TestDataHelper.DefaultUserName) =>
            MobileHelper.DeactivateMobileUserAsync(_context, userName);

        private MpUserCommon? ReloadCommon(string userName = TestDataHelper.DefaultUserName) =>
            _context.MpUsersCommons.AsNoTracking().FirstOrDefault(x => x.UserName == userName);

        private MpAppUser? ReloadAppUser(string userName = TestDataHelper.DefaultUserName) =>
            _context.MpAppUsers.AsNoTracking().FirstOrDefault(x => x.UserName == userName);

        // ── Trả false ───────────────────────────────────────────────────────────

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task DeactivateMobileUserAsync_WhenUserNameEmpty_ReturnsFalse(string userName)
        {
            // Arrange
            _context.Seed(TestDataHelper.CreateUsersCommon());

            // Act
            var result = await Deactivate(userName);

            // Assert
            result.Should().BeFalse();
            ReloadCommon()!.Status.Should().Be("A", "khong duoc dung toi user nao");
        }

        [Fact]
        public async Task DeactivateMobileUserAsync_WhenUserNotFound_ReturnsFalse()
        {
            // Arrange — co tinh KHONG seed MpUserCommon

            // Act
            var result = await Deactivate();

            // Assert
            result.Should().BeFalse();
        }

        /// <summary>
        /// Hàm chuẩn hoá username bằng <c>ToUpperInvariant()</c> rồi so khớp CHÍNH XÁC
        /// với cột trong DB. Dữ liệu lưu chữ thường sẽ không bao giờ khớp — user không
        /// vô hiệu hoá được mà không rõ lý do.
        ///
        /// Test ghi lại hành vi hiện tại. Muốn so không phân biệt hoa thường thì phải
        /// sửa truy vấn, và test này sẽ đỏ để nhắc.
        /// </summary>
        [Fact]
        public async Task DeactivateMobileUserAsync_WhenUsernameInDbIsLowercase_ReturnsFalse()
        {
            // Arrange
            _context.Seed(TestDataHelper.CreateUsersCommon(userName: "vatid001"));

            // Act
            var result = await Deactivate();

            // Assert
            result.Should().BeFalse();
        }

        // ── Vô hiệu hoá thành công ──────────────────────────────────────────────

        [Fact]
        public async Task DeactivateMobileUserAsync_WhenUserActive_SetsStatusToD()
        {
            // Arrange
            _context.Seed(TestDataHelper.CreateUsersCommon(status: "A"));

            // Act
            var result = await Deactivate();

            // Assert
            result.Should().BeTrue();
            ReloadCommon()!.Status.Should().Be("D");
        }

        [Theory]
        [InlineData("  VATID001  ")]
        [InlineData("vatid001")]
        public async Task DeactivateMobileUserAsync_WhenUserNameNeedsNormalizing_StillMatches(string userName)
        {
            // Arrange
            _context.Seed(TestDataHelper.CreateUsersCommon());

            // Act
            var result = await Deactivate(userName);

            // Assert
            result.Should().BeTrue();
            ReloadCommon()!.Status.Should().Be("D");
        }

        /// <summary>
        /// Xoá dấu vết thiết bị là mục đích chính: user vô hiệu hoá rồi thì không được
        /// nhận push notification và không được coi là thiết bị đã tin cậy nữa.
        /// </summary>
        [Fact]
        public async Task DeactivateMobileUserAsync_WhenUserHasDeviceInfo_ClearsAllThreeFields()
        {
            // Arrange
            _context.Seed(TestDataHelper.CreateUsersCommon());
            _context.Seed(TestDataHelper.CreateAppUser(
                fcmToken: "fcm-cu", fid: "fid-cu", deviceId: "device-cu"));

            // Act
            await Deactivate();

            // Assert
            var appUser = ReloadAppUser()!;
            appUser.FcmToken.Should().BeNull();
            appUser.Fid.Should().BeNull();
            appUser.DeviceId.Should().BeNull();
        }

        [Theory]
        [InlineData("fcm-con-lai", null, null)]
        [InlineData(null, "fid-con-lai", null)]
        [InlineData(null, null, "device-con-lai")]
        public async Task DeactivateMobileUserAsync_WhenOnlyOneDeviceFieldSet_ClearsAllThree(
            string? fcmToken, string? fid, string? deviceId)
        {
            // Arrange
            _context.Seed(TestDataHelper.CreateUsersCommon());
            _context.Seed(TestDataHelper.CreateAppUser(fcmToken: fcmToken, fid: fid, deviceId: deviceId));

            // Act
            await Deactivate();

            // Assert
            var appUser = ReloadAppUser()!;
            appUser.FcmToken.Should().BeNull();
            appUser.Fid.Should().BeNull();
            appUser.DeviceId.Should().BeNull();
        }

        [Fact]
        public async Task DeactivateMobileUserAsync_WhenNoAppUserRow_StillSetsStatus()
        {
            // Arrange — chi co MpUserCommon
            _context.Seed(TestDataHelper.CreateUsersCommon());

            // Act
            var result = await Deactivate();

            // Assert
            result.Should().BeTrue();
            ReloadCommon()!.Status.Should().Be("D");
        }

        // ── Gọi lại nhiều lần ───────────────────────────────────────────────────

        /// <summary>Client bấm nhầm hai lần, hoặc retry sau timeout — lần hai không được lỗi.</summary>
        [Fact]
        public async Task DeactivateMobileUserAsync_WhenCalledTwice_SecondCallStillReturnsTrue()
        {
            // Arrange
            _context.Seed(TestDataHelper.CreateUsersCommon());
            _context.Seed(TestDataHelper.CreateAppUser());

            // Act
            await Deactivate();
            var result = await Deactivate();

            // Assert
            result.Should().BeTrue();
            ReloadCommon()!.Status.Should().Be("D");
        }

        /// <summary>
        /// So sánh status dùng OrdinalIgnoreCase, nên "d" chữ thường cũng coi là đã vô
        /// hiệu hoá — không ghi đè thành "D".
        /// </summary>
        [Fact]
        public async Task DeactivateMobileUserAsync_WhenStatusIsLowercaseD_LeavesItUnchanged()
        {
            // Arrange
            _context.Seed(TestDataHelper.CreateUsersCommon(status: "d"));

            // Act
            var result = await Deactivate();

            // Assert
            result.Should().BeTrue();
            ReloadCommon()!.Status.Should().Be("d");
        }

        // ── Không được đụng user khác ───────────────────────────────────────────

        /// <summary>
        /// Bắt lỗi kiểu quên mệnh đề WHERE — lỗi mà các test trên không phát hiện được
        /// vì chỉ có một user trong DB.
        /// </summary>
        [Fact]
        public async Task DeactivateMobileUserAsync_WhenOtherUsersExist_DoesNotTouchThem()
        {
            // Arrange
            _context.Seed(TestDataHelper.CreateUsersCommon());
            _context.Seed(TestDataHelper.CreateAppUser());
            _context.Seed(TestDataHelper.CreateUsersCommon(userName: "VATID002", status: "A"));
            _context.Seed(TestDataHelper.CreateAppUser(userName: "VATID002", deviceId: "device-nguoi-khac"));

            // Act
            await Deactivate();

            // Assert
            ReloadCommon("VATID002")!.Status.Should().Be("A");
            ReloadAppUser("VATID002")!.DeviceId.Should().Be("device-nguoi-khac");
        }
    }
}
