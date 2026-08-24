using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using VcbPortalApi.Models;
using VcbPortalApi.Services;

namespace VcbPortalApi.Controllers
{
    [ApiController]
    [Route("api/v1/merchant-info")]
    public sealed class MerchantInfoAuthorizationController : ControllerBase
    {
        private readonly IMerchantInfoAuthorizationClient _client;
        private readonly ILogger<MerchantInfoAuthorizationController> _logger;

        public MerchantInfoAuthorizationController(
            IMerchantInfoAuthorizationClient client,
            ILogger<MerchantInfoAuthorizationController> logger)
        {
            _client = client;
            _logger = logger;
        }

        [HttpPost("authorization")]
        public async Task<IActionResult> Authorization(
            [FromHeader(Name = "Authorization")] string? authorizationHeader,
            [FromBody] MerchantAuthorizationRequest? request,
            CancellationToken cancellationToken)
        {
            if (!AuthenticationHeaderValue.TryParse(authorizationHeader, out var authorization) ||
                !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(authorization.Parameter))
            {
                return Unauthorized(new { message = "Authorization Bearer token là bắt buộc." });
            }

            if (request is null || !ModelState.IsValid)
                return ValidationProblem(ModelState);

            try
            {
                var result = await _client.AuthorizeAsync(request, authorization, cancellationToken);

                return new ContentResult
                {
                    StatusCode = result.StatusCode,
                    Content = result.Body,
                    ContentType = result.ContentType
                };
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError("Timeout khi gọi Merchant API authorization");
                return StatusCode(StatusCodes.Status504GatewayTimeout,
                    new { message = "Merchant API không phản hồi kịp thời." });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Không kết nối được Merchant API authorization");
                return StatusCode(StatusCodes.Status502BadGateway,
                    new { message = "Không kết nối được Merchant API." });
            }
        }
    }
}