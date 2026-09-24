using System.Text.Json;
using System.Text.RegularExpressions;
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
//
// PHẢI có CẢ HAI lời gọi. Bản không tiền tố đọc các biến thường. Bản tiền tố
// "ASPNETCORE_" là thứ ánh xạ ASPNETCORE_URLS thành khoá "Urls" — nếu thiếu,
// khoá "Urls" trong appsettings DÙNG CHUNG với VcbPortalApi sẽ thắng, và
// gateway đi chiếm đúng cổng của API. Chạy riêng từng cái thì không thấy;
// chạy song song thì cái khởi động sau chết vì cổng đã bị giữ.
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{envName}.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddEnvironmentVariables(prefix: "ASPNETCORE_");

var cfg = builder.Configuration;

// ─────────────────────────────────────────────────────────────────────────────
// Bỏ qua phần Kestrel trong file dùng chung
//
// appsettings là của VcbPortalApi, nên khoá "Kestrel" trong đó khai cổng của
// API. Gateway đọc chung file, mặc định sẽ nghe ĐÚNG cổng đó và giành cổng với
// API — chạy riêng từng cái thì không thấy, chạy song song thì cái sau chết.
//
// Kestrel:Endpoints ưu tiên cao hơn cả ASPNETCORE_URLS, nên không thể ghi đè
// bằng biến môi trường. Cách duy nhất là trỏ Kestrel vào một section KHÔNG tồn
// tại: nó không thấy endpoint nào và quay về dùng Urls/ASPNETCORE_URLS.
// ─────────────────────────────────────────────────────────────────────────────
builder.WebHost.ConfigureKestrel(o => o.Configure(cfg.GetSection("GatewayKestrel")));

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
// Giới hạn tần suất — đọc thẳng section IpRateLimiting CÓ SẴN của VcbPortalApi
//
// Gateway KHÔNG tự khai một bộ luật riêng. Hai bộ luật song song là hai thứ
// phải nhớ sửa cùng lúc, và lần quên đầu tiên sẽ là lần gateway chặn khác API.
// Đọc chung một section thì sửa một chỗ là cả hai cùng đổi.
//
// CHÚ Ý: bộ đếm nằm trong bộ nhớ tiến trình. Chạy nhiều instance gateway sau
// load balancer thì hạn mức thực tế nhân lên theo số instance, và restart là
// mất sạch bộ đếm. Muốn đúng con số phải đưa bộ đếm sang Redis.
// ─────────────────────────────────────────────────────────────────────────────
var rl = cfg.GetSection("IpRateLimiting");

// AspNetCoreRateLimit đọc IP thật từ header này vì nginx đứng trước. Để trống
// thì dùng IP kết nối trực tiếp.
var realIpHeader = rl["RealIPHeader"];

var whitelist = rl.GetSection("IpWhitelist").GetChildren()
                  .Select(c => c.Value!).Where(v => !string.IsNullOrWhiteSpace(v))
                  .ToHashSet(StringComparer.OrdinalIgnoreCase);

string ClientIp(HttpContext ctx)
{
    if (!string.IsNullOrWhiteSpace(realIpHeader))
    {
        var fwd = ctx.Request.Headers[realIpHeader].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(fwd))
            return fwd.Split(',')[0].Trim();
    }
    return ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

// "1m" / "30m" / "3m" / "1h" / "1d" — đúng định dạng Period của AspNetCoreRateLimit.
static TimeSpan ParsePeriod(string? period)
{
    if (string.IsNullOrWhiteSpace(period) || period.Length < 2)
        throw new InvalidOperationException($"Period khong hop le: '{period}'.");

    var value = int.Parse(period[..^1]);
    return period[^1] switch
    {
        's' => TimeSpan.FromSeconds(value),
        'm' => TimeSpan.FromMinutes(value),
        'h' => TimeSpan.FromHours(value),
        'd' => TimeSpan.FromDays(value),
        _ => throw new InvalidOperationException($"Don vi Period khong hieu: '{period}'."),
    };
}

// Endpoint dạng "POST:*/user/pwd" hoặc "*". Chuyển sang regex khớp với
// "{VERB}:{path}" của request. Dấu * là ký tự đại diện, phần còn lại khớp y hệt.
static Regex ToPattern(string endpoint)
{
    var pattern = "^" + string.Join(".*", endpoint.Split('*').Select(Regex.Escape)) + "$";
    return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
}

