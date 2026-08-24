namespace VcbPortalApi.Services
{
    /// <summary>
    /// Cách so khớp BID/MID/TID giữa SSO và MP_APP_USERS.
    ///
    /// Vấn đề: SSO trả dạng "B001"/"M001"/"T001", còn cột Oracle là NUMBER.
    /// Không có cách so nào đúng cho mọi trường hợp cho tới khi BA/VCB xác nhận
    /// "B001" ứng với giá trị nào trong MP_APP_USERS.BID.
    /// </summary>
    public enum HierarchyCompareMode
    {
        /// <summary>Bỏ phần chữ, so phần số: "B001" → 1, khớp với BID = 1.</summary>
        DigitsOnly = 0,

        /// <summary>Parse nguyên chuỗi thành số. "B001" không parse được ⇒ luôn lệch.</summary>
        Numeric = 1,

        /// <summary>So chuỗi: "B001" vs BID.ToString(). Chỉ đúng nếu DB lưu đúng chuỗi đó.</summary>
        Exact = 2,

        /// <summary>
        /// Bỏ hẳn việc so BID/MID/TID, chỉ so username.
        /// Username mới là chốt định danh; BID/MID/TID là kiểm tra phụ.
        /// </summary>
        Skip = 3
    }

    public sealed class MpAuthOptions
    {
        public const string SectionName = "MpAuth";

        /// <summary>
        /// Bật: dùng cột DEVICEID trong MP_APP_USERS làm cờ "đã xác thực lần đầu" (BR-08/BR-09).
        /// Tắt: luôn yêu cầu nhập mật khẩu (an toàn nhất, mất trải nghiệm SSO tự động).
        /// </summary>
        public bool UseDeviceIdForFirstAuth { get; set; } = true;

        /// <summary>Xem HierarchyCompareMode. Phải đối chiếu dữ liệu UAT thật trước khi chốt.</summary>
        public HierarchyCompareMode HierarchyCompare { get; set; } = HierarchyCompareMode.DigitsOnly;
    }
}
