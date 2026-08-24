using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using VcbPortalApi.Models.SSO;
using VcbPortalApi.Services.Sso;

namespace VcbPortalApi.Services
{
    public interface IMpSsoAuthService
    {
        /// <summary>
        /// UC-05/UC-06: verify one time token với SSO, đối chiếu MP_APP_USERS,
        /// quyết định AppMerchant có phải bắt nhập mật khẩu hay không.
        /// </summary>
        Task<ApiResponse<MerchantSsoLoginResult>> AuthenticateAsync(
            MerchantSsoLoginRequest request, CancellationToken ct = default);
    }

    public sealed class MpSsoAuthService : IMpSsoAuthService
    {
        private readonly IMpSsoClient _ssoClient;

        // TODO(DbContext): đổi thành tên DbContext thật của solution.
        private readonly VcbPortalDbContext _db;

        private readonly MpAuthOptions _authOptions;
        private readonly ILogger<MpSsoAuthService> _logger;

        public MpSsoAuthService(
            IMpSsoClient ssoClient,
            VcbPortalDbContext db,
            IOptions<MpAuthOptions> authOptions,
            ILogger<MpSsoAuthService> logger)
        {
            _ssoClient = ssoClient;
            _db = db;
            _authOptions = authOptions.Value;
            _logger = logger;
        }

        public async Task<ApiResponse<MerchantSsoLoginResult>> AuthenticateAsync(
            MerchantSsoLoginRequest request, CancellationToken ct = default)
        {
            var outcome = await VerifyAndResolveUserAsync(request.AccessTokenSSO, request.Client, ct);

            if (outcome.Error is not null)
            {
                await WriteSsoLogAsync(request.AccessTokenSSO, request.Client, outcome, outcome.Error, null, ct);
                return ApiResponse<MerchantSsoLoginResult>.Fail(outcome.Error.Code, outcome.Error.Message);
            }

            var info = outcome.Info!;
            var user = outcome.User!;

            var mismatch = FindMismatch(info, user);
            if (mismatch is not null)
            {
                _logger.LogWarning(
                    "Sai lệch dữ liệu tại {Field} (mode {Mode}). SSO={SsoValue}, DB={DbValue}, username={Username}",
                    mismatch.Field, _authOptions.HierarchyCompare,
                    mismatch.SsoValue, mismatch.DbValue, user.Username);

                var code = mismatch.Field == nameof(MpSsoUserInfo.MerchantUsername)
                    ? MpSsoResultCode.UsernameMismatch
                    : MpSsoResultCode.HierarchyMismatch;

                var failure = new Failure(code, "Thông tin tài khoản không khớp. Vui lòng liên hệ hỗ trợ.");

                await WriteSsoLogAsync(request.AccessTokenSSO, request.Client, outcome, failure,
                    new { mismatchField = mismatch.Field, ssoValue = mismatch.SsoValue, dbValue = mismatch.DbValue },
                    ct);

                return ApiResponse<MerchantSsoLoginResult>.Fail(failure.Code, failure.Message);
            }

            var (requirePassword, reason) = EvaluateFirstAuth(request.Client.DeviceId, user.Deviceid);

            _logger.LogInformation(
                "SSO thành công cho {Username} (cif {Cif}). requirePassword={RequirePassword} ({Reason})",
                user.Username, info.UserCif, requirePassword, reason);

            await WriteSsoLogAsync(request.AccessTokenSSO, request.Client, outcome,
                new Failure(MpSsoResultCode.Success, "Success"),
                new { requirePassword, reason },
                ct);

            return ApiResponse<MerchantSsoLoginResult>.Ok(new MerchantSsoLoginResult
            {
                Username = user.Username ?? string.Empty,
                Bid = user.Bid,
                Mid = user.Mid,
                Tid = user.Tid,
                RoleId = user.RoleId,
                BranchId = user.BranchId,
                UserCif = info.UserCif,
                UserFullName = info.UserFullName,
                AccountLinkCode = info.AccountLinkCode,
                RequirePassword = requirePassword,
                RequirePasswordReason = requirePassword ? reason : null
            });
        }

        // ---- Verify + tra MP_APP_USERS ----

        private sealed record Failure(string Code, string Message);

