using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VcbPortalApi.Controllers.Mobile;
using VcbPortalApi.DbContext.Oracle;
using VcbPortalApi.Models.MobileApp;
using VcbPortalApi.Models.MP.User;
using VcbPortalApi.Models.TwoFa;
using VcbPortalApi.Services;
using VcbPortalApi.UnitTests.Fixtures;
using VcbPortalApi.UnitTests.Helpers;

namespace VcbPortalApi.UnitTests.Controllers.Mobile
{
    /// <summary>
    /// Controller chỉ có một việc: ánh xạ kết quả tầng dưới sang response. Logic vô
    /// hiệu hoá user nằm ở MobileHelper và đã có test riêng — ở đây khoá phần ánh xạ,
    /// theo đúng hợp đồng của <see cref="MobileApiError"/>: mỗi nhánh một cặp
    /// (code, HTTP status).
    /// </summary>
    public class MobileControllerTests
    {
        private readonly FrontendContext _context = TestDb.Create<FrontendContext>();
        private readonly MerchantContext _merchantContext = TestDb.Create<MerchantContext>();

        /// <summary>
        /// DÙNG Options.Create, ĐỪNG Mock&lt;IOptions&lt;T&gt;&gt;: constructor TwoFaService
        /// làm <c>_options = options.Value;</c> ngay dòng đầu, mà mock chưa Setup thì
        /// <c>.Value</c> trả null. Enabled để false — luồng Deactive không dùng 2FA.
        /// </summary>
        private readonly IOptions<TwoFaOptions> _twoFaOptions = Options.Create(new TwoFaOptions());
        private readonly IOptions<SmsNotifyOptions> _smsOptions = Options.Create(new SmsNotifyOptions());
        private readonly IHttpClientFactory _httpClientFactory = new Mock<IHttpClientFactory>().Object;

        private MobileController CreateController(string? userName = TestDataHelper.DefaultUserName) =>
            new(_context,
                _merchantContext,
                new UserAppConfigService(_context, _merchantContext),
                new TwoFaService(_twoFaOptions, _smsOptions, _httpClientFactory))
            {
                ControllerContext = TestHttpContext.Build(userName)
            };

        private MpUserCommon? Reload() =>
            _context.MpUserCommons.AsNoTracking()
                    .FirstOrDefault(x => x.UserName == TestDataHelper.DefaultUserName);

        // ══ Deactive ════════════════════════════════════════════════════════════

        /// <summary>Vô hiệu hoá được thì trạng thái user chuyển "D".</summary>
        [Fact]
        public async Task Deactive_WhenDeactivationSucceeds_MarksUserDeactivated()
        {
            _context.Seed(TestDataHelper.CreateUsersCommon(status: "A"));

            var result = await CreateController().Deactive();

            result.Should().BeOfType<OkResult>();
            Reload()!.Status.Should().Be("D");
        }

        /// <summary>Vô hiệu hoá còn xoá dấu vết thiết bị, để cài lại app không tự vào được.</summary>
        [Fact]
        public async Task Deactive_WhenUserHasDevice_ClearsDeviceTokens()
        {
            _context.Seed(TestDataHelper.CreateUsersCommon());
            _context.Seed(TestDataHelper.CreateAppUser());

            await CreateController().Deactive();

            var appUser = _context.MpAppUsers.AsNoTracking()
                                  .First(x => x.UserName == TestDataHelper.DefaultUserName);

            appUser.FcmToken.Should().BeNull();
            appUser.Fid.Should().BeNull();
            appUser.DeviceId.Should().BeNull();
        }

        /// <summary>Gọi lại lần hai vẫn thành công — không có lỗi "đã vô hiệu hoá rồi".</summary>
        [Fact]
        public async Task Deactive_WhenAlreadyDeactivated_StillSucceeds()
        {
            _context.Seed(TestDataHelper.CreateUsersCommon(status: "D"));

            var result = await CreateController().Deactive();

            result.Should().BeOfType<OkResult>();
            Reload()!.Status.Should().Be("D");
        }

