using System.Text.Json;
using System.Threading.RateLimiting;
using Yarp.ReverseProxy;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────────────────────────────────────────────────────────
// Môi trường đọc LÚC CHẠY, không phải hằng lúc biên dịch
//
// Khác VcbPortalApi (nơi BuildSettings.Env là const vì code đã viết vậy từ lâu),
// gateway là project mới nên không có lý do gì phải chịu ràng buộc đó. Đọc lúc
// chạy nghĩa là MỘT bản build chạy được cả 4 môi trường: bản đã kiểm ở UAT
// chính là bản đẩy lên Prod, không phải build lại một bản chưa ai kiểm.
//
// Đặt qua biến môi trường ASPNETCORE_ENVIRONMENT trên từng máy chủ.
// ─────────────────────────────────────────────────────────────────────────────
string[] knownEnvs = ["Dev", "Uat", "Pilot", "Prod"];
var envName = builder.Environment.EnvironmentName;

if (!knownEnvs.Contains(envName, StringComparer.Ordinal))
    throw new InvalidOperationException(
        $"ASPNETCORE_ENVIRONMENT = '{envName}' khong hop le. " +
        $"Phai la mot trong: {string.Join(", ", knownEnvs)}.");

// optional:false để thiếu file là ném ngay lúc khởi động, kèm đúng tên file
// thiếu — thay vì chạy tiếp với cấu hình rỗng rồi hỏng ở chỗ khó lần ra.
// AddEnvironmentVariables() gọi lại ở cuối để biến môi trường vẫn thắng JSON.
builder.Configuration
    .AddJsonFile($"appsettings.{envName}.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

var cfg = builder.Configuration;

// Chốt an toàn: file nạp vào phải tự khai đúng môi trường nó phục vụ. Chép nhầm
// appsettings.Dev.json đè lên Prod là app không khởi động được, thay vì âm thầm
// trỏ sang backend sai.
var envInFile = cfg["Env"];
if (envInFile != envName)
    throw new InvalidOperationException(
        $"Sai file cau hinh: dang chay {envName} nhung file khai Env={envInFile}.");

// ─────────────────────────────────────────────────────────────────────────────
    // KHÔNG xác thực ở gateway — có chủ ý
    //
    // VcbPortalApi phát ra token JWE: ký bằng RSA (RsaSsaPssSha256) rồi MÃ HOÁ
    // (RsaOAEP + Aes256CbcHmacSha512). Muốn kiểm ở gateway thì phải giải mã trước,
    // mà giải mã cần khoá RIÊNG TƯ giải mã — thứ nhạy cảm nhất trong hệ thống.
    // Bật xác thực ở đây đồng nghĩa với chép khoá đó sang thêm một máy chủ nữa;
    // lộ một trong hai là lộ cả hệ.
    //
    // Nên để VcbPortalApi tự xác thực như nó vẫn làm — nó có sẵn khoá và
    // TokenValidationParameters, đang chạy đúng. Token rác vẫn chạm tới API, nhưng
    // token đã mã hoá thì không tự chế ra được: kẻ tấn công chỉ gửi được rác ngẫu
    // 0nhiên, mà rác ngẫu nhiên đã bị phần giới hạn tần suất bên dưới chặn.
    //
    // MUỐN BẬT SAU NÀY, cần đúng ba thứ:
    //   IssuerSigningKey   = new RsaSecurityKey(khoá CÔNG KHAI ký)
    //   TokenDecryptionKey = new RsaSecurityKey(khoá RIÊNG TƯ giải mã)
    //   ValidAlgorithms    = [SecurityAlgorithms.RsaSsaPssSha256]
// rồi đặt "AuthorizationPolicy": "authenticated" cho route catch-all trong
// cả 4 file appsettings.
// ─────────────────────────────────────────────────────────────────────────────

// ─────────────────────────────────────────────────────────────────────────────
// Giới hạn tần suất
//
// Phân vùng theo IP client. Gateway là biên nên RemoteIpAddress CHÍNH LÀ IP
// client — khác VcbPortalApi, nơi phải đọc X-Forwarded-For.
//
// CHÚ Ý: bộ đếm nằm trong bộ nhớ tiến trình. Chạy nhiều instance gateway sau
// load balancer thì hạn mức thực tế nhân lên theo số instance, và restart là
// mất sạch bộ đếm. Muốn đúng con số phải đưa bộ đếm sang Redis.
// ─────────────────────────────────────────────────────────────────────────────
var trustForwardedFor = cfg.GetValue("RateLimit:TrustForwardedFor", false);
var whitelist = cfg.GetSection("RateLimit:IpWhitelist").GetChildren()
                   .Select(c => c.Value!).Where(v => !string.IsNullOrWhiteSpace(v))
                   .ToHashSet(StringComparer.OrdinalIgnoreCase);

string ClientIp(HttpContext ctx)
{
    if (trustForwardedFor)
    {
        var fwd = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(fwd))
            return fwd.Split(',')[0].Trim();
    }
    return ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

RateLimitPartition<string> Partition(HttpContext ctx, string prefix, int permit, int windowSeconds)
{
    var ip = ClientIp(ctx);

    // IP trong whitelist không bị giới hạn — giữ đúng ý nghĩa IpWhitelist
    // mà VcbPortalApi đang dùng.
    if (whitelist.Contains(ip))
        return RateLimitPartition.GetNoLimiter($"{prefix}:free:{ip}");

    return RateLimitPartition.GetSlidingWindowLimiter($"{prefix}:{ip}", _ =>
        new SlidingWindowRateLimiterOptions
        {
            PermitLimit = permit,
            Window = TimeSpan.FromSeconds(windowSeconds),

            // Cửa sổ trượt chia 6 đoạn. Fixed window cho phép dồn cục ở ranh
            // giới: 10 lần lúc 29:59 rồi 10 lần nữa lúc 30:01 là 20 lần trong
            // 2 giây. Với endpoint đăng nhập thì đó đúng là kẽ hở cần bịt.
            SegmentsPerWindow = 6,
            QueueLimit = 0,
        });
}

var quotaMessage = cfg["RateLimit:QuotaExceededMessage"] ?? "Truy cap qua thuong xuyen.";

builder.Services.AddRateLimiter(o =>
{
    o.AddPolicy("general", ctx => Partition(ctx, "general",
        cfg.GetValue("RateLimit:General:PermitLimit", 100),
        cfg.GetValue("RateLimit:General:WindowSeconds", 180)));

    o.AddPolicy("login", ctx => Partition(ctx, "login",
        cfg.GetValue("RateLimit:Login:PermitLimit", 10),
        cfg.GetValue("RateLimit:Login:WindowSeconds", 1800)));

    // Trả về đúng hình dạng body mà VcbPortalApi đang trả khi vượt hạn mức,
    // để client không phải phân biệt lỗi đến từ gateway hay từ API.
    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsync(
            JsonSerializer.Serialize(new { code = "429", message = quotaMessage }), ct);
    };
});

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(cfg.GetSection("ReverseProxy"));


