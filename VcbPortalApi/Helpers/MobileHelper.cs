using Microsoft.EntityFrameworkCore;
using VcbPortalApi.DbContext;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution thật đã có MobileHelper. ĐỪNG chép đè.
//
// LƯU Ý KHI TEST: hai hàm này là static, KHÔNG thay bằng fake được. Muốn điều
// khiển chúng trả true/false thì phải seed dữ liệu vào MerchantContext.
// Nếu ở solution thật chúng dùng Dapper/SQL thô thay vì EF thì EF InMemory không
// chạy được — khi đó bỏ 2 test nhánh "không thuộc bid/mid" và ghi chú lý do.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Helpers
{
    public static class MobileHelper
    {
        /// <summary>mid thuộc bid VÀ tid thuộc mid.</summary>
        public static Task<bool> IsMidTidUnderBidAsync(
            MerchantContext context, decimal bid, decimal mid, decimal tid,
            CancellationToken cancellationToken = default) =>
            context.MpTerminals.AsNoTracking()
                .AnyAsync(x => x.Bid == bid && x.Mid == mid && x.Tid == tid, cancellationToken);

        /// <summary>tid thuộc mid.</summary>
        public static Task<bool> IsTidUnderMidAsync(
            MerchantContext context, decimal mid, decimal tid,
            CancellationToken cancellationToken = default) =>
            context.MpTerminals.AsNoTracking()
                .AnyAsync(x => x.Mid == mid && x.Tid == tid, cancellationToken);
    }
}
