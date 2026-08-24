namespace VcbPortalApi.Models.SSO
{
    /// <summary>
    /// TODO: solution nhiều khả năng đã có wrapper response chuẩn (xem các controller
    /// AcqAdviceController / MerchantController). Nếu có thì DÙNG CÁI ĐÓ và xoá class này —
    /// mobile không nên phải parse hai khuôn response khác nhau trong cùng một app.
    /// </summary>
    public sealed class ApiResponse<T>
    {
        public string Code { get; init; } = MpSsoResultCode.Success;
        public string Message { get; init; } = "Success";
        public T? Data { get; init; }

        public static ApiResponse<T> Ok(T data, string message = "Success") =>
            new() { Code = MpSsoResultCode.Success, Message = message, Data = data };

        public static ApiResponse<T> Fail(string code, string message) =>
            new() { Code = code, Message = message, Data = default };
    }

    /// <summary>
    /// Mã kết quả BE trả về cho AppMerchant. Độc lập với resCode của VCB SSO
    /// để client không phụ thuộc bảng mã của hệ thống bên thứ ba.
    /// </summary>
    public static class MpSsoResultCode
    {
        public const string Success = "00";
        public const string InvalidRequest = "01";

        // Map từ resCode của VCB SSO
        public const string SsoTokenInvalid = "10";      // resCode 10
        public const string SsoClientIpInvalid = "11";   // resCode 12
        public const string SsoUnavailable = "12";       // resCode 99-199, timeout, lỗi kết nối
        public const string SsoResponseMalformed = "13"; // sai định dạng hoặc chữ ký không hợp lệ

        // Đối chiếu MP_APP_USERS
        public const string UserNotFound = "20";
        public const string UsernameMismatch = "22";
        public const string HierarchyMismatch = "23";

        public const string DeviceUpdateFailed = "30";
        public const string SystemError = "99";
    }
}
