using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using VcbPortalApi.Models;

namespace VcbPortalApi.Services
{
    public interface IMerchantInfoAuthorizationClient
    {
        Task<MerchantAuthorizationResponse> AuthorizeAsync(
            MerchantAuthorizationRequest request,
            AuthenticationHeaderValue authorization,
            CancellationToken cancellationToken = default);
    }

    public sealed record MerchantAuthorizationResponse(int StatusCode, string Body, string? ContentType);

    public sealed class MerchantInfoAuthorizationClient : IMerchantInfoAuthorizationClient
    {
        private const string AuthorizationPath = "/api/v2/merchant/app/merchant-info/authorization";

        private readonly HttpClient _httpClient;

        public MerchantInfoAuthorizationClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<MerchantAuthorizationResponse> AuthorizeAsync(
            MerchantAuthorizationRequest request,
            AuthenticationHeaderValue authorization,
            CancellationToken cancellationToken = default)
        {
            var json = JsonConvert.SerializeObject(request);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, AuthorizationPath)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            httpRequest.Headers.Authorization = authorization;

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            return new MerchantAuthorizationResponse(
                (int)response.StatusCode,
                body,
                response.Content.Headers.ContentType?.ToString());
        }
    }
}