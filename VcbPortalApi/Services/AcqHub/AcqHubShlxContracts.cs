namespace VcbPortalApi.Services.AcqHub
{
    // ─────────────────────────────────────────────────────────────────────────
    // TÊN THUỘC TÍNH VIẾT THƯỜNG — có chủ ý.
    //
    // ToJsonString() dùng Newtonsoft và giữ nguyên tên thuộc tính, không đổi sang
    // camelCase. Đặt PascalCase rồi gắn [JsonPropertyName] KHÔNG cứu được: thuộc
    // tính đó của System.Text.Json, Newtonsoft bỏ qua — bản tin sẽ ra "MerchantId"
    // thay vì "merchantId" và ACQHUB không nhận ra khoá nào.
    //
    // AcqRequest cũng khai data/clientId/requestTime/checkSum viết thường vì lý do
    // y hệt.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Một terminal cần cài đặt. Tên trường khớp đúng những gì Angular gửi lên VÀ
    /// những gì ACQHUB nhận bên trong trường data — cố ý giống nhau để không phải
    /// ánh xạ lại. Đã đối chiếu với bản tin thật giải mã từ base64.
    /// </summary>
    public sealed class ShlxConfigItem
    {
        public string merchantId { get; set; } = "";
        public string terminalId { get; set; } = "";
        public string terminalName { get; set; } = "";
        public string ddAccountNumber { get; set; } = "";

        /// <summary>Mã chi nhánh 5 ký tự, ví dụ "01400". Frontend đã đệm số 0 đầu.</summary>
        public string branchCode { get; set; } = "";

        public string province { get; set; } = "";
    }

    /// <summary>
    /// Vừa là body Angular gửi lên, vừa là phần nằm trong trường data gửi sang
    /// ACQHUB — cùng một hình dạng.
    /// </summary>
    public sealed class ShlxConfigRequest
    {
        public List<ShlxConfigItem> items { get; set; } = [];
    }

    /// <summary>Kết quả của MỘT terminal. ACQHUB dùng snake_case ở phần này.</summary>
    public sealed class ShlxConfigResult
    {
        public string terminal_id { get; set; } = "";
        public long merchant_account_id { get; set; }
        public bool success { get; set; }
        public string result_code { get; set; } = "";
        public string result_message { get; set; } = "";
    }

    /// <summary>
    /// Response ACQHUB, trả NGUYÊN VẸN về cho Angular.
    ///
    /// code == "00" ở ngoài KHÔNG có nghĩa mọi terminal đều thành công — trạng thái
    /// thật của từng cái nằm ở results[].success. Bản tin thất bại quan sát được có
    /// code "01" và results rỗng, tức bị từ chối ở khâu xác thực phong bì.
    /// </summary>
    public sealed class ShlxConfigResponse
    {
        public string code { get; set; } = "";
        public string message { get; set; } = "";
        public string requestId { get; set; } = "";
        public string subCode { get; set; } = "";
        public string subMessage { get; set; } = "";
        public string serverTime { get; set; } = "";
        public string nodeOut { get; set; } = "";
        public string operation { get; set; } = "";
        public List<ShlxConfigResult> results { get; set; } = [];
    }
}
