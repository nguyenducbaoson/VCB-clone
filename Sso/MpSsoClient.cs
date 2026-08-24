using System.Text;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VcbPortalApi.Helpers;
using VcbPortalApi.Models.SSO;

namespace VcbPortalApi.Services.Sso
{
    /// <summary>
    /// Gọi /TokenManager/ValidateAccessToken của VCB SSO.
    ///
    /// Request dựng bằng ValidateTokenRequest có sẵn (CreateSignature/CreateStringContent).
    /// Response KHÔNG parse bằng SsoBaseResponse được: bản tin thật trả header/payload
    /// dạng object lồng, trong khi SsoBaseMessage khai báo chúng là string — deserialize
    /// thẳng sẽ ném "Unexpected token: StartObject". Nên đọc bằng JObject và chấp nhận
    /// cả hai dạng (object lồng hoặc chuỗi JSON).
    /// </summary>
    public sealed class MpSsoClient : IMpSsoClient
    {
        private const string TimeFormat = "yyyy-MM-ddTHH:mm:ss";
        private const string MsgNameValidate = "ValidateAccessToken";

        // resCode theo bảng mã dùng chung của VCB.
        private const int ResCodeSuccess = 0;
        private const int ResCodeInvalidToken = 10;
        private const int ResCodeInvalidClientIp = 12;
        private const int ResCodeTimeoutFrom = 99;
        private const int ResCodeTimeoutTo = 199;

        private readonly HttpClient _http;
        private readonly MpSsoOptions _options;
        private readonly ILogger<MpSsoClient> _logger;

        public MpSsoClient(HttpClient http, IOptions<MpSsoOptions> options, ILogger<MpSsoClient> logger)
        {
            _http = http;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<MpSsoVerifyResult> ValidateAccessTokenAsync(
            MpSsoVerifyInput input, CancellationToken ct = default)
        {
            var raw = await PostValidateAsync(input, ct);
            if (raw.Failure is not null) return raw.Failure;

            JObject root;
            try
            {
                root = JObject.Parse(raw.Body!);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Response ValidateAccessToken không phải JSON hợp lệ");
                return MpSsoVerifyResult.Fail(MpSsoFailureKind.Other, 0, "Response SSO sai định dạng");
            }

            var (headerJson, header) = ReadSection<SsoBaseResponseHeader>(root["header"]);
            if (header is null)
            {
                _logger.LogError("Response ValidateAccessToken thiếu header");
                return MpSsoVerifyResult.Fail(MpSsoFailureKind.Other, 0, "Response SSO sai định dạng");
            }

            if (header.resCode != ResCodeSuccess)
                return MapFailure(header);

            var (payloadJson, payload) = ReadSection<ValidateTokenResponsePayload>(root["payload"]);

            if (!VerifySignature(headerJson, payloadJson, root["signature"]?.Value<string>()))
                return MpSsoVerifyResult.Fail(MpSsoFailureKind.Other, header.resCode, "Chữ ký response không hợp lệ");

            if (payload is null)
            {
                _logger.LogError("Response ValidateAccessToken thiếu payload");
                return MpSsoVerifyResult.Fail(MpSsoFailureKind.Other, header.resCode, "Payload response không hợp lệ");
            }

            var othersInfo = FlattenOthersInfo(payload.othersInfo);

            var user = new MpSsoUserInfo
            {
                SourceUserId = payload.userId,
                UserFullName = payload.userFullName,
                UserRole = payload.userRole,
                UserOf = payload.userOf,
                UserCif = FormatCif(payload.userCIF),
                MerchantUsername = PickFirst(othersInfo, _options.UsernameFieldNames),
                Bid = PickFirst(othersInfo, _options.BidFieldNames),
                Mid = PickFirst(othersInfo, _options.MidFieldNames),
                Tid = PickFirst(othersInfo, _options.TidFieldNames),
                AccountLinkCode = PickFirst(othersInfo, _options.AccountLinkCodeFieldNames),
                OthersInfoRaw = othersInfo
            };

            return new MpSsoVerifyResult { IsValid = true, ResCode = header.resCode, User = user };
        }

        // ---- Gửi request ----

        private async Task<(string? Body, MpSsoVerifyResult? Failure)> PostValidateAsync(
            MpSsoVerifyInput input, CancellationToken ct)
        {
            var request = new ValidateTokenRequest();

            request.HeaderObj.msgID = Guid.NewGuid().ToString();
            request.HeaderObj.msgName = MsgNameValidate;
            request.HeaderObj.appId = _options.AppId;
            request.HeaderObj.requestTime = DateTime.Now.ToString(TimeFormat);

            request.PayloadObj.clientIP = input.ClientIp;
            request.PayloadObj.accessTokenSSO = input.AccessTokenSSO;
            request.PayloadObj.auditObject = input.Audit;

            // Ký bằng hàm có sẵn — nó serialize header/payload rồi HMAC-SHA256 + ToUpper().
            // Tự ký tay sẽ ra chữ ký chữ thường và bị SSO từ chối.
            request.CreateSignature();

            try
            {
                using var content = new StringContent(
                    request.CreateStringContent(), Encoding.UTF8, "application/json");

                using var response = await _http.PostAsync(_options.ValidateAccessTokenPath, content, ct);
                var body = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("{MsgName} trả HTTP {Status}: {Body}",
                        MsgNameValidate, (int)response.StatusCode, body);
                    return (null, MpSsoVerifyResult.Fail(
                        MpSsoFailureKind.Other, (int)response.StatusCode, "SSO trả về lỗi HTTP"));
                }

                return (body, null);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogError(ex, "Timeout khi gọi {MsgName}", MsgNameValidate);
                throw new MpSsoUnavailableException("Timeout khi gọi SSO", ex);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Lỗi kết nối tới SSO khi gọi {MsgName}", MsgNameValidate);
                throw new MpSsoUnavailableException("Không kết nối được SSO", ex);
            }
        }

