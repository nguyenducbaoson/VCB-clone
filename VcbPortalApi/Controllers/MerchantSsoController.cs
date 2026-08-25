using System.Net;
using Microsoft.AspNetCore.Mvc;
using VcbPortalApi.Models.SSO;
using VcbPortalApi.Services;

namespace VcbPortalApi.Controllers
{
    /// <summary>
    /// SSO Digibank → DigiMerchant. AppMerchant gọi vào đây sau khi nhận one time token
    /// từ AppDigBank qua deep link.
    ///
    /// TODO: solution đã có SSOController / SSOController2 / MerchantController —
    /// cân nhắc gộp action này vào controller phù hợp thay vì thêm controller mới.
    /// </summary>
    [ApiController]
    [Route("api/v1/merchant-sso")]
    public sealed class MerchantSsoController : ControllerBase
    {
        private readonly IMpSsoAuthService _authService;
        private readonly ILogger<MerchantSsoController> _logger;

        public MerchantSsoController(IMpSsoAuthService authService, ILogger<MerchantSsoController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        /// <summary>
        /// UC-05/UC-06. Verify one time token với VCB SSO, đối chiếu othersInfo với
        /// MP_APP_USERS, rồi trả về requirePassword cho AppMerchant quyết định màn hình kế tiếp.
        /// </summary>
        [HttpPost("login")]
        [ProducesResponseType(typeof(ApiResponse<MerchantSsoLoginResult>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<MerchantSsoLoginResult>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Login([FromBody] MerchantSsoLoginRequest? request, CancellationToken ct)
        {
            if (request is null)
            {
                return BadRequest(ApiResponse<MerchantSsoLoginResult>.Fail(
                    MpSsoResultCode.InvalidRequest, "Body request không hợp lệ."));
            }

            if (!ModelState.IsValid)
            {
                var message = string.Join("; ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));

                return BadRequest(ApiResponse<MerchantSsoLoginResult>.Fail(
                    MpSsoResultCode.InvalidRequest, message));
            }

            FillClientIpIfMissing(request.Client);

            try
            {
                var result = await _authService.AuthenticateAsync(request, ct);
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                throw; // client ngắt kết nối — không phải lỗi hệ thống
            }
            catch (Exception ex)
            {
                // Không lộ chi tiết lỗi ra client; token không bao giờ ghi ra ILogger.
                _logger.LogError(ex, "Lỗi không mong đợi khi SSO login");

                return Ok(ApiResponse<MerchantSsoLoginResult>.Fail(
                    MpSsoResultCode.SystemError, "Hệ thống đang bận. Vui lòng thử lại sau."));
            }
        }

        /// <summary>
        /// SSO so khớp ValidateTokenRequestPayload.clientIP với IP lúc cấp token
        /// (resCode 12 nếu sai), nên IP phải là của THIẾT BỊ khách hàng.
        /// Client không gửi thì lấy tạm IP kết nối đến — chỉ đúng khi mobile gọi thẳng BE;
        /// qua gateway thì phải cấu hình ForwardedHeaders.
        /// </summary>
        private void FillClientIpIfMissing(ClientContext? client)
        {
            if (client is null || !string.IsNullOrWhiteSpace(client.ClientIp)) return;

            client.ClientIp = NormalizeIp(HttpContext.Connection.RemoteIpAddress);

            _logger.LogWarning(
                "Client không gửi client.clientIp — dùng tạm IP kết nối {Ip}. " +
                "SSO có thể từ chối với resCode 12 nếu đây không phải IP của thiết bị.",
                client.ClientIp);
        }

        /// <summary>
        /// clientIP theo tài liệu VCB có độ dài tối đa 15 — chỉ vừa IPv4.
        /// RemoteIpAddress thường trả IPv4-mapped IPv6 ("::ffff:10.0.0.1") hoặc "::1" với localhost,
        /// nên phải quy về dạng IPv4 trước khi gửi đi.
        /// </summary>
        private static string? NormalizeIp(IPAddress? address)
        {
            if (address is null) return null;

            if (address.IsIPv4MappedToIPv6)
                return address.MapToIPv4().ToString();

            if (IPAddress.IsLoopback(address))
                return IPAddress.Loopback.ToString();

            return address.ToString();
        }
    }
}
