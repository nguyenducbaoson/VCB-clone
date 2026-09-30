using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VcbPortalApi.Controllers.Frontend;
using VcbPortalApi.Services.AcqHub;
using VcbPortalApi.UnitTests.Fixtures;
using Xunit;

namespace VcbPortalApi.UnitTests.Controllers.Frontend
{
    /// <summary>
    /// Đi hết đường: controller → client → AcqRequest dựng phong bì → ACQHUB giả.
    ///
    /// Dùng AcqHubShlxClient THẬT chứ không mock, chỉ thay tầng HTTP. Mock client đi
    /// thì bài test chỉ chứng minh controller gọi đúng hàm — không chứng minh được
    /// phong bì gửi sang ACQHUB có đúng không, mà đó mới là chỗ hay sai.
    /// </summary>
    public class AcqHubShlxControllerTests
    {
        private static readonly string Secret = VcbPortalApi.AppSettings.AcqSecretKey;

        private static ShlxConfigRequest MotTerminal(string terminalId = "V827308102") => new()
        {
            items =
            [
                new ShlxConfigItem
                {
                    merchantId = "MERCHANT_TEST",
                    terminalId = terminalId,
                    terminalName = "TEN_TERMINAL",
                    ddAccountNumber = "0000000000",
                    branchCode = "01400",
                    province = "TINH_TEST",
                },
            ],
        };

        /// <summary>
        /// ACQHUB giả: kiểm checkSum theo đúng công thức của AcqRequest, rồi trả kết
        /// quả cho từng terminal đọc được trong data. Terminal có đuôi "_HONG" thì báo
        /// thất bại — để kiểm được trường hợp một lô thành công MỘT PHẦN.
        /// </summary>
        private static HttpMessageHandler AcqHubGiaLap() => new StubHandler(req =>
        {
            var root = JsonDocument.Parse(req.Content!.ReadAsStringAsync().Result).RootElement;

            var data = root.GetProperty("data").GetString()!;
            var clientId = root.GetProperty("clientId").GetString()!;
            var requestTime = root.GetProperty("requestTime").GetInt64();
            var checkSum = root.GetProperty("checkSum").GetString()!;

            var raw = string.Join("|", data, clientId, requestTime, "shlxconfig", Secret);
            var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

            if (expected != checkSum)
                return Json("""{"code":"01","message":"Failed","results":[]}""");

            var terminals = JsonDocument
                .Parse(Encoding.UTF8.GetString(Convert.FromBase64String(data)))
                .RootElement.GetProperty("items").EnumerateArray()
                .Select(x => x.GetProperty("terminalId").GetString()!)
                .ToArray();

            var results = string.Join(",", terminals.Select(t =>
                $$"""{"terminal_id":"{{t}}","success":{{(t.EndsWith("_HONG") ? "false" : "true")}}}"""));

            return Json($$"""{"code":"00","message":"Succeed","results":[{{results}}]}""");
        });

        private static HttpResponseMessage Json(string body) =>
            new(HttpStatusCode.OK) { Content = new StringContent(body) };

        private static AcqHubShlxController CreateController(HttpMessageHandler? handler = null)
        {
            var client = new AcqHubShlxClient(
                new HttpClient(handler ?? AcqHubGiaLap()),
                Options.Create(new AcqHubShlxOptions
                {
                    BaseUrl = "http://acqhub.test:8829",
                    ShlxConfigPath = "/api/acqhub/configpartner/v1/shlxconfig",
                }),
                NullLogger<AcqHubShlxClient>.Instance);

            return new AcqHubShlxController(client, NullLogger<AcqHubShlxController>.Instance)
            {
                ControllerContext = TestHttpContext.Build(),
            };
        }

        [Fact]
        public async Task Danh_sach_rong_thi_BadRequest_khong_goi_ACQHUB()
        {
            var res = await CreateController()
                .ShlxConfig(new ShlxConfigRequest(), CancellationToken.None);

            res.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task Body_null_thi_BadRequest()
        {
            var res = await CreateController().ShlxConfig(null!, CancellationToken.None);

            res.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task Mot_terminal_thanh_cong_thi_tra_nguyen_response_ACQHUB()
        {
            var res = await CreateController()
                .ShlxConfig(MotTerminal(), CancellationToken.None);

            var body = res.Should().BeOfType<OkObjectResult>()
                .Which.Value.Should().BeOfType<ShlxConfigResponse>().Subject;

            body.code.Should().Be("00");
            body.results.Should().ContainSingle();
            body.results[0].terminal_id.Should().Be("V827308102");
            body.results[0].success.Should().BeTrue();
        }

        [Fact]
        public async Task Lo_thanh_cong_MOT_PHAN_thi_giu_nguyen_ket_qua_tung_terminal()
        {
            // Đây là lý do controller trả nguyên response chứ không rút gọn thành
            // một cờ thành/bại: frontend cần biết terminal nào hỏng để giữ lại dòng đó.
            var request = new ShlxConfigRequest
            {
                items =
                [
                    .. MotTerminal("TERM_OK_1").items,
                    .. MotTerminal("TERM_HONG").items,
                    .. MotTerminal("TERM_OK_2").items,
                ],
            };

            var res = await CreateController().ShlxConfig(request, CancellationToken.None);
            var body = (ShlxConfigResponse)((OkObjectResult)res).Value!;

            body.code.Should().Be("00", "phong bi hop le — code ngoai van la 00");
            body.results.Should().HaveCount(3);
            body.results.Count(x => x.success).Should().Be(2);
            body.results.Single(x => !x.success).terminal_id.Should().Be("TERM_HONG");
        }

        [Fact]
        public async Task CheckSum_sai_thi_ACQHUB_tu_choi_ca_lo()
        {
            var handler = new StubHandler(_ =>
                Json("""{"code":"01","message":"Failed","results":[]}"""));

            var res = await CreateController(handler)
                .ShlxConfig(MotTerminal(), CancellationToken.None);

            var body = (ShlxConfigResponse)((OkObjectResult)res).Value!;

            body.code.Should().Be("01");
            body.results.Should().BeEmpty("bi chan truoc khi xu ly terminal nao");
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