        /// <summary>
        /// Giữ cả Info lẫn Error để ghi log tra soát được kể cả khi luồng thất bại giữa chừng.
        /// </summary>
        private sealed record VerifyOutcome(
            MpSsoUserInfo? Info, MpAppUser? User, Failure? Error, int? SsoResCode);

        /// <summary>
        /// Username đem tra DB lấy từ othersInfo.userDes của SSO — KHÔNG lấy từ request.
        /// Client chỉ gửi token, nên không thể đổi username để mượn tài khoản người khác.
        /// </summary>
        private async Task<VerifyOutcome> VerifyAndResolveUserAsync(
            string accessToken, ClientContext client, CancellationToken ct)
        {
            var input = new MpSsoVerifyInput(accessToken, client.ClientIp ?? string.Empty, BuildAuditObject(client));

            MpSsoVerifyResult verify;
            try
            {
                verify = await _ssoClient.ValidateAccessTokenAsync(input, ct);
            }
            catch (MpSsoUnavailableException ex)
            {
                _logger.LogError(ex, "Không gọi được SSO");
                return new VerifyOutcome(null, null, new Failure(
                    MpSsoResultCode.SsoUnavailable, "Không kết nối được hệ thống SSO. Vui lòng thử lại."), null);
            }

            if (!verify.IsValid)
                return new VerifyOutcome(null, null, MapSsoFailure(verify), verify.ResCode);

            var info = verify.User!;

            // BR-01/BR-02 (liên kết phải Active) không kiểm tra ở đây: othersInfo không trả
            // trạng thái liên kết. Nhưng SSO chỉ cấp token khi có accountLinkCode, nên token
            // hợp lệ đã hàm ý liên kết đang tồn tại.

            if (string.IsNullOrWhiteSpace(info.MerchantUsername))
            {
                _logger.LogError(
                    "othersInfo không có userDes. Keys nhận được: {Keys}",
                    string.Join(",", info.OthersInfoRaw.Keys));

                return new VerifyOutcome(info, null, new Failure(
                    MpSsoResultCode.SsoResponseMalformed, "Dữ liệu trả về từ SSO không hợp lệ."), verify.ResCode);
            }

            var user = await FindUserAsync(info.MerchantUsername, ct);
            if (user is null)
            {
                _logger.LogWarning("Không tìm thấy user {Username} trong MP_APP_USERS", info.MerchantUsername);
                return new VerifyOutcome(info, null, new Failure(
                    MpSsoResultCode.UserNotFound, "Tài khoản DigiMerchant không tồn tại."), verify.ResCode);
            }

            return new VerifyOutcome(info, user, null, verify.ResCode);
        }

        /// <summary>
        /// Luồng này chỉ ĐỌC MP_APP_USERS nên luôn AsNoTracking.
        ///
        /// TODO(entity): tên property giả định theo quy tắc scaffold EF từ cột Oracle —
        /// USERNAME→Username, ROLE_ID→RoleId, BID→Bid, MID→Mid, TID→Tid, FCM_TOKEN→FcmToken,
        /// FID→Fid, DEVICEID→Deviceid, OS→Os, NOTE→Note, BRANCH_ID→BranchId.
        /// Nếu entity thật đặt tên khác thì compiler sẽ chỉ thẳng vào chỗ cần sửa.
        /// </summary>
        private async Task<MpAppUser?> FindUserAsync(string username, CancellationToken ct)
        {
            var normalized = username.Trim().ToUpperInvariant();

            return await _db.Set<MpAppUser>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username != null && u.Username.ToUpper() == normalized, ct);
        }

        private static Failure MapSsoFailure(MpSsoVerifyResult verify) => verify.Failure switch
        {
            MpSsoFailureKind.InvalidToken => new Failure(
                MpSsoResultCode.SsoTokenInvalid,
                "Phiên truy cập không hợp lệ hoặc đã hết hạn. Vui lòng thử lại từ Digibank."),

            MpSsoFailureKind.InvalidClientIp => new Failure(
                MpSsoResultCode.SsoClientIpInvalid, "Không xác thực được thiết bị truy cập. Vui lòng thử lại."),

            MpSsoFailureKind.Timeout => new Failure(
                MpSsoResultCode.SsoUnavailable, "Hệ thống SSO đang bận. Vui lòng thử lại."),

            _ => new Failure(MpSsoResultCode.SsoResponseMalformed, "Không xác thực được phiên truy cập.")
        };

