using VcbPortalApi.StaticData.MP;

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

    // ─────────────────────────────────────────────────────────────────────────
    // HAI HÌNH DẠNG KHÁC NHAU — CỐ Ý.
    //
    //   ShlxUploadItem = một dòng Excel: 6 cột ACQHUB + 5 cột chỉ dùng để tạo user
    //                    portal (USER_NAME, ROLE_ID, FULLNAME, EMAIL, MOBILE).
    //   ShlxConfigItem = đúng những gì ACQHUB khai, không hơn một khoá nào.
    //
    // ĐỪNG GỘP LÀM MỘT. ShlxConfigItem bị ToJsonString() rồi base64 vào trường
    // `data`, nên mọi thuộc tính thêm vào nó đều bay sang ACQHUB. Gửi khoá họ
    // không khai thì hoặc bị bỏ qua, hoặc bị từ chối cả lô — và cả hai chỉ lộ ra
    // khi đã chạy thật. Đây cũng đúng lý do luồng này không gọi AddAuditData.
    //
    // Hệ quả thứ hai, quan trọng hơn: EMAIL và MOBILE là dữ liệu cá nhân. Tách
    // kiểu ở đây là thứ bảo đảm chúng không rời khỏi hệ thống VCB.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Một dòng trong file Excel người dùng tải lên.</summary>
    public sealed class ShlxUploadItem
    {
        // ── 6 cột gửi sang ACQHUB ────────────────────────────────────────────
        public string merchantId { get; set; } = "";
        public string terminalId { get; set; } = "";
        public string terminalName { get; set; } = "";
        public string ddAccountNumber { get; set; } = "";

        /// <summary>Mã chi nhánh 5 ký tự, ví dụ "01400". Frontend đã đệm số 0 đầu.</summary>
        public string branchCode { get; set; } = "";

        public string province { get; set; } = "";

        // ── 5 cột CHỈ dùng để tạo user portal, không rời khỏi hệ thống ───────

        /// <summary>USER_NAME. Bắt buộc — thiếu thì không tạo được user.</summary>
        public string? userName { get; set; }

        /// <summary>ROLE_ID. Bỏ trống thì mặc định <see cref="Roles.RoleShlx"/> (28).</summary>
        public decimal? roleId { get; set; }

        /// <summary>FULLNAME. Bỏ trống thì lấy chính userName, đúng như ApiCommon.CreateUsers.</summary>
        public string? fullName { get; set; }

        public string? email { get; set; }
        public string? mobile { get; set; }

        public ShlxConfigItem ToConfigItem() => new()
        {
            merchantId = merchantId,
            terminalId = terminalId,
            terminalName = terminalName,
            ddAccountNumber = ddAccountNumber,
            branchCode = branchCode,
            province = province,
        };
    }

    /// <summary>Body Angular gửi lên — KHÁC với phần nằm trong trường data gửi đi.</summary>
    public sealed class ShlxUploadRequest
    {
        public List<ShlxUploadItem> items { get; set; } = [];
    }

    /// <summary>
    /// Một terminal cần cài đặt. Tên trường khớp đúng những gì ACQHUB nhận bên
    /// trong trường data. Đã đối chiếu với bản tin thật giải mã từ base64.
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

    /// <summary>Phần nằm trong trường data gửi sang ACQHUB.</summary>
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
    /// Kết quả tạo user portal của MỘT terminal. KHÔNG đến từ ACQHUB — portal tự
    /// dựng sau khi ACQHUB đã cài xong terminal đó.
    /// </summary>
    public sealed class ShlxUserResult
    {
        public string terminal_id { get; set; } = "";
        public string user_name { get; set; } = "";

        /// <summary>true = vừa tạo mới. false = đã có sẵn, hoặc không tạo được.</summary>
        public bool created { get; set; }

        /// <summary>Lý do hiện ở cột LỖI khi created == false.</summary>
        public string message { get; set; } = "";
    }

    /// <summary>
    /// Response ACQHUB, cộng thêm kết quả tạo user do portal tự điền.
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

        /// <summary>
        /// KHÔNG PHẢI CỦA ACQHUB — portal tự điền sau khi tạo user. ACQHUB không
        /// gửi khoá này nên lúc giải mã response nó luôn rỗng; controller gán vào
        /// ngay trước khi trả cho Angular.
        /// </summary>
        public List<ShlxUserResult> users { get; set; } = [];
    }
}
