using Tests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using VcbPortalApi.Controllers;
using VcbPortalApi.Models.SSO;

namespace Tests.Controllers
{
    /// <summary>
    /// MẪU 3 — TEST MỘT ENDPOINT API.
    ///
    /// Gọi thẳng action của controller như gọi một hàm bình thường, service thật thay
    /// bằng fake. Không dựng web server, không mở cổng, chạy nhanh như unit test.
    ///
    /// Test ở tầng này KHÔNG kiểm tra logic nghiệp vụ (đã có mẫu 1 và 2 lo) mà kiểm tra
    /// phần việc riêng của controller: validate đầu vào, chọn mã HTTP, không rò rỉ chi
    /// tiết lỗi ra client, và điền IP thiết bị khi client không gửi.
    ///
    /// Muốn test cả pipeline thật (routing, model binding, middleware) thì cần
    /// WebApplicationFactory — nặng hơn, để dành cho luồng quan trọng.
    /// </summary>
    public class MerchantSsoControllerTests
    {
        /// <summary>
        /// Controller đọc HttpContext.Connection.RemoteIpAddress nên phải gắn HttpContext
        /// giả, nếu không sẽ NullReferenceException.
        /// </summary>
        private static MerchantSsoController CreateController(
            FakeMpSsoAuthService authService, string? remoteIp = "10.0.0.5")
        {
            var httpContext = new DefaultHttpContext();

            if (remoteIp is not null)
                httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(remoteIp);

            return new MerchantSsoController(authService, NullLogger<MerchantSsoController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };
        }

        private static MerchantSsoLoginRequest ValidRequest(string? clientIp = "192.168.1.10") => new()
        {
            AccessTokenSSO = "token-mau",
            Client = new ClientContext
            {
                SessionId = "session-1",
                ClientIp = clientIp,
                DeviceId = "device-1",
                OsType = "ANDROID"
            }
        };

        [Fact]
        public async Task Login_ThanhCong_TraVe200VaMa00()
        {
            var authService = new FakeMpSsoAuthService();
            var controller = CreateController(authService);

            var actionResult = await controller.Login(ValidRequest(), CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(actionResult);
            var body = Assert.IsType<ApiResponse<MerchantSsoLoginResult>>(ok.Value);
            Assert.Equal(MpSsoResultCode.Success, body.Code);
            Assert.Equal(1, authService.SoLanGoi);
        }

        [Fact]
        public async Task Login_BodyNull_TraVe400VaKhongGoiService()
        {
            var authService = new FakeMpSsoAuthService();
            var controller = CreateController(authService);

            var actionResult = await controller.Login(null, CancellationToken.None);

            var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
            var body = Assert.IsType<ApiResponse<MerchantSsoLoginResult>>(badRequest.Value);
            Assert.Equal(MpSsoResultCode.InvalidRequest, body.Code);
            Assert.Equal(0, authService.SoLanGoi);
        }

        [Fact]
        public async Task Login_ModelStateKhongHopLe_TraVe400KemThongBaoLoi()
        {
            var authService = new FakeMpSsoAuthService();
            var controller = CreateController(authService);
            controller.ModelState.AddModelError("AccessTokenSSO", "accessTokenSSO là bắt buộc");

            var actionResult = await controller.Login(ValidRequest(), CancellationToken.None);

            var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
            var body = Assert.IsType<ApiResponse<MerchantSsoLoginResult>>(badRequest.Value);
            Assert.Equal(MpSsoResultCode.InvalidRequest, body.Code);
            Assert.Contains("accessTokenSSO", body.Message);
            Assert.Equal(0, authService.SoLanGoi);
        }

        /// <summary>
        /// Service nổ thì client vẫn nhận response đúng khuôn, và tuyệt đối không thấy
        /// nội dung exception — thông tin đó chỉ được ghi vào log phía server.
        /// </summary>
        [Fact]
        public async Task Login_ServiceNemException_TraVeMa99VaKhongLoChiTietLoi()
        {
            var authService = new FakeMpSsoAuthService
            {
                NemException = new InvalidOperationException("chuoi ket noi Oracle sai o day")
            };
            var controller = CreateController(authService);

            var actionResult = await controller.Login(ValidRequest(), CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(actionResult);
            var body = Assert.IsType<ApiResponse<MerchantSsoLoginResult>>(ok.Value);
            Assert.Equal(MpSsoResultCode.SystemError, body.Code);
            Assert.DoesNotContain("Oracle", body.Message);
        }

        /// <summary>
        /// Client huỷ kết nối không phải lỗi hệ thống — phải để exception bay lên,
        /// không được nuốt rồi ghi log lỗi giả.
        /// </summary>
        [Fact]
        public async Task Login_ClientHuyKetNoi_NemLaiOperationCanceled()
        {
            var authService = new FakeMpSsoAuthService { NemException = new OperationCanceledException() };
            var controller = CreateController(authService);

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => controller.Login(ValidRequest(), CancellationToken.None));
        }

        [Fact]
        public async Task Login_ClientCoGuiClientIp_GiuNguyenKhongGhiDe()
        {
            var authService = new FakeMpSsoAuthService();
            var controller = CreateController(authService, remoteIp: "10.0.0.5");

            await controller.Login(ValidRequest(clientIp: "192.168.1.10"), CancellationToken.None);

            Assert.Equal("192.168.1.10", authService.RequestNhanDuoc!.Client.ClientIp);
        }

        /// <summary>
        /// clientIP theo tài liệu VCB tối đa 15 ký tự nên chỉ vừa IPv4. RemoteIpAddress
        /// thường trả IPv4-mapped IPv6 ("::ffff:10.0.0.5"), phải quy về IPv4 trước khi gửi
        /// sang SSO, nếu không SSO trả resCode 12.
        /// </summary>
        [Theory]
        [InlineData("10.0.0.5", "10.0.0.5")]
        [InlineData("::ffff:10.0.0.5", "10.0.0.5")]
        [InlineData("::1", "127.0.0.1")]
        public async Task Login_ClientKhongGuiClientIp_DienIpKetNoiDangIPv4(
            string remoteIp, string mongDoi)
        {
            var authService = new FakeMpSsoAuthService();
            var controller = CreateController(authService, remoteIp);

            await controller.Login(ValidRequest(clientIp: null), CancellationToken.None);

            Assert.Equal(mongDoi, authService.RequestNhanDuoc!.Client.ClientIp);
        }
    }
}
