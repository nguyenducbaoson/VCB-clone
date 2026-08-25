using Microsoft.EntityFrameworkCore;

namespace Tests.TestSupport
{
    /// <summary>
    /// Dựng DbContext chạy trên bộ nhớ. Dùng được cho MỌI DbContext trong solution,
    /// không phải viết helper riêng cho từng cái:
    ///
    ///     using var fe = TestDb.Create&lt;FrontendContext&gt;();
    ///     using var mc = TestDb.Create&lt;MerchantContext&gt;();
    ///
    /// Mỗi lần gọi sinh một database riêng theo Guid, nên test chạy song song không
    /// giẫm dữ liệu của nhau và không cần dọn dẹp.
    ///
    /// Điều kiện duy nhất: DbContext phải có constructor nhận DbContextOptions&lt;T&gt;.
    /// Đây là khuôn scaffold mặc định của EF nên gần như luôn đúng.
    ///
    /// GIỚI HẠN: InMemory KHÔNG phải Oracle. Nó không kiểm tra ràng buộc, không biết
    /// ORA-12899, và dịch LINQ theo luật C# chứ không phải SQL. Dùng để test LOGIC.
    /// </summary>
    public static class TestDb
    {
        public static T Create<T>() where T : DbContext
        {
            var options = new DbContextOptionsBuilder<T>()
                .UseInMemoryDatabase($"test-{Guid.NewGuid()}")
                .Options;

            return (T)Activator.CreateInstance(typeof(T), options)!;
        }
    }

    public static class TestDbExtensions
    {
        /// <summary>
        /// Thêm dữ liệu mẫu rồi lưu ngay. Trả về chính entity đó để test giữ tham chiếu
        /// mà khẳng định sau khi gọi hàm cần test:
        ///
        ///     var user = db.Seed(new MpAppUser { Username = "VATID001" });
        ///     await service.RefreshStatusAsync("VATID001");
        ///     Assert.Equal(2, user.PhoneposStatus);
        /// </summary>
        public static T Seed<T>(this DbContext db, T entity) where T : class
        {
            db.Add(entity);
            db.SaveChanges();
            return entity;
        }

        /// <summary>Thêm nhiều dòng cùng lúc.</summary>
        public static void SeedRange<T>(this DbContext db, params T[] entities) where T : class
        {
            db.AddRange(entities);
            db.SaveChanges();
        }
    }
}
