using Microsoft.EntityFrameworkCore;
using VcbPortalApi.DbContext;
using VcbPortalApi.Services;
using VcbPortalApi.Services.Sso;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution thật đã có Startup.cs. ĐỪNG chép đè.
//
// PHẦN DUY NHẤT CẦN MANG SANG SOLUTION THẬT là các dòng đăng ký DI trong
// ConfigureServices bên dưới (đánh dấu "ĐĂNG KÝ CHO LUỒNG SSO"). Chép đúng
// những dòng đó vào Startup.ConfigureServices có sẵn.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi
{
    public class Startup
    {
        public Startup(IConfiguration configuration) => Configuration = configuration;

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            // ── ĐĂNG KÝ CHO LUỒNG SSO — phần cần mang sang solution thật ────────────
            AppSettings.Load(Configuration);

            services.Configure<MpSsoOptions>(Configuration.GetSection(MpSsoOptions.SectionName));
            services.Configure<MpAuthOptions>(Configuration.GetSection(MpAuthOptions.SectionName));

            // HttpClient riêng cho SSO: BaseAddress và timeout lấy từ cấu hình, không
            // dùng chung HttpClient mặc định để timeout của SSO không ảnh hưởng nơi khác.
            services.AddHttpClient<IMpSsoClient, MpSsoClient>((sp, http) =>
            {
                var options = sp.GetRequiredService<
                    Microsoft.Extensions.Options.IOptions<MpSsoOptions>>().Value;

                http.BaseAddress = new Uri(options.BaseUrl);
                http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            });

            services.AddScoped<IMpSsoAuthService, MpSsoAuthService>();
            services.AddScoped<IMpAppUserStatusService, MpAppUserStatusService>();
            // ── HẾT PHẦN CẦN MANG SANG ──────────────────────────────────────────────

            // DbContext thật của solution dùng Oracle; ở bản khung để InMemory cho gọn.
            services.AddDbContext<VcbPortalDbContext>(o => o.UseInMemoryDatabase("skeleton"));
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseRouting();
            app.UseEndpoints(e => e.MapControllers());
        }
    }
}
