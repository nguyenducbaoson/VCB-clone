using Microsoft.EntityFrameworkCore;
using VcbPortalApi.DbContext;
using VcbPortalApi.DbContext.Oracle;
using VcbPortalApi.Services;
using VcbPortalApi.Services.AcqHub;
using VcbPortalApi.Services.MP;
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

            // Chuỗi kết nối Oracle nằm trong appsettings.json.
            // OnConfiguring của MỌI DbContext chạy ngoài DI nên phải mượn lại cấu
            // hình ở đây, TRƯỚC khi có DbContext nào được dựng.

            // ── ĐĂNG KÝ CHO LUỒNG SSO — phần cần mang sang solution thật ────────────

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

            // ── ĐĂNG KÝ CHO LUỒNG CÀI ĐẶT SHLX QUA ACQHUB ───────────────────────────

            services.Configure<AcqHubShlxOptions>(
                Configuration.GetSection(AcqHubShlxOptions.SectionName));

            // HttpClient riêng, timeout dài hơn SSO: một lô có thể tới 100 terminal và
            // ACQHUB xử lý tuần tự từng cái.
            //
            // KHÔNG đặt BaseAddress: ShlxConfigUrl trong cấu hình là URL tuyệt đối, để
            // sau này thêm endpoint ACQHUB khác thì mỗi cái tự khai URL đầy đủ.
            services.AddHttpClient<IAcqHubShlxClient, AcqHubShlxClient>((sp, http) =>
            {
                var options = sp.GetRequiredService<
                    Microsoft.Extensions.Options.IOptions<AcqHubShlxOptions>>().Value;

                http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            });
            // Tạo user portal cho terminal SHLX vừa cài. Cùng vòng đời với request để
            // dùng chung FrontendContext.
            services.AddScoped<IUserService, UserService>();
            // ── HẾT PHẦN SHLX ───────────────────────────────────────────────────────

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
