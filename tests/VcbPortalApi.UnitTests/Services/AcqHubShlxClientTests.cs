using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VcbPortalApi.Services.AcqHub;
using Xunit;

namespace VcbPortalApi.UnitTests.Services
{
    /// <summary>
    /// Kiểm phong bì mà AcqRequest dựng ra.
    ///
    /// Điều đáng kiểm nhất không phải "có gọi được không" mà là "checkSum có khớp
    /// với data và requestTime vừa sinh ra không" — ba thứ đó phải luôn được tạo
    /// cùng lúc. Bản tin thất bại quan sát được ở UAT chính là kiểu lỗi này:
    /// requestTime gõ cứng trong Postman đã cũ một tiếng so với checkSum.
    ///
    /// Khoá và clientId đọc từ AppSettings.Cfg → appsettings.{Env}.json, nên test
    /// dùng đúng giá trị placeholder trong file đó.
    /// </summary>
    public class AcqHubShlxClientTests
    {
        private static readonly string Secret = VcbPortalApi.AppSettings.AcqSecretKey;
        private static readonly string ClientId = VcbPortalApi.AppSettings.AcqClientId;

        private const string ApiName = "shlxconfig";
        private const string Path = "/api/acqhub/configpartner/v1/shlxconfig";

        private static List<ShlxConfigItem> Sample(string terminalId = "TERMINAL_TEST") =>
        [
            new()
            {
                merchantId = "MERCHANT_TEST",
                terminalId = terminalId,
                terminalName = "TEN_TERMINAL",
                ddAccountNumber = "0000000000",
                branchCode = "01400",
                province = "TINH_TEST",
            },
        ];

        /// <summary>Phong bì đọc lại từ bản tin đã gửi. AcqRequest không dựng ngược được.</summary>
        private sealed record Envelope(string data, string clientId, long requestTime, string checkSum);

        private static Envelope DocPhongBi(HttpRequestMessage req)
        {
            var root = JsonDocument.Parse(req.Content!.ReadAsStringAsync().Result).RootElement;
            return new Envelope(
                root.GetProperty("data").GetString()!,
                root.GetProperty("clientId").GetString()!,
                root.GetProperty("requestTime").GetInt64(),
                root.GetProperty("checkSum").GetString()!);
        }

