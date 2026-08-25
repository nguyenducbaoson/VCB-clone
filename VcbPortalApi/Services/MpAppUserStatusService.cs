using Microsoft.EntityFrameworkCore;
using VcbPortalApi.DbContext;
using VcbPortalApi.Models.SSO;

namespace VcbPortalApi.Services
{
    /// <summary>
    /// Giá trị lưu ở MP_APP_USERS.PHONEPOS_STATUS / VISAACCEPT_STATUS.
    /// "Chưa đăng ký" là NULL, không phải một con số.
    /// </summary>
    public static class MpAppUserStatus
    {
        public const decimal DaDangKy = 0;
        public const decimal KichHoat = 2;
        public const decimal Huy = 7;
        public static readonly decimal? ChuaDangKy = null;
    }

    /// <summary>Giá trị cột PARTNER trong MP_APP_PARTNER_CARD_REG.</summary>
    public static class MpPartner
    {
        public const string PhonePos = "PHONEPOS";
        public const string VisaAccept = "VISAACCEPT";
    }

    public interface IMpAppUserStatusService
    {
        /// <summary>
        /// Đọc TẤT CẢ bản ghi của user trong MP_APP_PARTNER_CARD_REG (một username có thể có
        /// nhiều dòng, trải trên nhiều partner), suy ra trạng thái tổng hợp cho từng partner,
        /// rồi cập nhật MP_APP_USERS.PHONEPOS_STATUS và VISAACCEPT_STATUS.
        ///
        /// Chỉ SaveChanges khi giá trị thật sự đổi. Không tìm thấy user trong MP_APP_USERS
        /// thì ghi log cảnh báo và bỏ qua, không ném exception.
        /// </summary>
        Task RefreshStatusAsync(string username, CancellationToken ct = default);
    }

    public sealed class MpAppUserStatusService : IMpAppUserStatusService
    {
        // TODO(DbContext): đổi thành tên DbContext thật của solution.
        private readonly VcbPortalDbContext _db;
        private readonly ILogger<MpAppUserStatusService> _logger;

        public MpAppUserStatusService(VcbPortalDbContext db, ILogger<MpAppUserStatusService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task RefreshStatusAsync(string username, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                _logger.LogWarning("RefreshStatusAsync nhận username rỗng, bỏ qua");
                return;
            }

            var normalized = username.Trim().ToUpperInvariant();

            // Lấy TẤT CẢ bản ghi của user. Một username có nhiều dòng là bình thường —
            // ví dụ VATID001 có 3 dòng VISAACCEPT (3, 3, 0) và 1 dòng PHONEPOS (2).
            // Chỉ cần 2 cột nên projection cho nhẹ.
            var registrations = await _db.Set<MpAppPartnerCardReg>()
                .AsNoTracking()
                .Where(r => r.UserName != null && r.UserName.ToUpper() == normalized)
                .Select(r => new { r.Partner, r.Status })
                .ToListAsync(ct);

            var user = await _db.Set<MpAppUser>()
                .FirstOrDefaultAsync(u => u.UserName != null && u.UserName.ToUpper() == normalized, ct);

            if (user is null)
            {
                _logger.LogWarning(
                    "Không tìm thấy {UserName} trong MP_APP_USERS, bỏ qua cập nhật trạng thái", username);
                return;
            }

            var rows = registrations.Select(r => (r.Partner, r.Status)).ToList();

            // Chẩn đoán: in đúng những dòng service đọc được. Cả 2 cột cùng ra một giá trị
            // thường là do khâu GHI đẻ ra dòng cho cả hai partner, không phải khâu tính.
            _logger.LogDebug("MP_APP_PARTNER_CARD_REG của {UserName}: {Rows}",
                username,
                rows.Count == 0
                    ? "(không có dòng nào)"
                    : string.Join(" | ", rows.Select(r => $"{r.Partner}={r.Status}")));

            var phonepos = ResolveForPartner(rows, MpPartner.PhonePos);
            var visaaccept = ResolveForPartner(rows, MpPartner.VisaAccept);

            if (user.PhoneposStatus == phonepos && user.VisaacceptStatus == visaaccept)
                return;

            _logger.LogInformation(
                "Cập nhật trạng thái {UserName}: PHONEPOS {OldPhonepos}->{NewPhonepos}, " +
                "VISAACCEPT {OldVisa}->{NewVisa}",
                user.UserName, user.PhoneposStatus, phonepos, user.VisaacceptStatus, visaaccept);

            user.PhoneposStatus = phonepos;
            user.VisaacceptStatus = visaaccept;

            // KHÔNG đụng FINONE_STATUS: chưa có bản ghi PARTNER nào cho FinOne, tính ra
            // sẽ là null và ghi đè mất giá trị do luồng khác quản lý.
            await _db.SaveChangesAsync(ct);
        }

        /// <summary>
        /// Lọc các bản ghi thuộc một partner rồi suy ra trạng thái. Hàm thuần, tách riêng để
        /// test và để báo cáo dùng lại mà không cần DbContext.
        /// </summary>
        public static decimal? ResolveForPartner(
            IEnumerable<(string? Partner, string? Status)> rows, string partner)
        {
            var statuses = rows
                .Where(r => string.Equals(r.Partner?.Trim(), partner, StringComparison.OrdinalIgnoreCase))
                .Select(r => ParseStatus(r.Status))
                .ToList();

            return ResolveStatus(statuses);
        }

        /// <summary>
        /// STATUS là VARCHAR2(2) nên phải parse. Giá trị không phải số trả về null và sẽ
        /// rơi vào nhánh "các trường hợp còn lại" thay vì âm thầm khớp nhầm một quy tắc.
        /// </summary>
        private static int? ParseStatus(string? raw) =>
            int.TryParse(raw?.Trim(), out var parsed) ? parsed : null;

        /// <summary>
        /// THỨ TỰ HAI NHÁNH CUỐI LÀ BẮT BUỘC: "toàn 7" phải xét TRƯỚC "toàn 0/7".
        /// Tập chỉ có 7 thoả cả hai điều kiện; đảo lại thì user đã hủy sẽ ra "Đã đăng ký".
        ///
        /// Nhánh 2..6 thì đặt đâu cũng được — nó loại trừ nhau với hai nhánh kia (đã có
        /// giá trị trong 2..6 thì không thể "toàn 7" hay "toàn 0/7"). Để lên đầu cho khớp
        /// thứ tự bảng đặc tả, không phải vì bắt buộc.
        ///
        ///   Có ít nhất 1 bản ghi trạng thái 2/3/4/5/6  -> Kích hoạt    (2)
        ///   Tất cả bản ghi trạng thái 7                -> Hủy          (7)
        ///   Tất cả bản ghi trạng thái 0, hoặc 0 và 7   -> Đã đăng ký   (0)
        ///   Còn lại (kể cả không có bản ghi nào)       -> Chưa đăng ký (null)
        /// </summary>
        public static decimal? ResolveStatus(IReadOnlyCollection<int?> statuses)
        {
            if (statuses.Count == 0) return MpAppUserStatus.ChuaDangKy;

            if (statuses.Any(s => s is >= 2 and <= 6)) return MpAppUserStatus.KichHoat;

            if (statuses.All(s => s == 7)) return MpAppUserStatus.Huy;

            // Tới đây chắc chắn không phải toàn 7, nên "toàn {0,7}" đồng nghĩa có ít nhất một 0.
            if (statuses.All(s => s is 0 or 7)) return MpAppUserStatus.DaDangKy;

            return MpAppUserStatus.ChuaDangKy;
        }
    }
}
