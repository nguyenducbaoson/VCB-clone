using VcbPortalApi.Services;

namespace VcbPortalApi.UnitTests.Services
{
    /// <summary>
    /// Quy tắc suy ra trạng thái partner của user.
    ///
    /// Hàm được test là <c>static</c>, vào ra thuần tuý — không DB, không mạng.
    /// Đây là loại test rẻ nhất và nên ưu tiên.
    ///
    /// Bảng đặc tả nghiệp vụ:
    ///   Tồn tại ít nhất 1 bản ghi trạng thái 2/3/4/5/6  -> Kích hoạt    (2)
    ///   Tất cả bản ghi trạng thái 7                     -> Hủy          (7)
    ///   Tất cả bản ghi trạng thái 0, hoặc 0 và 7        -> Đã đăng ký   (0)
    ///   Các trường hợp còn lại                          -> Chưa đăng ký (null)
    /// </summary>
    public class MpAppUserStatusLogicTests
    {
        [Theory]
        // Khong co ban ghi nao
        [InlineData("",      null)]
        // Da dang ky
        [InlineData("0",     0)]
        [InlineData("0,0",   0)]
        [InlineData("0,7",   0)]
        [InlineData("7,7,0", 0)]
        // Kich hoat - ca dai 2..6
        [InlineData("2",     2)]
        [InlineData("3",     2)]
        [InlineData("4",     2)]
        [InlineData("5",     2)]
        [InlineData("6",     2)]
        // Huy
        [InlineData("7",     7)]
        [InlineData("7,7",   7)]
        // Thu tu nhanh: co ca 2 lan 7 phai ra Kich hoat, khong phai Huy
        [InlineData("2,7",   2)]
        [InlineData("7,0,3", 2)]
        // Gia tri khong roi vao nhanh nao
        [InlineData("1",     null)]
        [InlineData("8",     null)]
        [InlineData("0,1",   null)]
        [InlineData("7,1",   null)]
        // STATUS la VARCHAR2(2) nen co the chua rac - khong duoc nem exception
        [InlineData("X",     null)]
        [InlineData("0,X",   null)]
        [InlineData("2,X",   2)]
        // Khoang trang thua van phai parse duoc
        [InlineData(" 2 ",   2)]
        // Ca that cua VATID001 tren UAT: 3 dong VISAACCEPT
        [InlineData("3,3,0", 2)]
        public void ResolveForPartner_WhenGivenStatuses_ReturnsExpectedStatus(
            string statusesCsv, int? expected)
        {
            // Arrange
            var rows = BuildRows(MpPartner.PhonePos, statusesCsv);

            // Act
            var result = MpAppUserStatusService.ResolveForPartner(rows, MpPartner.PhonePos);

            // Assert — ép kiểu vì attribute trong C# không nhận hằng decimal, nên
            // InlineData chỉ truyền được int còn hàm trả về decimal?.
            result.Should().Be((decimal?)expected);
        }

        [Fact]
        public void ResolveForPartner_WhenOtherPartnersPresent_IgnoresThem()
        {
            // Arrange
            var rows = new (string? Partner, string? Status)[]
            {
                (MpPartner.PhonePos,   "0"),
                (MpPartner.VisaAccept, "3"),
                (MpPartner.VisaAccept, "3")
            };

            // Act
            var phonepos = MpAppUserStatusService.ResolveForPartner(rows, MpPartner.PhonePos);
            var visaaccept = MpAppUserStatusService.ResolveForPartner(rows, MpPartner.VisaAccept);

            // Assert
            phonepos.Should().Be(MpAppUserStatus.DaDangKy);
            visaaccept.Should().Be(MpAppUserStatus.KichHoat);
        }

        [Fact]
        public void ResolveForPartner_WhenPartnerNameHasDifferentCasing_StillMatches()
        {
            // Arrange
            var rows = new (string? Partner, string? Status)[] { (" phonepos ", "2") };

            // Act
            var result = MpAppUserStatusService.ResolveForPartner(rows, MpPartner.PhonePos);

            // Assert
            result.Should().Be(MpAppUserStatus.KichHoat);
        }

        [Fact]
        public void ResolveForPartner_WhenNoRowForThatPartner_ReturnsNull()
        {
            // Arrange
            var rows = new (string? Partner, string? Status)[] { (MpPartner.VisaAccept, "2") };

            // Act
            var result = MpAppUserStatusService.ResolveForPartner(rows, MpPartner.PhonePos);

            // Assert
            result.Should().BeNull();
        }

        /// <summary>
        /// "0,7" -> hai dòng cùng partner, trạng thái "0" và "7". Chuỗi rỗng -> không dòng nào.
        /// </summary>
        private static List<(string? Partner, string? Status)> BuildRows(string partner, string statusesCsv) =>
            string.IsNullOrEmpty(statusesCsv)
                ? []
                : [.. statusesCsv.Split(',').Select(s => ((string?)partner, (string?)s))];
    }
}