        private static string TinhCheckSum(Envelope e, string secret) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                $"{e.data}|{e.clientId}|{e.requestTime}|{ApiName}|{secret}")));

        private static AcqHubShlxClient Create(
            HttpMessageHandler? handler = null, string baseUrl = "http://acqhub.test:8829")
        {
            return new AcqHubShlxClient(
                new HttpClient(handler ?? AcqHubGiaLap()),
                Options.Create(new AcqHubShlxOptions { BaseUrl = baseUrl, ShlxConfigPath = Path }),
                NullLogger<AcqHubShlxClient>.Instance);
        }

        /// <summary>
        /// ACQHUB giả: tính lại checkSum bằng khoá thật, chỉ trả thành công khi khớp.
        /// Đây là bài kiểm sát thực tế nhất — nó kiểm cả công thức lẫn thứ tự ghép.
        /// </summary>
        private static HttpMessageHandler AcqHubGiaLap(string? secret = null) => new StubHandler(req =>
        {
            var e = DocPhongBi(req);

            if (TinhCheckSum(e, secret ?? Secret) != e.checkSum)
                return Json("""{"code":"01","message":"Failed","results":[]}""");

            var items = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(e.data)))
                .RootElement.GetProperty("items").EnumerateArray()
                .Select(x => x.GetProperty("terminalId").GetString()!)
                .ToArray();

            var results = string.Join(",", items.Select(t =>
                $$"""{"terminal_id":"{{t}}","success":{{(t.EndsWith("_HONG") ? "false" : "true")}}}"""));

            return Json($$"""{"code":"00","results":[{{results}}]}""");
        });

        private static HttpResponseMessage Json(string body) =>
            new(HttpStatusCode.OK) { Content = new StringContent(body) };

        [Fact]
        public async Task Data_la_base64_cua_JSON_items_ten_khoa_viet_thuong()
        {
            Envelope? bat = null;
            await Create(new StubHandler(req => { bat = DocPhongBi(req); return Json("""{"code":"00","results":[]}"""); }))
                .ConfigAsync(Sample());

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(bat!.data));

            json.Should().Contain("\"terminalId\":\"TERMINAL_TEST\"",
                "ToJsonString giu nguyen ten thuoc tinh — PascalCase la sai khoa");
            json.Should().Contain("\"branchCode\":\"01400\"", "so 0 dau phai giu nguyen");
        }

        [Fact]
        public async Task CheckSum_khop_voi_data_va_requestTime_vua_sinh()
        {
            Envelope? bat = null;
            await Create(new StubHandler(req => { bat = DocPhongBi(req); return Json("""{"code":"00","results":[]}"""); }))
                .ConfigAsync(Sample());

            bat!.checkSum.Should().Be(TinhCheckSum(bat, Secret));
            bat.clientId.Should().Be(ClientId);
        }

        [Fact]
        public async Task RequestTime_la_epoch_MILI_GIAY_va_moi_tinh()
        {
            Envelope? bat = null;
            await Create(new StubHandler(req => { bat = DocPhongBi(req); return Json("""{"code":"00","results":[]}"""); }))
                .ConfigAsync(Sample());

            bat!.requestTime.ToString().Should().HaveLength(13, "AcqRequest dung ToUnixTimeMilliseconds");
            bat.requestTime.Should().BeCloseTo(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), 5000);
        }

        [Theory]
        [InlineData("/api/acqhub/configpartner/v1/shlxconfig", "shlxconfig")]
        [InlineData("/api/acqhub/configpartner/v1/ShlxConfig", "shlxconfig")]
        public void ApiName_la_doan_cuoi_URL_viet_thuong(string url, string mongDoi)
        {
            // apiname nam TRONG chuoi ky, nen doi URL la doi checkSum.
            AcqHubShlxClient.ApiNameFromUrl(url).Should().Be(mongDoi);
        }

        [Theory]
        [InlineData("http://acqhub.test:8829", "/api/v1/shlxconfig")]
        [InlineData("http://acqhub.test:8829/", "/api/v1/shlxconfig")]
        [InlineData("http://acqhub.test:8829", "api/v1/shlxconfig")]
        [InlineData("http://acqhub.test:8829/", "api/v1/shlxconfig")]
        public void Ghep_BaseUrl_va_Path_khong_sinh_dau_gach_doi(string baseUrl, string path)
        {
            // BaseUrl nam o file moi truong, Path nam o file chung — nguoi sua hai
            // file khac nhau, nen dau / o cho noi la chuyen som muon cung xay ra.
            new AcqHubShlxOptions { BaseUrl = baseUrl, ShlxConfigPath = path }
                .ShlxConfigUrl.Should().Be("http://acqhub.test:8829/api/v1/shlxconfig");
        }

        [Fact]
        public async Task Khoa_dung_thi_ACQHUB_chap_nhan()
        {
            var res = await Create().ConfigAsync(Sample());

            res.code.Should().Be("00");
            res.results.Should().ContainSingle().Which.success.Should().BeTrue();
        }

        [Fact]
        public async Task Khoa_sai_thi_ACQHUB_tu_choi()
        {
            // Bai kiem nguoc: neu bai tren van xanh khi checkSum sai thi no khong kiem gi.
            var res = await Create(AcqHubGiaLap(secret: "KHOA_KHAC")).ConfigAsync(Sample());

            res.code.Should().Be("01");
            res.results.Should().BeEmpty();
        }

        [Fact]
        public async Task Thieu_BaseUrl_thi_nem_ngay_khong_goi_di()
        {
            var act = () => Create(baseUrl: "").ConfigAsync(Sample());

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*BaseUrl*");
        }

        private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
            : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(respond(request));
        }
    }
}
