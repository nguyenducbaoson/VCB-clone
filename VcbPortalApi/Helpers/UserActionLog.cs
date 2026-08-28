using Microsoft.EntityFrameworkCore;
using VcbPortalApi.DbContext.Oracle;
using VcbPortalApi.Models.MobileApp;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG. UserActionLogHelper dưới đây CHÉP NGUYÊN VĂN từ ảnh code thật.
//
// MỘT KHÁC BIỆT ĐÃ BIẾT: bản thật `using VcbPortalApi.DbContext.Oracle;`, tức
// FrontendContext nằm trong namespace ...DbContext.Oracle. Repo này đang để ở
// VcbPortalApi.DbContext. Chỉ khác namespace, không khác hành vi.
//
// UserActionLogTypes thì CHƯA có ảnh — dựng lại theo đúng những thành viên mà
// FepController và Insert/CountConsecutiveFailuresAsync gọi tới.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Helpers
{
    /// <summary>
    /// DỰNG LẠI — chưa có ảnh. Giữ đúng các thành viên được gọi tới:
    /// Action.LoginMobile, ResultCode.WrongPassword, Source.Web,
    /// NormalizeResult(result), IsFailure(result).
    /// </summary>
    public static class UserActionLogTypes
    {
        public static class Action
        {
            public const string LoginMobile = "LOGIN_MOBILE";
        }

        public static class ResultCode
        {
            public const string Success = "OK";
            public const string WrongPassword = "WRONGPWD";
        }

        public static class Source
        {
            public const string Web = "WEB";
        }

        /// <summary>Chuẩn hoá mã kết quả. Insert bắt buộc kết quả tối đa 10 ký tự.</summary>
        public static string NormalizeResult(string? result) =>
            (result ?? string.Empty).Trim().ToUpperInvariant();

        /// <summary>Kết quả nào KHÔNG phải thành công thì tính là thất bại.</summary>
        public static bool IsFailure(string? result) =>
            !string.Equals(NormalizeResult(result), ResultCode.Success, StringComparison.Ordinal);
    }

    public static class UserActionLogHelper
    {
        public static void TryLog(
            string action,
            string result,
            string? userName = null,
            string? message = null,
            string? extraData = null,
            string? requestIp = null,
            string? source = null)
        {
            try
            {
                using var context = new FrontendContext();
                Insert(context, action, result, userName, message, extraData, requestIp, source);
                _ = context.SaveChanges();
            }
            catch (Exception ex)
            {
                AppSettings.Logger.Error(ex);
            }
        }

        public static async Task TryLogAsync(
            string action,
            string result,
            string? userName = null,
            string? message = null,
            string? extraData = null,
            string? requestIp = null,
            string? source = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await using var context = new FrontendContext();
                Insert(context, action, result, userName, message, extraData, requestIp, source);
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                AppSettings.Logger.Error(ex);
            }
        }

        /// <summary>Ghi log dùng chung DbContext (cùng transaction với logic gọi).</summary>
        public static void Insert(
            FrontendContext context,
            string action,
            string result,
            string? userName = null,
            string? message = null,
            string? extraData = null,
            string? requestIp = null,
            string? source = null)
        {
            if (string.IsNullOrWhiteSpace(action))
                throw new ArgumentException("Action is required.", nameof(action));

            var normalizedResult = UserActionLogTypes.NormalizeResult(result);
            if (normalizedResult.Length > 10)
                throw new ArgumentException("Result max length is 10.", nameof(result));

            context.MpAppUserActionLogs.Add(new MpAppUserActionLog
            {
                CreateTime = DateTime.Now,
                UserName = Trunc(userName?.Trim().ToUpperInvariant(), 100),
                Action = Trunc(action.Trim().ToUpperInvariant(), 50)!,
                Result = normalizedResult,
                Message = Trunc(message, 500),
                ExtraData = Trunc(extraData, 2000),
                RequestIp = Trunc(requestIp, 100),
                Source = Trunc(source?.Trim().ToUpperInvariant(), 20),
            });
        }

        /// count số lần thất bại liên tiếp của action
        public static async Task<int> CountConsecutiveFailuresAsync(
            FrontendContext context,
            string userName,
            string action,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(action))
                return 0;

            var normalizedUser = userName.Trim().ToUpperInvariant();
            var normalizedAction = action.Trim().ToUpperInvariant();

            var recent = await context.MpAppUserActionLogs
                .AsNoTracking()
                .Where(x => x.UserName == normalizedUser && x.Action == normalizedAction)
                .OrderByDescending(x => x.CreateTime)
                .ThenByDescending(x => x.Id)
                .Select(x => x.Result)
                .Take(50)
                .ToListAsync(cancellationToken);

            var count = 0;
            foreach (var result in recent)
            {
                if (UserActionLogTypes.IsFailure(result))
                    count++;
                else
                    break;
            }

            return count;
        }

        private static string? Trunc(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            return value.Length <= maxLength ? value : value[..maxLength];
        }
    }
}