        /// <summary>
        /// Dựng AuditObject có sẵn của solution từ thông tin client gửi lên.
        ///
        /// AuditObject KHÔNG có clientIP — trường đó nằm ở ValidateTokenRequestPayload.clientIP.
        /// userAgent bỏ trống: theo tài liệu VCB nó chỉ dùng khi clientType = WEB.
        ///
        /// Property của AuditObject khai báo non-nullable với default null!, nên dùng
        /// null-forgiving để giữ nguyên null khi client không gửi, thay vì ép thành chuỗi rỗng.
        /// </summary>
        private static AuditObject BuildAuditObject(ClientContext client) => new()
        {
            sessionId = client.SessionId,
            clientType = "MOBILE",
            deviceToken = client.DeviceId!,
            deviceType = client.DeviceType!,
            appVersion = client.AppVersion!,
            osType = client.OsType!,
            osVersion = client.OsVersion!
        };

        /// <summary>
        /// BR-08/BR-09: quyết định có bắt nhập mật khẩu không, dựa trên DEVICEID.
        ///
        /// Luồng SSO này chỉ ĐỌC DEVICEID, không ghi. Việc ghi thuộc luồng đăng nhập /
        /// kích hoạt thiết bị sẵn có của DigiMerchant. Nghĩa là: khách đã kích hoạt
        /// DigiMerchant trên đúng máy này thì DEVICEID khớp và vào thẳng; kích hoạt ở máy
        /// khác thì DEVICEID đổi và lần SSO sau bị bắt nhập lại — đúng tinh thần BR-09.
        ///
        /// Mọi trường hợp không chắc chắn đều nghiêng về YÊU CẦU nhập mật khẩu.
        /// </summary>
        private (bool RequirePassword, string Reason) EvaluateFirstAuth(string? requestDeviceId, string? storedDeviceId)
        {
            if (!_authOptions.UseDeviceIdForFirstAuth)
                return (true, "Cơ chế nhận diện thiết bị đang tắt");

            if (string.IsNullOrWhiteSpace(requestDeviceId))
                return (true, "Client không gửi deviceId");

            if (string.IsNullOrWhiteSpace(storedDeviceId))
                return (true, "Chưa có thiết bị nào được ghi nhận cho user");

            return string.Equals(requestDeviceId.Trim(), storedDeviceId.Trim(), StringComparison.Ordinal)
                ? (false, "Thiết bị đã xác thực")
                : (true, "Phát hiện đăng nhập từ thiết bị khác");
        }

        // ---- Đối chiếu dữ liệu SSO vs MP_APP_USERS ----

        private sealed record FieldMismatch(string Field, string? SsoValue, string? DbValue);

        /// <summary>
        /// So khớp từng trường, trả về trường lệch đầu tiên (null nếu khớp hết).
        ///
        /// Username là chốt định danh — luôn so chuỗi chính xác, không phân biệt hoa thường.
        /// BID/MID/TID là kiểm tra phụ, cách so tuỳ MpAuth:HierarchyCompare vì SSO trả
        /// "B001" còn cột Oracle là NUMBER.
        /// null/rỗng ở cả hai phía coi là khớp (user cấp BID không có MID/TID).
        /// </summary>
        private FieldMismatch? FindMismatch(MpSsoUserInfo info, MpAppUser user)
        {
            if (!TextMatches(info.MerchantUsername, user.Username))
                return new FieldMismatch(nameof(MpSsoUserInfo.MerchantUsername), info.MerchantUsername, user.Username);

            if (_authOptions.HierarchyCompare == HierarchyCompareMode.Skip)
                return null;

            if (!HierarchyMatches(info.Bid, user.Bid))
                return new FieldMismatch(nameof(MpSsoUserInfo.Bid), info.Bid, user.Bid?.ToString());

            if (!HierarchyMatches(info.Mid, user.Mid))
                return new FieldMismatch(nameof(MpSsoUserInfo.Mid), info.Mid, user.Mid?.ToString());

            if (!HierarchyMatches(info.Tid, user.Tid))
                return new FieldMismatch(nameof(MpSsoUserInfo.Tid), info.Tid, user.Tid?.ToString());

            return null;
        }

