// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution thật đã có Program.cs + Startup.cs. ĐỪNG chép đè.
// Chỉ đủ để project build và chạy được, phục vụ việc dựng bộ test.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi
{
    public static class Program
    {
        public static void Main(string[] args) => CreateHostBuilder(args).Build().Run();

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(web => web.UseStartup<Startup>());
    }
}
