using VcbPortalApi.Models.SSO;
using VcbPortalApi.Services;

namespace Tests.TestSupport
{
    /// <summary>
    /// Fake tự viết cho IMpSsoAuthService.
    ///
    /// Không dùng thư viện mock (Moq/NSubstitute) cho base này: một class chục dòng
    /// dễ đọc hơn cú pháp mock, và không thêm phụ thuộc phải xin duyệt ở project thật.
    ///
    /// Fake làm hai việc: trả về thứ mình dặn trước, và GHI LẠI đầu vào nhận được
    /// để test kiểm tra controller có truyền đúng dữ liệu xuống service không.
    /// </summary>
    public sealed class FakeMpSsoAuthService : IMpSsoAuthService
    {
        /// <summary>Kết quả sẽ trả về. Đặt trước khi gọi.</summary>
        public ApiResponse<MerchantSsoLoginResult> KetQuaTraVe { get; set; } =
            ApiResponse<MerchantSsoLoginResult>.Ok(new MerchantSsoLoginResult { Username = "VATID001" });

        /// <summary>Gán khác null thì service sẽ ném exception này, để test nhánh lỗi.</summary>
        public Exception? NemException { get; set; }

        /// <summary>Request mà controller đã truyền xuống. Null nghĩa là chưa được gọi.</summary>
        public MerchantSsoLoginRequest? RequestNhanDuoc { get; private set; }

        public int SoLanGoi { get; private set; }

        public Task<ApiResponse<MerchantSsoLoginResult>> AuthenticateAsync(
            MerchantSsoLoginRequest request, CancellationToken ct = default)
        {
            SoLanGoi++;
            RequestNhanDuoc = request;

            if (NemException is not null) throw NemException;

            return Task.FromResult(KetQuaTraVe);
        }
    }
}
