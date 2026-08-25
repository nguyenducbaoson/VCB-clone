using System.ComponentModel.DataAnnotations;

namespace VcbPortalApi.Models.SSO
{
    /// <summary>
    /// Payload AppMerchant gửi sang BE sau khi nhận one time token SSO từ AppDigBank
    /// qua deep link (UC-05/UC-06).
    /// </summary>
    public sealed class MerchantSsoLoginRequest
    {
        /// <summary>One time token SSO nhận được tại bước redirect.</summary>
        [Required(ErrorMessage = "accessTokenSSO là bắt buộc")]
        [MaxLength(2000)]
        public string AccessTokenSSO { get; set; } = string.Empty;

        [Required(ErrorMessage = "client là bắt buộc")]
        public ClientContext Client { get; set; } = new();
    }

    /// <summary>Kết quả trả về AppMerchant khi đối chiếu SSO thành công.</summary>
    public sealed class MerchantSsoLoginResult
    {
        public string Username { get; init; } = string.Empty;

        // Giá trị lấy từ MP_APP_USERS (kiểu NUMBER), không phải giá trị SSO gửi sang.
        public decimal? Bid { get; init; }
        public decimal? Mid { get; init; }
        public decimal? Tid { get; init; }

        // decimal? cho khớp cột Oracle NUMBER — nhận được cả khi entity scaffold ra long?.
        public decimal? RoleId { get; init; }
        public decimal? BranchId { get; init; }

        /// <summary>CIF khách hàng do SSO xác nhận.</summary>
        public string? UserCif { get; init; }

        public string? UserFullName { get; init; }

        /// <summary>Mã liên kết Digibank – DigiMerchant, nếu SSO có trả trong othersInfo.</summary>
        public string? AccountLinkCode { get; init; }

        /// <summary>
        /// true  => AppMerchant hiển thị màn hình nhập mật khẩu (UC-05).
        /// false => vào thẳng trang chủ DigiMerchant (UC-06).
        /// Suy ra từ so DEVICEID trong MP_APP_USERS với deviceId client gửi lên (BR-08/BR-09).
        /// </summary>
        public bool RequirePassword { get; init; }

        /// <summary>Lý do phải nhập mật khẩu — phục vụ log/analytics phía client.</summary>
        public string? RequirePasswordReason { get; init; }
    }
}