var app = builder.Build();

// ─────────────────────────────────────────────────────────────────────────────
// Lỗi chuyển tiếp trả cùng khuôn {code, message} như mọi lỗi khác của hệ thống.
// Mặc định YARP trả 502 với body RỖNG — client nhận được thứ khác hẳn những lỗi
// nó vẫn xử lý, và không có gì để hiện cho người dùng.
// ─────────────────────────────────────────────────────────────────────────────
var upstreamMessage = cfg["Upstream:ErrorMessage"] ?? "He thong dang ban, vui long thu lai sau.";

app.Use(async (ctx, next) =>
{
    await next();

    var failure = ctx.Features.Get<IForwarderErrorFeature>();
    if (failure is null || ctx.Response.HasStarted) return;

    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsync(JsonSerializer.Serialize(new
    {
        code = ctx.Response.StatusCode.ToString(),
        message = upstreamMessage,
    }));
});

app.UseRouting();
app.UseRateLimiter();

// ─────────────────────────────────────────────────────────────────────────────
// /health nói cả về BACKEND, không chỉ về gateway.
//
// Một gateway còn sống nhưng đầu kia đã chết thì load balancer vẫn phải biết để
// ngừng đổ traffic vào nó. Trạng thái lấy từ chính bộ theo dõi sức khoẻ của YARP.
// ─────────────────────────────────────────────────────────────────────────────
app.MapGet("/health", (IProxyStateLookup lookup) =>
{
    var clusters = lookup.GetClusters().Select(c => new
    {
        cluster = c.ClusterId,
        destinations = c.DestinationsState.AllDestinations.Select(d => new
        {
            id = d.DestinationId,
            address = d.Model.Config.Address,
            health = d.Health.Passive.ToString(),
        }).ToArray(),
    }).ToArray();

    var allUp = clusters.All(c => c.destinations.Any(d => d.health != "Unhealthy"));

    return Results.Json(
        new { status = allUp ? "ok" : "degraded", env = envName, clusters },
        statusCode: allUp ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
});

app.MapReverseProxy();

app.Run();
