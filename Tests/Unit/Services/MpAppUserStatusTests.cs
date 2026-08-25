using VcbPortalApi.Services;

namespace Tests.Unit.Services
{
    /// <summary>
    /// Unit test cho quy tắc suy ra trạng thái partner của user.
    ///
    /// MẪU CHUẨN cho unit test: hàm được test là <c>static</c>, vào ra thuần tuý,
    /// không DB, không mạng, không đồng hồ. Chạy được cả khi tắt wifi.
    ///
    /// Bảng đặc tả nghiệp vụ:
    ///   Tồn tại ít nhất 1 bản ghi trạng thái 2/3/4/5/6  -> Kích hoạt    (2)
    ///   Tất cả bản ghi trạng thái 7                     -> Hủy          (7)
    ///   Tất cả bản ghi trạng thái 0, hoặc 0 và 7        -> Đã đăng ký   (0)
    ///   Các trường hợp còn lại                          -> Chưa đăng ký (null)
    ///
    /// THỨ TỰ CÁC NHÁNH LÀ BẮT BUỘC — user có cả bản ghi 2 lẫn 7 phải ra "Kích hoạt".
    /// </summary>
    public class MpAppUserStatusTests
    {
        [Theory]
        // Không có bản ghi nào
        [InlineData("",      null)]
        // Đã đăng ký
        [InlineData("0",     0)]
        [InlineData("0,0",   0)]
        [InlineData("0,7",   0)]
        [InlineData("7,7,0", 0)]
        // Kích hoạt — cả dải 2..6
        [InlineData("2",     2)]
        [InlineData("3",     2)]
        [InlineData("4",     2)]
        [InlineData("5",     2)]
        [InlineData("6",     2)]
        // Hủy
        [InlineData("7",     7)]
        [InlineData("7,7",   7)]
        // Thứ tự nhánh: có cả 2 lẫn 7 phải ra Kích hoạt, không phải Hủy
        [InlineData("2,7",   2)]
        [InlineData("7,0,3", 2)]
        // Giá trị không rơi vào nhánh nào
        [InlineData("1",     null)]
        [InlineData("8",     null)]
        [InlineData("0,1",   null)]
        [InlineData("7,1",   null)]
        // STATUS là VARCHAR2(2) nên có thể chứa rác — không được ném exception
        [InlineData("X",     null)]
        [InlineData("0,X",   null)]
        [InlineData("2,X",   2)]
        // Khoảng trắng thừa vẫn phải parse được
        [InlineData(" 2 ",   2)]
        // Ca thật của VATID001 trên UAT: 3 dòng VISAACCEPT
        [InlineData("3,3,0", 2)]
        public void ResolveForPartner_MatchesBusinessRules(string statusesCsv, int? expected)
        {
            var rows = BuildRows(MpPartner.PhonePos, statusesCsv);

            var actual = MpAppUserStatusService.ResolveForPartner(rows, MpPartner.PhonePos);

            // Ép kiểu vì tham số khai báo int?: attribute trong C# không nhận hằng
            // decimal, nên InlineData chỉ truyền được int, còn hàm trả về decimal?.
            Assert.Equal((decimal?)expected, actual);
        }

        [Fact]
        public void ResolveForPartner_IgnoresRowsOfOtherPartners()
        {
            var rows = new (string? Partner, string? Status)[]
            {
                (MpPartner.PhonePos,   "0"),
                (MpPartner.VisaAccept, "3"),   // không được ảnh hưởng tới PHONEPOS
                (MpPartner.VisaAccept, "3")
            };

            Assert.Equal(0, MpAppUserStatusService.ResolveForPartner(rows, MpPartner.PhonePos));
            Assert.Equal(2, MpAppUserStatusService.ResolveForPartner(rows, MpPartner.VisaAccept));
        }

        [Fact]
        public void ResolveForPartner_PartnerNameIsCaseAndWhitespaceInsensitive()
        {
            var rows = new (string? Partner, string? Status)[] { (" phonepos ", "2") };

            Assert.Equal(2, MpAppUserStatusService.ResolveForPartner(rows, MpPartner.PhonePos));
        }

        [Fact]
        public void ResolveForPartner_NoRowForThatPartner_ReturnsNull()
        {
            var rows = new (string? Partner, string? Status)[] { (MpPartner.VisaAccept, "2") };

            Assert.Null(MpAppUserStatusService.ResolveForPartner(rows, MpPartner.PhonePos));
        }

        /// <summary>
        /// "0,7" -> hai dòng PHONEPOS trạng thái "0" và "7". Chuỗi rỗng -> không dòng nào.
        /// Viết dữ liệu test dạng CSV cho InlineData đọc gọn, đổi lại cần hàm dựng này.
        /// </summary>
        private static List<(string? Partner, string? Status)> BuildRows(string partner, string statusesCsv) =>
            string.IsNullOrEmpty(statusesCsv)
                ? []
                : [.. statusesCsv.Split(',').Select(s => ((string?)partner, (string?)s))];
    }
}