var rules = rl.GetSection("GeneralRules").GetChildren()
    .Select(r => new
    {
        Endpoint = r["Endpoint"] ?? "*",
        Pattern = ToPattern(r["Endpoint"] ?? "*"),
        Period = ParsePeriod(r["Period"]),
        Limit = int.Parse(r["Limit"] ?? "0"),
    })
    .ToArray();

if (rules.Length == 0)
    throw new InvalidOperationException(
        "IpRateLimiting:GeneralRules rong — kiem tra lai appsettings.json.");

// Body khi vượt hạn mức lấy nguyên từ QuotaExceededResponse của
// AspNetCoreRateLimit, để client nhận đúng thứ VcbPortalApi vẫn trả.
var quota = rl.GetSection("QuotaExceededResponse");
var quotaContentType = quota["ContentType"] ?? "application/json";
var quotaStatusCode = quota.GetValue("StatusCode",
                          rl.GetValue("HttpStatusCode", StatusCodes.Status429TooManyRequests));

// AspNetCoreRateLimit chạy Content qua string.Format nên "{{" trong file là dấu
// { thật, và nó điền {0}=Period, {1}=Limit, {2}=RetryAfter.
//
// Gateway KHÔNG điền được ba chỗ đó: bộ giới hạn nối chuỗi chỉ báo "đã bị chặn",
// không cho biết LUẬT NÀO chặn, mà Period/Limit thì thuộc về luật. Content đang
// dùng không có placeholder nào nên không ảnh hưởng — nếu sau này thêm vào thì
// chúng sẽ ra rỗng.
var quotaTemplate = quota["Content"]
    ?? JsonSerializer.Serialize(new
       {
           code = quotaStatusCode.ToString(),
           message = rl["QuotaExceededMessage"] ?? "Truy cap qua thuong xuyen.",
       });

string quotaBody;
try
{
    quotaBody = string.Format(quotaTemplate, "", "", "");
}
catch (FormatException)
{
    quotaBody = quotaTemplate.Replace("{{", "{").Replace("}}", "}");
}

builder.Services.AddRateLimiter(o =>
{
    // MỖI LUẬT một bộ đếm riêng, nối chuỗi lại: request phải qua được hết mới
    // đi tiếp. Giống AspNetCoreRateLimit — luật "*" 100/3m và luật
    // "POST:*/user/pwd" 10/30m cùng áp lên một request đổi mật khẩu, luật nào
    // chạm trần trước thì luật đó chặn.
    o.GlobalLimiter = PartitionedRateLimiter.CreateChained(
        rules.Select(rule =>
            PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            {
                // /health là của hạ tầng, không phải của người dùng. Luật "*"
                // 100/3m sẽ nuốt nó: load balancer poll mỗi giây là chạm trần
                // sau 100 giây, rồi gateway tự báo mình chết dù vẫn khoẻ.
                if (ctx.Request.Path.StartsWithSegments("/health"))
                    return RateLimitPartition.GetNoLimiter("health");

                var target = $"{ctx.Request.Method}:{ctx.Request.Path}";

                // Luật không áp cho endpoint này thì không đếm. Luật "*" thành
                // regex ^.*$ nên khớp mọi request, đúng ý nghĩa "tất cả".
                if (!rule.Pattern.IsMatch(target))
                    return RateLimitPartition.GetNoLimiter("bo-qua");

                var ip = ClientIp(ctx);

                // IP nội bộ trong IpWhitelist không bị giới hạn — giữ đúng ý
                // nghĩa mà VcbPortalApi đang dùng.
                if (whitelist.Contains(ip))
                    return RateLimitPartition.GetNoLimiter($"free:{ip}");

                return RateLimitPartition.GetSlidingWindowLimiter(
                    $"{rule.Endpoint}|{ip}",
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = rule.Limit,
                        Window = rule.Period,

                        // Cửa sổ trượt chia 6 đoạn. Fixed window cho phép dồn cục
                        // ở ranh giới: 10 lần lúc 29:59 rồi 10 lần nữa lúc 30:01
                        // là 20 lần trong 2 giây. Với endpoint đăng nhập hay đổi
                        // mật khẩu thì đó đúng là kẽ hở cần bịt.
                        SegmentsPerWindow = 6,
                        QueueLimit = 0,
                    });
            })
        ).ToArray());

    // Trả về đúng hình dạng body mà VcbPortalApi đang trả khi vượt hạn mức,
    // để client không phải phân biệt lỗi đến từ gateway hay từ API.
    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = quotaStatusCode;
        ctx.HttpContext.Response.ContentType = quotaContentType;
        await ctx.HttpContext.Response.WriteAsync(quotaBody, ct);
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
