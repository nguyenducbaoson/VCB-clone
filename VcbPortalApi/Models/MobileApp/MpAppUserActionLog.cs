// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution thật đã có entity này. ĐỪNG chép đè.
// Dựng lại đủ các cột mà UserActionLogHelper.Insert ghi vào, kèm độ dài cột suy
// từ chính các lời gọi Trunc(...) trong hàm đó.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Models.MobileApp
{
    /// <summary>MP_APP_USER_ACTION_LOG</summary>
    public class MpAppUserActionLog
    {
        public long Id { get; set; }
        public DateTime CreateTime { get; set; }

        /// <summary>Trunc 100</summary>
        public string? UserName { get; set; }

        /// <summary>Trunc 50, bắt buộc</summary>
        public string Action { get; set; } = null!;

        /// <summary>Tối đa 10 ký tự — Insert ném ArgumentException nếu dài hơn.</summary>
        public string Result { get; set; } = null!;

        /// <summary>Trunc 500</summary>
        public string? Message { get; set; }

        /// <summary>Trunc 2000</summary>
        public string? ExtraData { get; set; }

        /// <summary>Trunc 100</summary>
        public string? RequestIp { get; set; }

        /// <summary>Trunc 20</summary>
        public string? Source { get; set; }
    }
}