        private static bool TextMatches(string? left, string? right) =>
            string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);

        private bool HierarchyMatches(string? ssoValue, decimal? dbValue)
        {
            var hasSso = !string.IsNullOrWhiteSpace(ssoValue);

            // Cả hai cùng trống => khớp; một bên trống => lệch.
            if (!hasSso || dbValue is null) return !hasSso && dbValue is null;

            var raw = ssoValue!.Trim();

            return _authOptions.HierarchyCompare switch
            {
                // "B001" -> "001" -> 1. So trong cùng một trường (bid với bid) nên việc bỏ
                // chữ cái không gây nhầm giữa các cấp.
                HierarchyCompareMode.DigitsOnly =>
                    TryParseDecimal(new string(raw.Where(char.IsDigit).ToArray()), out var digits)
                    && digits == dbValue.Value,

                HierarchyCompareMode.Numeric =>
                    TryParseDecimal(raw, out var numeric) && numeric == dbValue.Value,

                HierarchyCompareMode.Exact =>
                    string.Equals(raw, dbValue.Value.ToString(CultureInfo.InvariantCulture),
                        StringComparison.OrdinalIgnoreCase),

                _ => true
            };
        }

        private static bool TryParseDecimal(string value, out decimal parsed) =>
            decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed);

        private static string Normalize(string? value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

        // ---- Ghi log tra soát vào MP_SSO_LOG ----

        private async Task WriteSsoLogAsync(
            string token,
            ClientContext client,
            VerifyOutcome outcome,
            Failure result,
            object? detail,
            CancellationToken ct)
        {
            MpSsoLog? log = null;

            try
            {
                log = new MpSsoLog
                {
                    CreateTime = DateTime.Now,

                    // Token lưu vào bảng log là convention sẵn có của luồng SSO hiện tại.
                    // Đây là one time token, dùng xong hết giá trị. Vẫn KHÔNG ghi ra ILogger.
                    Token = token,

                    RealIp = client.ClientIp,
                    UaPlatform = client.OsType,
                    UaName = client.DeviceType,

                    UserId = outcome.Info?.SourceUserId,
                    UserFullName = outcome.Info?.UserFullName,
                    UserRole = outcome.Info?.UserRole,
                    UserOf = outcome.Info?.UserOf,
                    UserCif = outcome.Info?.UserCif,

                    Bid = outcome.User?.Bid,

                    // MP_SSO_LOG không có cột cho MID/TID, username DigiMerchant, deviceId
                    // hay requirePassword — dồn vào Response để khỏi phải đổi schema.
                    Response = BuildLogResponse(outcome, result, client, detail)
                };

                _db.Set<MpSsoLog>().Add(log);
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                // Nuốt lỗi có chủ đích: mất một dòng log không đáng để chặn khách đăng nhập.
                _logger.LogError(ex, "Không ghi được MpSsoLog cho kết quả {ResultCode}", result.Code);

                // Gỡ khỏi change tracker để lần SaveChanges sau không thử lại và hỏng theo.
                if (log is not null) _db.Entry(log).State = EntityState.Detached;
            }
        }

        private static string BuildLogResponse(
            VerifyOutcome outcome, Failure result, ClientContext client, object? detail)
        {
            var payload = new
            {
                resultCode = result.Code,
                resultMessage = result.Message,
                ssoResCode = outcome.SsoResCode,
                merchantUsername = outcome.Info?.MerchantUsername,
                bid = outcome.Info?.Bid,
                mid = outcome.Info?.Mid,
                tid = outcome.Info?.Tid,
                accountLinkCode = outcome.Info?.AccountLinkCode,
                userOf = outcome.Info?.UserOf,
                deviceId = client.DeviceId,
                sessionId = client.SessionId,
                detail
            };

            var json = JsonConvert.SerializeObject(payload);

            // Cột RESPONSE là VARCHAR2 nên phải cắt, tránh ORA-12899 làm hỏng cả SaveChanges.
            // TODO(DDL): chỉnh cho khớp độ dài thật của cột MP_SSO_LOG.RESPONSE.
            const int maxLength = 4000;
            return json.Length <= maxLength ? json : json[..maxLength];
        }
    }
}
