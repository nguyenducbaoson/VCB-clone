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

namespace VcbPortalApi.UnitTests.Fixtures
{
    /// <summary>
    /// Lớp test nào đụng trạng thái static (
    /// AppSettings.JdWhiteList, Branches.Names) phải mang
    /// [Collection(StaticStateCollection.Name)] để xUnit chạy tuần tự.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class StaticStateCollection
    {
        public const string Name = "static-state";
    }
}