        // ---- Đọc envelope ----

        /// <summary>
        /// Đọc một khối header/payload. Chấp nhận cả hai dạng:
        /// object lồng (như bản tin thật) và chuỗi JSON (như SsoBaseMessage khai báo).
        /// Trả về cả chuỗi JSON dùng để tính chữ ký lẫn object đã parse.
        /// </summary>
        private static (string Json, T? Value) ReadSection<T>(JToken? token) where T : class
        {
            if (token is null || token.Type == JTokenType.Null)
                return (string.Empty, null);

            if (token.Type == JTokenType.String)
            {
                var json = token.Value<string>() ?? string.Empty;
                return (json, string.IsNullOrWhiteSpace(json) ? null : JsonConvert.DeserializeObject<T>(json));
            }

            return (token.ToString(Formatting.None), token.ToObject<T>());
        }

        /// <summary>
        /// Chữ ký = HMAC-SHA256(header + payload, secretKey), hex chữ hoa
        /// (response mẫu: "C0DA13F969...", 64 ký tự).
        ///
        /// Khi header/payload về dạng object, chuỗi đem tính là bản serialize lại của mình —
        /// khác một khoảng trắng hay thứ tự key là lệch. Log đủ expected/actual để debug nhanh.
        /// </summary>
        private bool VerifySignature(string headerJson, string payloadJson, string? signature)
        {
            if (!_options.VerifyResponseSignature) return true;

            var expected = HMAC256.HmacSha256(headerJson + payloadJson, AppSettings.SsoPrdHmacSecretKey);

            if (!string.IsNullOrWhiteSpace(signature) &&
                string.Equals(expected, signature, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            _logger.LogError(
                "Chữ ký response không khớp. expected={Expected}, actual={Actual}, signedString={Signed}",
                expected, signature, headerJson + payloadJson);

            return false;
        }

        private MpSsoVerifyResult MapFailure(SsoBaseResponseHeader header)
        {
            _logger.LogWarning("SSO từ chối. resCode={Code}, resMessage={Message}",
                header.resCode, header.resMessage);

            return header.resCode switch
            {
                ResCodeInvalidToken =>
                    MpSsoVerifyResult.Fail(MpSsoFailureKind.InvalidToken, header.resCode, header.resMessage),

                ResCodeInvalidClientIp =>
                    MpSsoVerifyResult.Fail(MpSsoFailureKind.InvalidClientIp, header.resCode, header.resMessage),

                >= ResCodeTimeoutFrom and <= ResCodeTimeoutTo =>
                    MpSsoVerifyResult.Fail(MpSsoFailureKind.Timeout, header.resCode, header.resMessage),

                _ => MpSsoVerifyResult.Fail(MpSsoFailureKind.Other, header.resCode, header.resMessage)
            };
        }

        /// <summary>
        /// userCIF trong ValidateTokenResponsePayload là int (response mẫu: 24094302).
        /// Nếu CIF thật có số 0 đứng đầu thì kiểu int đã làm mất từ lúc parse — khi đó
        /// phải đổi property đó sang string.
        /// </summary>
        private static string? FormatCif(int userCif) =>
            userCif <= 0 ? null : userCif.ToString();

        // ---- othersInfo ----

        private static Dictionary<string, string?> FlattenOthersInfo(Dictionary<string, object>? source)
        {
            var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            if (source is null) return map;

            foreach (var kv in source)
                map[kv.Key] = kv.Value?.ToString();

            return map;
        }

        private static string? PickFirst(IReadOnlyDictionary<string, string?> map, string[] keys)
        {
            foreach (var key in keys)
                if (map.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                    return value;
            return null;
        }
    }
}
