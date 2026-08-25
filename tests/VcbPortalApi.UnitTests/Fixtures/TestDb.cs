using Microsoft.EntityFrameworkCore;

namespace VcbPortalApi.UnitTests.Fixtures
{
    public static class TestDb
    {
        public static T Create<T>() where T : Microsoft.EntityFrameworkCore.DbContext
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
        /// mà khẳng định sau khi gọi hàm cần test.
        /// </summary>
        public static T Seed<T>(this Microsoft.EntityFrameworkCore.DbContext db, T entity) where T : class
        {
            db.Add(entity);
            db.SaveChanges();
            return entity;
        }

        public static void SeedRange<T>(this Microsoft.EntityFrameworkCore.DbContext db, params T[] entities)
            where T : class
        {
            db.AddRange(entities);
            db.SaveChanges();
        }
    }
}
