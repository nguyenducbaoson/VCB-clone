using Microsoft.EntityFrameworkCore;
using VcbPortalApi.DbContext;
using VcbPortalApi.Models.SSO;

namespace Tests.TestSupport
{
    /// <summary>
    /// Tạo DbContext chạy trên bộ nhớ (EF Core InMemory) cho test.
    ///
    /// Mỗi lần gọi <see cref="Create"/> sinh một database riêng theo Guid, nên các test
    /// chạy song song không giẫm lên dữ liệu của nhau. Không cần Oracle, không cần dọn dẹp.
    ///
    /// Giới hạn cần biết: InMemory KHÔNG phải Oracle. Nó không kiểm tra ràng buộc, không
    /// biết ORA-12899, và dịch LINQ theo luật của C# chứ không phải SQL. Dùng nó để test
    /// LOGIC nghiệp vụ. Những gì phụ thuộc hành vi thật của Oracle thì kiểm tra bằng
    /// script trong Sql/ chạy trên UAT.
    /// </summary>
    public static class TestDb
    {
        public static VcbPortalDbContext Create()
        {
            var options = new DbContextOptionsBuilder<VcbPortalDbContext>()
                .UseInMemoryDatabase($"test-{Guid.NewGuid()}")
                .Options;

            return new VcbPortalDbContext(options);
        }

        /// <summary>
        /// Thêm một user vào MP_APP_USERS. Tham số đặt tên rõ để chỗ gọi đọc được luôn:
        /// SeedUser(db, "VATID001", phonepos: 0, visaaccept: null).
        /// </summary>
        public static MpAppUser SeedUser(
            VcbPortalDbContext db,
            string username,
            decimal? phonepos = null,
            decimal? visaaccept = null,
            decimal? finone = null,
            string? deviceId = null,
            decimal? bid = null,
            decimal? mid = null,
            decimal? tid = null)
        {
            var user = new MpAppUser
            {
                Username = username,
                PhoneposStatus = phonepos,
                VisaacceptStatus = visaaccept,
                FinoneStatus = finone,
                Deviceid = deviceId,
                Bid = bid,
                Mid = mid,
                Tid = tid
            };

            db.Add(user);
            db.SaveChanges();
            return user;
        }

        /// <summary>
        /// Thêm các bản ghi đăng ký partner. Truyền dạng ("PHONEPOS", "2") cho gọn:
        /// SeedRegistrations(db, "VATID001", ("VISAACCEPT", "3"), ("VISAACCEPT", "0"));
        ///
        /// STATUS truyền dạng CHUỖI vì cột thật là VARCHAR2(2) — muốn test được cả
        /// giá trị rác như "X" hay "07" thì không được ép thành số ở đây.
        /// </summary>
        public static void SeedRegistrations(
            VcbPortalDbContext db, string username, params (string Partner, string? Status)[] rows)
        {
            var nextRowId = db.MpAppPartnerCardRegs.Count();

            foreach (var (partner, status) in rows)
            {
                db.Add(new MpAppPartnerCardReg
                {
                    RowId = ++nextRowId,
                    Username = username,
                    Partner = partner,
                    Status = status,
                    CreateTime = new DateTime(2026, 1, 1)
                });
            }

            db.SaveChanges();
        }
    }
}
