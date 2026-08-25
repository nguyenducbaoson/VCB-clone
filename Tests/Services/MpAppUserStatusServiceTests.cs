using Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using VcbPortalApi.Services;

namespace Tests.Services
{
    /// <summary>
    /// MẪU 2 — TEST SERVICE CÓ ĐỤNG DATABASE.
    ///
    /// Ba bước cố định, viết theo thứ tự này thì test nào cũng dễ đọc:
    ///   Arrange — dựng DB rỗng, đổ dữ liệu mẫu, tạo service
    ///   Act     — gọi đúng MỘT hàm cần kiểm tra
    ///   Assert  — kiểm tra kết quả
    ///
    /// DB ở đây là EF Core InMemory, không phải Oracle: chạy trong tiến trình test,
    /// mỗi test một database riêng, không cần kết nối và không để lại rác.
    /// </summary>
    public class MpAppUserStatusServiceTests
    {
        private static MpAppUserStatusService CreateService(VcbPortalApi.DbContext.VcbPortalDbContext db) =>
            new(db, NullLogger<MpAppUserStatusService>.Instance);

        [Fact]
        public async Task RefreshStatusAsync_CapNhatCaHaiCotTheoBanGhiDangKy()
        {
            // Arrange — VATID001 trên UAT: 3 dòng VISAACCEPT (3, 3, 0) và 1 dòng PHONEPOS (2)
            using var db = TestDb.Create();
            var user = TestDb.SeedUser(db, "VATID001");
            TestDb.SeedRegistrations(db, "VATID001",
                ("VISAACCEPT", "3"),
                ("VISAACCEPT", "3"),
                ("VISAACCEPT", "0"),
                ("PHONEPOS",   "2"));

            // Act
            await CreateService(db).RefreshStatusAsync("VATID001");

            // Assert
            Assert.Equal(MpAppUserStatus.KichHoat, user.PhoneposStatus);
            Assert.Equal(MpAppUserStatus.KichHoat, user.VisaacceptStatus);
        }

        [Fact]
        public async Task RefreshStatusAsync_HaiPartnerCoTheRaHaiTrangThaiKhacNhau()
        {
            using var db = TestDb.Create();
            var user = TestDb.SeedUser(db, "VATID004");
            TestDb.SeedRegistrations(db, "VATID004",
                ("PHONEPOS",   "2"),
                ("VISAACCEPT", "0"));

            await CreateService(db).RefreshStatusAsync("VATID004");

            Assert.Equal(MpAppUserStatus.KichHoat, user.PhoneposStatus);
            Assert.Equal(MpAppUserStatus.DaDangKy, user.VisaacceptStatus);
        }

        [Fact]
        public async Task RefreshStatusAsync_KhongCoBanGhiNao_TraVeChuaDangKy()
        {
            using var db = TestDb.Create();
            var user = TestDb.SeedUser(db, "TIENNX", phonepos: 2, visaaccept: 2);

            await CreateService(db).RefreshStatusAsync("TIENNX");

            Assert.Null(user.PhoneposStatus);
            Assert.Null(user.VisaacceptStatus);
        }

        /// <summary>
        /// FINONE_STATUS do luồng khác quản lý. Chưa có bản ghi PARTNER nào cho FinOne nên
        /// nếu service tính luôn cột này thì sẽ ra null và xoá mất dữ liệu của luồng kia.
        /// </summary>
        [Fact]
        public async Task RefreshStatusAsync_KhongDungToiFinoneStatus()
        {
            using var db = TestDb.Create();
            var user = TestDb.SeedUser(db, "VATID001", finone: 2);
            TestDb.SeedRegistrations(db, "VATID001", ("PHONEPOS", "0"));

            await CreateService(db).RefreshStatusAsync("VATID001");

            Assert.Equal(2, user.FinoneStatus);
        }

        [Theory]
        [InlineData("vatid001")]
        [InlineData("  VATID001  ")]
        public async Task RefreshStatusAsync_KhopUsernameKhongPhanBietHoaThuong(string usernameGoiVao)
        {
            using var db = TestDb.Create();
            var user = TestDb.SeedUser(db, "VATID001");
            TestDb.SeedRegistrations(db, "VATID001", ("PHONEPOS", "2"));

            await CreateService(db).RefreshStatusAsync(usernameGoiVao);

            Assert.Equal(MpAppUserStatus.KichHoat, user.PhoneposStatus);
        }

        /// <summary>
        /// User không có trong MP_APP_USERS thì ghi log cảnh báo rồi bỏ qua. Ném exception
        /// ở đây sẽ làm hỏng luồng gọi nó, trong khi đây là tình huống dữ liệu bình thường.
        /// </summary>
        [Fact]
        public async Task RefreshStatusAsync_KhongTimThayUser_KhongNemException()
        {
            using var db = TestDb.Create();
            TestDb.SeedRegistrations(db, "KHONG_TON_TAI", ("PHONEPOS", "2"));

            var ex = await Record.ExceptionAsync(() =>
                CreateService(db).RefreshStatusAsync("KHONG_TON_TAI"));

            Assert.Null(ex);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task RefreshStatusAsync_UsernameRong_KhongNemException(string? username)
        {
            using var db = TestDb.Create();

            var ex = await Record.ExceptionAsync(() => CreateService(db).RefreshStatusAsync(username!));

            Assert.Null(ex);
        }

        /// <summary>
        /// Bản ghi của user khác không được lẫn vào. Test này bắt lỗi kiểu quên mệnh đề
        /// WHERE username — lỗi mà các test trên không phát hiện được vì chỉ có một user.
        /// </summary>
        [Fact]
        public async Task RefreshStatusAsync_KhongLayNhamBanGhiCuaUserKhac()
        {
            using var db = TestDb.Create();
            var user = TestDb.SeedUser(db, "VATID005");
            TestDb.SeedRegistrations(db, "VATID005", ("PHONEPOS", "0"));
            TestDb.SeedRegistrations(db, "VATID002", ("PHONEPOS", "2"));

            await CreateService(db).RefreshStatusAsync("VATID005");

            Assert.Equal(MpAppUserStatus.DaDangKy, user.PhoneposStatus);
        }
    }
}