        /// <summary>Không có user trong DB → lỗi nghiệp vụ 01 / HTTP 400.</summary>
        [Fact]
        public async Task Deactive_WhenUserNotFound_ReturnsBaseError()
        {
            var result = await CreateController().Deactive();

            result.ShouldBeError(MobileApiError.CodeBaseError, HttpStatusCode.BadRequest);
        }

        /// <summary>
        /// Token không mang claim username: vẫn chỉ trả 01 chứ KHÔNG phải 02
        /// (Unauthorized). Nhìn từ phía app, "chưa đăng nhập" và "không tìm thấy user"
        /// là cùng một mã — muốn phân biệt thì phải sửa controller.
        /// </summary>
        [Fact]
        public async Task Deactive_WhenNoIdentity_ReturnsBaseErrorNotUnauthorized()
        {
            _context.Seed(TestDataHelper.CreateUsersCommon());

            var result = await CreateController(userName: null).Deactive();

            result.ShouldBeError(MobileApiError.CodeBaseError);
            result.ShouldBeApiResponse().ReadField("code")
                  .Should().NotBe(MobileApiError.CodeUnauthorized);
        }

        /// <summary>
        /// DB hỏng thì KHÔNG để chi tiết exception lọt ra client. Dựng tình huống
        /// bằng cách dispose context trước khi gọi.
        ///
        /// CỐ Ý KHÔNG khẳng định mã lỗi: nhánh này đi qua
        /// <c>new ErrorMessage(ex).Simplify()</c>, mà ErrorMessage là file tôi chưa
        /// có ảnh — body của nó không chắc có trường <c>code</c>. Điều tên test hứa
        /// là "không rò rỉ", và đó là thứ được khoá ở đây. Có ảnh ErrorMessage rồi
        /// thì siết thêm mã lỗi và status cụ thể.
        /// </summary>
        [Fact]
        public async Task Deactive_WhenDatabaseThrows_ReturnsErrorWithoutLeakingDetails()
        {
            _context.Seed(TestDataHelper.CreateUsersCommon());
            var controller = CreateController();
            _context.Dispose();

            var result = await controller.Deactive();

            var response = result.ShouldBeApiResponse();

            response.StatusCode.Should().NotBe((int)HttpStatusCode.OK, "hong DB thi khong duoc bao thanh cong");

            var body = response.Value!.ToString();
            body.Should().NotContain("Disposed", "khong duoc lo chi tiet loi ra client");
            body.Should().NotContain("ObjectDisposedException");
            body.Should().NotContain("MpUserCommons", "khong duoc lo ten bang ra client");
        }

        /// <summary>
        /// GHI LẠI CHỖ LỆCH HỢP ĐỒNG: mọi nhánh lỗi trả body { code, message }, nhưng
        /// nhánh thành công trả <c>Ok()</c> — HTTP 200 với body RỖNG. App phải xử lý
        /// hai khuôn khác nhau. Đổi sang MobileApiError.BaseSuccess() thì nhất quán,
        /// và test này đỏ để nhắc sửa cả phía app.
        /// </summary>
        [Fact]
        public async Task Deactive_WhenSucceeds_ReturnsEmptyBodyNotStandardSuccessShape()
        {
            _context.Seed(TestDataHelper.CreateUsersCommon());

            var result = await CreateController().Deactive();

            result.Should().BeOfType<OkResult>("chua theo khuon { code, message }");
            result.Should().NotBeAssignableTo<ObjectResult>();
        }

        // ══ GetMinAppVersion ════════════════════════════════════════════════════

        /// <summary>Trả phiên bản tối thiểu lấy từ cấu hình.</summary>
        [Fact]
        public async Task GetMinAppVersion_ReturnsVersionFromConfig()
        {
            var result = await CreateController().GetMinAppVersion(CancellationToken.None);

            var body = result.Should().BeOfType<OkObjectResult>().Subject
                             .Value.Should().BeOfType<UserAppMinVersionDto>().Subject;

            body.MinVersion.Should().NotBeNullOrEmpty();
        }

        /// <summary>Không có token vẫn gọi được — action mang [AllowAnonymous].</summary>
        [Fact]
        public async Task GetMinAppVersion_WorksWithoutIdentity()
        {
            var result = await CreateController(userName: null).GetMinAppVersion(CancellationToken.None);

            result.Should().BeOfType<OkObjectResult>();
        }
    }
}
