using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using VcbPortalApi.Controllers;
using VcbPortalApi.Models.SSO;
using VcbPortalApi.Services;

namespace VcbPortalApi.UnitTests.Controllers
{
    /// <summary>
    /// Controller có tách service nên chỉ cần mock service, không cần DbContext.
    /// Đây là ca dễ nhất — mẫu nên hướng tới khi viết controller mới.
    ///
    /// Test ở tầng này KHÔNG kiểm tra logic nghiệp vụ (service test lo) mà kiểm tra
    /// phần việc riêng của controller: validate đầu vào, chọn mã HTTP, không rò rỉ
    /// chi tiết lỗi ra client, và điền IP thiết bị khi client không gửi.
    /// </summary>
    public class MerchantSsoControllerTests
    {
        private readonly Mock<IMpSsoAuthService> _authServiceMock = new();

        /// <summary>
        /// Controller đọc HttpContext.Connection.RemoteIpAddress nên phải gắn HttpContext
        /// giả, nếu không sẽ NullReferenceException.
        /// </summary>
        private MerchantSsoController CreateController(string remoteIp = "10.0.0.5")
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);

            return new MerchantSsoController(
                _authServiceMock.Object,
                NullLogger<MerchantSsoController>.Instance)
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

        /// <summary>Bắt service trả về thành công, đồng thời giữ lại request nhận được.</summary>
        private void SetupAuthSuccess(Action<MerchantSsoLoginRequest>? capture = null) =>
            _authServiceMock
                .Setup(x => x.AuthenticateAsync(It.IsAny<MerchantSsoLoginRequest>(), It.IsAny<CancellationToken>()))
                .Callback<MerchantSsoLoginRequest, CancellationToken>((r, _) => capture?.Invoke(r))
                .ReturnsAsync(ApiResponse<MerchantSsoLoginResult>.Ok(
                    new MerchantSsoLoginResult { Username = "VATID001" }));

        [Fact]
        public async Task Login_WhenRequestValid_Returns200WithSuccessCode()
        {
            // Arrange
            SetupAuthSuccess();
            var controller = CreateController();

            // Act
            var result = await controller.Login(ValidRequest(), CancellationToken.None);

            // Assert
            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            var body = ok.Value.Should().BeOfType<ApiResponse<MerchantSsoLoginResult>>().Subject;
            body.Code.Should().Be(MpSsoResultCode.Success);

            _authServiceMock.Verify(
                x => x.AuthenticateAsync(It.IsAny<MerchantSsoLoginRequest>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Login_WhenBodyIsNull_Returns400AndDoesNotCallService()
        {
            // Arrange
            var controller = CreateController();

            // Act
            var result = await controller.Login(null, CancellationToken.None);

            // Assert
            var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            var body = badRequest.Value.Should().BeOfType<ApiResponse<MerchantSsoLoginResult>>().Subject;
            body.Code.Should().Be(MpSsoResultCode.InvalidRequest);

            _authServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Login_WhenModelStateInvalid_Returns400WithValidationMessage()
        {
            // Arrange
            var controller = CreateController();
            controller.ModelState.AddModelError("AccessTokenSSO", "accessTokenSSO là bắt buộc");

            // Act
            var result = await controller.Login(ValidRequest(), CancellationToken.None);

            // Assert
            var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            var body = badRequest.Value.Should().BeOfType<ApiResponse<MerchantSsoLoginResult>>().Subject;
            body.Code.Should().Be(MpSsoResultCode.InvalidRequest);
            body.Message.Should().Contain("accessTokenSSO");

            _authServiceMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// Service nổ thì client vẫn nhận response đúng khuôn, và tuyệt đối không thấy
        /// nội dung exception — thông tin đó chỉ được ghi vào log phía server.
        /// </summary>
        [Fact]
        public async Task Login_WhenServiceThrows_ReturnsSystemErrorWithoutLeakingDetails()
        {
            // Arrange
            _authServiceMock
                .Setup(x => x.AuthenticateAsync(It.IsAny<MerchantSsoLoginRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("chuoi ket noi Oracle sai o day"));

            var controller = CreateController();

            // Act
            var result = await controller.Login(ValidRequest(), CancellationToken.None);

            // Assert
            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            var body = ok.Value.Should().BeOfType<ApiResponse<MerchantSsoLoginResult>>().Subject;
            body.Code.Should().Be(MpSsoResultCode.SystemError);
            body.Message.Should().NotContain("Oracle");
        }

        /// <summary>
        /// Client huỷ kết nối không phải lỗi hệ thống — phải để exception bay lên,
        /// không được nuốt rồi ghi log lỗi giả.
        /// </summary>
        [Fact]
        public async Task Login_WhenClientCancels_RethrowsOperationCanceled()
        {
            // Arrange
            _authServiceMock
                .Setup(x => x.AuthenticateAsync(It.IsAny<MerchantSsoLoginRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            var controller = CreateController();

            // Act
            var act = () => controller.Login(ValidRequest(), CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task Login_WhenClientSendsClientIp_KeepsItUnchanged()
        {
            // Arrange
            MerchantSsoLoginRequest? captured = null;
            SetupAuthSuccess(r => captured = r);
            var controller = CreateController(remoteIp: "10.0.0.5");

            // Act
            await controller.Login(ValidRequest(clientIp: "192.168.1.10"), CancellationToken.None);

            // Assert
            captured!.Client.ClientIp.Should().Be("192.168.1.10");
        }

        /// <summary>
        /// clientIP theo tài liệu VCB tối đa 15 ký tự nên chỉ vừa IPv4. RemoteIpAddress
        /// thường trả IPv4-mapped IPv6 ("::ffff:10.0.0.5"), phải quy về IPv4 trước khi
        /// gửi sang SSO, nếu không SSO trả resCode 12.
        /// </summary>
        [Theory]
        [InlineData("10.0.0.5", "10.0.0.5")]
        [InlineData("::ffff:10.0.0.5", "10.0.0.5")]
        [InlineData("::1", "127.0.0.1")]
        public async Task Login_WhenClientOmitsClientIp_FillsConnectionIpAsIPv4(
            string remoteIp, string expected)
        {
            // Arrange
            MerchantSsoLoginRequest? captured = null;
            SetupAuthSuccess(r => captured = r);
            var controller = CreateController(remoteIp);

            // Act
            await controller.Login(ValidRequest(clientIp: null), CancellationToken.None);

            // Assert
            captured!.Client.ClientIp.Should().Be(expected);
        }
    }
}
