namespace VcbPortalApi.Services.Sso
{
    public enum MpSsoFailureKind
    {
        None,
        InvalidToken,
        InvalidClientIp,
        Timeout,
        Other
    }

    /// <summary>
    /// Kết quả ValidateAccessToken đã chuẩn hoá. Tầng nghiệp vụ chỉ làm việc với model này.
    /// </summary>
    public sealed class MpSsoVerifyResult
    {
        public bool IsValid { get; init; }
        public MpSsoFailureKind Failure { get; init; } = MpSsoFailureKind.None;

        /// <summary>resCode/resMessage thô từ SSO, phục vụ log và tra soát.</summary>
        public int ResCode { get; init; }
        public string? ResMessage { get; init; }

        public MpSsoUserInfo? User { get; init; }

        public static MpSsoVerifyResult Fail(MpSsoFailureKind kind, int resCode, string? resMessage) =>
            new() { IsValid = false, Failure = kind, ResCode = resCode, ResMessage = resMessage };
    }

    /// <summary>Thông tin bóc từ payload của ValidateAccessToken.</summary>
    public sealed class MpSsoUserInfo
    {
        /// <summary>userId — định danh phía Digibank (response mẫu trả số điện thoại).</summary>
        public string? SourceUserId { get; init; }

        public string? UserFullName { get; init; }
        public string? UserRole { get; init; }

        /// <summary>userOf — hệ thống đích, response mẫu trả "DIGI_MERCHANT".</summary>
        public string? UserOf { get; init; }

        public string? UserCif { get; init; }

        /// <summary>othersInfo.userDes — username DigiMerchant, đem đi tra MP_APP_USERS.</summary>
        public string? MerchantUsername { get; init; }

        /// <summary>othersInfo.bid — response mẫu trả dạng "B001", KHÔNG phải số thuần.</summary>
        public string? Bid { get; init; }
        public string? Mid { get; init; }
        public string? Tid { get; init; }

        public string? AccountLinkCode { get; init; }

        /// <summary>Toàn bộ othersInfo dạng chuỗi, để log khi dò sai key.</summary>
        public IReadOnlyDictionary<string, string?> OthersInfoRaw { get; init; }
            = new Dictionary<string, string?>();
    }
}
