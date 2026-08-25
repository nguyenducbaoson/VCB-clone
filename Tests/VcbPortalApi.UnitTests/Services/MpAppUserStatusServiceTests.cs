using Microsoft.Extensions.Logging.Abstractions;
using VcbPortalApi.DbContext;
using VcbPortalApi.Services;
using VcbPortalApi.UnitTests.Fixtures;
using VcbPortalApi.UnitTests.Helpers;

namespace VcbPortalApi.UnitTests.Services
{
    /// <summary>
    /// Service có đụng DbContext. Dùng EF Core InMemory: chạy trong tiến trình test,
    /// mỗi test một database riêng, không cần Oracle và không để lại rác.
    ///
    /// InMemory KHÔNG phải Oracle — nó không kiểm tra ràng buộc, không biết ORA-12899,
    /// và dịch LINQ theo luật C# chứ không phải SQL. Dùng để test LOGIC.
    /// </summary>
    public class MpAppUserStatusServiceTests
    {
        private readonly VcbPortalDbContext _db = TestDb.Create<VcbPortalDbContext>();

        private MpAppUserStatusService CreateService() =>
            new(_db, NullLogger<MpAppUserStatusService>.Instance);

        [Fact]
        public async Task RefreshStatusAsync_WhenUserHasBothPartners_UpdatesBothColumns()
        {
            // Arrange — VATID001 tren UAT: 3 dong VISAACCEPT (3, 3, 0) va 1 dong PHONEPOS (2)
            var user = _db.Seed(TestDataHelper.CreateAppUser());
            _db.SeedRange(TestDataHelper.CreateRegistrations(TestDataHelper.DefaultUserName,
                (MpPartner.VisaAccept, "3"),
                (MpPartner.VisaAccept, "3"),
                (MpPartner.VisaAccept, "0"),
                (MpPartner.PhonePos,   "2")));

            // Act
            await CreateService().RefreshStatusAsync(TestDataHelper.DefaultUserName);

            // Assert
            user.PhoneposStatus.Should().Be(MpAppUserStatus.KichHoat);
            user.VisaacceptStatus.Should().Be(MpAppUserStatus.KichHoat);
        }

        [Fact]
        public async Task RefreshStatusAsync_WhenPartnersDiffer_ResolvesEachIndependently()
        {
            // Arrange
            var user = _db.Seed(TestDataHelper.CreateAppUser());
            _db.SeedRange(TestDataHelper.CreateRegistrations(TestDataHelper.DefaultUserName,
                (MpPartner.PhonePos,   "2"),
                (MpPartner.VisaAccept, "0")));

            // Act
            await CreateService().RefreshStatusAsync(TestDataHelper.DefaultUserName);

            // Assert
            user.PhoneposStatus.Should().Be(MpAppUserStatus.KichHoat);
            user.VisaacceptStatus.Should().Be(MpAppUserStatus.DaDangKy);
        }

        [Fact]
        public async Task RefreshStatusAsync_WhenNoRegistration_ClearsBothColumns()
        {
            // Arrange
            var user = _db.Seed(TestDataHelper.CreateAppUser(phoneposStatus: 2, visaacceptStatus: 2));

            // Act
            await CreateService().RefreshStatusAsync(TestDataHelper.DefaultUserName);

            // Assert
            user.PhoneposStatus.Should().BeNull();
            user.VisaacceptStatus.Should().BeNull();
        }

        /// <summary>
        /// FINONE_STATUS do luồng khác quản lý. Chưa có bản ghi PARTNER nào cho FinOne
        /// nên nếu service tính luôn cột này thì sẽ ra null và xoá mất dữ liệu luồng kia.
        /// </summary>
        [Fact]
        public async Task RefreshStatusAsync_WhenUpdating_DoesNotTouchFinoneStatus()
        {
            // Arrange
            var user = _db.Seed(TestDataHelper.CreateAppUser(finoneStatus: 2));
            _db.SeedRange(TestDataHelper.CreateRegistrations(TestDataHelper.DefaultUserName,
                (MpPartner.PhonePos, "0")));

            // Act
            await CreateService().RefreshStatusAsync(TestDataHelper.DefaultUserName);

            // Assert
            user.FinoneStatus.Should().Be(2);
        }

        [Theory]
        [InlineData("vatid001")]
        [InlineData("  VATID001  ")]
        public async Task RefreshStatusAsync_WhenUsernameCasingDiffers_StillMatches(string username)
        {
            // Arrange
            var user = _db.Seed(TestDataHelper.CreateAppUser());
            _db.SeedRange(TestDataHelper.CreateRegistrations(TestDataHelper.DefaultUserName,
                (MpPartner.PhonePos, "2")));

            // Act
            await CreateService().RefreshStatusAsync(username);

            // Assert
            user.PhoneposStatus.Should().Be(MpAppUserStatus.KichHoat);
        }

        /// <summary>
        /// User không có trong MP_APP_USERS thì ghi log cảnh báo rồi bỏ qua. Ném exception
        /// ở đây sẽ làm hỏng luồng gọi nó, trong khi đây là tình huống dữ liệu bình thường.
        /// </summary>
        [Fact]
        public async Task RefreshStatusAsync_WhenUserNotFound_DoesNotThrow()
        {
            // Arrange
            _db.SeedRange(TestDataHelper.CreateRegistrations("KHONG_TON_TAI",
                (MpPartner.PhonePos, "2")));

            // Act
            var act = () => CreateService().RefreshStatusAsync("KHONG_TON_TAI");

            // Assert
            await act.Should().NotThrowAsync();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task RefreshStatusAsync_WhenUsernameEmpty_DoesNotThrow(string? username)
        {
            // Arrange & Act
            var act = () => CreateService().RefreshStatusAsync(username!);

            // Assert
            await act.Should().NotThrowAsync();
        }

        /// <summary>
        /// Bản ghi của user khác không được lẫn vào. Test này bắt lỗi kiểu quên mệnh đề
        /// WHERE username — lỗi mà các test trên không phát hiện được vì chỉ có một user.
        /// </summary>
        [Fact]
        public async Task RefreshStatusAsync_WhenOtherUsersHaveRegistrations_IgnoresThem()
        {
            // Arrange
            var user = _db.Seed(TestDataHelper.CreateAppUser("VATID005"));
            _db.SeedRange(TestDataHelper.CreateRegistrations("VATID005", (MpPartner.PhonePos, "0")));
            _db.SeedRange(TestDataHelper.CreateRegistrations("VATID002", (MpPartner.PhonePos, "2")));

            // Act
            await CreateService().RefreshStatusAsync("VATID005");

            // Assert
            user.PhoneposStatus.Should().Be(MpAppUserStatus.DaDangKy);
        }
    }
}
