namespace VcbPortalApi.Services.Sso
{
    /// <summary>
    /// Cấu hình riêng cho luồng SSO DigiMerchant.
    /// TODO: nếu solution đã có SSO base url / appId trong AppSettings thì dùng lại.
    /// </summary>
    public sealed class MpSsoOptions
    {
        public const string SectionName = "MpSso";

        public string BaseUrl { get; set; } = string.Empty;

        /// <summary>App ID của DigiMerchant do VCB cấp.</summary>
        public string AppId { get; set; } = string.Empty;

        public string ValidateAccessTokenPath { get; set; } = "/TokenManager/ValidateAccessToken";

        public int TimeoutSeconds { get; set; } = 15;

        /// <summary>
        /// Verify chữ ký response.
        ///
        /// LƯU Ý: response trả header/payload dạng object nên chữ ký phải tính lại trên chuỗi
        /// JSON serialize lại — thứ tự key hoặc khoảng trắng khác đi là chữ ký lệch dù bản tin
        /// hợp lệ. Nếu UAT báo lệch liên tục, xem log "chữ ký response không khớp" để so
        /// expected/actual, rồi hỏi VCB chính xác chuỗi nào được ký.
        /// </summary>
        public bool VerifyResponseSignature { get; set; } = true;

        // ---- Tên field trong othersInfo ----
        // Đã xác nhận bằng response thật: bid, mid, tid, userDes.
        // Giữ thêm vài biến thể phòng khi môi trường khác đặt khác.

        public string[] UsernameFieldNames { get; set; } = { "userDes", "username", "userName" };
        public string[] BidFieldNames { get; set; } = { "bid" };
        public string[] MidFieldNames { get; set; } = { "mid" };
        public string[] TidFieldNames { get; set; } = { "tid" };

        /// <summary>Không có trong response mẫu, chỉ dùng để ghi log nếu VCB có trả.</summary>
        public string[] AccountLinkCodeFieldNames { get; set; } = { "accountLinkCode", "linkCode" };
    }
}
