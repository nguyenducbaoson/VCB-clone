using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using VcbPortalApi.Controllers.Mobile;
using VcbPortalApi.DbContext;
using VcbPortalApi.Models.MP;
using VcbPortalApi.Services;
using VcbPortalApi.StaticData.MP;
using VcbPortalApi.UnitTests.Fixtures;
using VcbPortalApi.UnitTests.Helpers;

namespace VcbPortalApi.UnitTests.Controllers.Mobile
{
    /// <summary>
    /// POST /ma/partner/token — <see cref="MobilePartnerController.IssueSsoToken"/>.
    ///
    /// Toàn bộ logic nằm trong action, không tách service, nên test đánh thẳng vào
    /// action. Mỗi lệnh <c>return</c> sớm trong action là một test ở đây.
    ///
    /// NGUYÊN TẮC ARRANGE: mọi test nhánh lỗi đều dựng bối cảnh HỢP LỆ HOÀN TOÀN rồi
    /// chỉ làm hỏng đúng một thứ. Nhờ vậy test fail thì chắc chắn do nhánh đó, không
    /// phải do quên seed dữ liệu chỗ khác.
    /// </summary>
    public class MobilePartnerControllerTests
    {
        private readonly FrontendContext _frontend = TestDb.Create<FrontendContext>();
        private readonly MerchantContext _merchant = TestDb.Create<MerchantContext>();

        private MobilePartnerController CreateController(ControllerContext? context = null) =>
            new(_frontend,
                _merchant,
                new MpAppUserStatusService(TestDb.Create<VcbPortalDbContext>(),
                                           NullLogger<MpAppUserStatusService>.Instance))
            {
                ControllerContext = context ?? TestHttpContext.Build()
            };

        /// <summary>Bối cảnh đầy đủ, hợp lệ cho một user với role cho trước.</summary>
        private void GivenValidUserWithRole(decimal roleId)
        {
            _frontend.Seed(TestDataHelper.CreateSession());
            _frontend.Seed(TestDataHelper.CreateUsersCommon(roleId: roleId));
            _frontend.Seed(TestDataHelper.CreateAppUser());
            _merchant.Seed(TestDataHelper.CreateTerminal());
        }

        // ── 1. Đầu vào sai ──────────────────────────────────────────────────────

        [Fact]
        public async Task IssueSsoToken_WhenModelStateInvalid_ReturnsInvalidParameters()
        {
            // Arrange
            GivenValidUserWithRole(Roles.RoleMid);
            var controller = CreateController();
            controller.ModelState.AddModelError(nameof(PartnerSsoTokenForm.PartnerCode), "bat buoc");

            // Act
            var result = await controller.IssueSsoToken(TestDataHelper.CreatePartnerTokenForm());

            // Assert
            result.ShouldBeError("InvalidParameters");
        }

        [Fact]
        public async Task IssueSsoToken_WhenNoIdentity_ReturnsUnauthorized()
        {
            // Arrange
            GivenValidUserWithRole(Roles.RoleMid);
            var controller = CreateController(TestHttpContext.Build(userName: null));

            // Act
            var result = await controller.IssueSsoToken(TestDataHelper.CreatePartnerTokenForm());

            // Assert
            result.ShouldBeUnauthorized();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task IssueSsoToken_WhenPartnerCodeEmpty_ReturnsPartnerCodeEmpty(string? partnerCode)
        {
            // Arrange
            GivenValidUserWithRole(Roles.RoleMid);
            var controller = CreateController();
            var form = TestDataHelper.CreatePartnerTokenForm(partnerCode: partnerCode);

            // Act
            var result = await controller.IssueSsoToken(form);

            // Assert
            result.ShouldBeError("PartnerCodeEmpty");
        }

        [Fact]
        public async Task IssueSsoToken_WhenNoAuthorizationHeader_ReturnsUnauthorized()
        {
            // Arrange
            GivenValidUserWithRole(Roles.RoleMid);
            var controller = CreateController(TestHttpContext.Build(coBearerToken: false));

            // Act
            var result = await controller.IssueSsoToken(TestDataHelper.CreatePartnerTokenForm());

            // Assert
            result.ShouldBeUnauthorized();
        }

        [Fact]
        public async Task IssueSsoToken_WhenBearerTokenExpired_ReturnsUnauthorized()
        {
            // Arrange
            GivenValidUserWithRole(Roles.RoleMid);
            var controller = CreateController(
                TestHttpContext.Build(tokenExpiresUtc: DateTime.UtcNow.AddMinutes(-1)));

            // Act
            var result = await controller.IssueSsoToken(TestDataHelper.CreatePartnerTokenForm());

            // Assert
            result.ShouldBeUnauthorized();
        }

        // ── 2. Dữ liệu trong DB thiếu ───────────────────────────────────────────

        [Fact]
        public async Task IssueSsoToken_WhenSessionMissing_ReturnsBaseError()
        {
            // Arrange — co tinh KHONG seed session
            _frontend.Seed(TestDataHelper.CreateUsersCommon());
            _frontend.Seed(TestDataHelper.CreateAppUser());
            var controller = CreateController();

            // Act
            var result = await controller.IssueSsoToken(TestDataHelper.CreatePartnerTokenForm());

            // Assert
            result.ShouldBeError("BaseError");
        }

        [Fact]
        public async Task IssueSsoToken_WhenSessionIdBlank_ReturnsBaseError()
        {
            // Arrange
            _frontend.Seed(TestDataHelper.CreateSession(sessionId: "   "));
            _frontend.Seed(TestDataHelper.CreateUsersCommon());
            _frontend.Seed(TestDataHelper.CreateAppUser());
            var controller = CreateController();

            // Act
            var result = await controller.IssueSsoToken(TestDataHelper.CreatePartnerTokenForm());

            // Assert
            result.ShouldBeError("BaseError");
        }

        [Fact]
        public async Task IssueSsoToken_WhenEmailBlank_ReturnsBaseError()
        {
            // Arrange
            _frontend.Seed(TestDataHelper.CreateSession());
            _frontend.Seed(TestDataHelper.CreateUsersCommon(email: null));
            _frontend.Seed(TestDataHelper.CreateAppUser());
            var controller = CreateController();

            // Act
            var result = await controller.IssueSsoToken(TestDataHelper.CreatePartnerTokenForm());

            // Assert
            result.ShouldBeError("BaseError");
        }

        // ── 3. Nhánh user role BID ──────────────────────────────────────────────

        [Theory]
        [InlineData(null, 40000001d)]         // thieu mid
        [InlineData(68100000000097d, null)]   // thieu tid
        [InlineData(0d, 40000001d)]           // mid = 0
        [InlineData(null, null)]              // thieu ca hai
        public async Task IssueSsoToken_WhenRoleBidMissingMidOrTid_ReturnsMidOrMidEmptyUserBid(
            double? mid, double? tid)
        {
            // Arrange
            GivenValidUserWithRole(Roles.RoleBid);
            var controller = CreateController();
            var form = TestDataHelper.CreatePartnerTokenForm(
                mid: mid is null ? null : (decimal)mid.Value,
                tid: tid is null ? null : (decimal)tid.Value);

            // Act
            var result = await controller.IssueSsoToken(form);

            // Assert
            result.ShouldBeError("MidOrMidEmptyUserBid");
        }

        [Fact]
        public async Task IssueSsoToken_WhenRoleBidAndUserHasNoBid_ReturnsUserBidInvalid()
        {
            // Arrange
            _frontend.Seed(TestDataHelper.CreateSession());
            _frontend.Seed(TestDataHelper.CreateUsersCommon(roleId: Roles.RoleBid));
            _frontend.Seed(TestDataHelper.CreateAppUser(bid: null));   // ← thieu BID
            _merchant.Seed(TestDataHelper.CreateTerminal());
            var controller = CreateController();

            // Act
            var result = await controller.IssueSsoToken(TestDataHelper.CreatePartnerTokenForm());

            // Assert
            result.ShouldBeError("UserBidInvalid");
        }

        [Fact]
        public async Task IssueSsoToken_WhenRoleBidAndMidTidNotUnderBid_ReturnsMidOrTidNotExistUserBid()
        {
            // Arrange
            GivenValidUserWithRole(Roles.RoleBid);
            var controller = CreateController();
            var form = TestDataHelper.CreatePartnerTokenForm(tid: 99999999);   // tid ngoai phan cap

            // Act
            var result = await controller.IssueSsoToken(form);

            // Assert
            result.ShouldBeError("MidOrTidNotExistUserBid");
        }

        // ── 4. Nhánh user role MID ──────────────────────────────────────────────

        [Fact]
        public async Task IssueSsoToken_WhenRoleMidMissingTid_ReturnsTidEmptyUserMid()
        {
            // Arrange
            GivenValidUserWithRole(Roles.RoleMid);
            var controller = CreateController();
            var form = TestDataHelper.CreatePartnerTokenForm(tid: null);

            // Act
            var result = await controller.IssueSsoToken(form);

            // Assert
            result.ShouldBeError("TidEmptyUserMid");
        }

        [Fact]
        public async Task IssueSsoToken_WhenRoleMidAndUserHasNoMid_ReturnsUserMidInvalid()
        {
            // Arrange
            _frontend.Seed(TestDataHelper.CreateSession());
            _frontend.Seed(TestDataHelper.CreateUsersCommon(roleId: Roles.RoleMid));
            _frontend.Seed(TestDataHelper.CreateAppUser(mid: null));   // ← thieu MID
            _merchant.Seed(TestDataHelper.CreateTerminal());
            var controller = CreateController();

            // Act
            var result = await controller.IssueSsoToken(TestDataHelper.CreatePartnerTokenForm());

            // Assert
            result.ShouldBeError("UserMidInvalid");
        }

        [Fact]
        public async Task IssueSsoToken_WhenRoleMidAndTidNotUnderMid_ReturnsTidNotExistUserMid()
        {
            // Arrange
            GivenValidUserWithRole(Roles.RoleMid);
            var controller = CreateController();
            var form = TestDataHelper.CreatePartnerTokenForm(tid: 99999999);

            // Act
            var result = await controller.IssueSsoToken(form);

            // Assert
            result.ShouldBeError("TidNotExistUserMid");
        }

        // ── 5. Đường thành công ─────────────────────────────────────────────────

        [Fact]
        public async Task IssueSsoToken_WhenValid_ReturnsTokenWithExpectedClaims()
        {
            // Arrange
            GivenValidUserWithRole(Roles.RoleMid);
            var controller = CreateController();

            // Act
            var result = await controller.IssueSsoToken(TestDataHelper.CreatePartnerTokenForm());

            // Assert
            var claims = result.ReadTokenClaims();
            claims["sub"].Should().Be(TestDataHelper.DefaultUserName);
            claims["unique_name"].Should().Be(TestDataHelper.DefaultUserName);
            claims["email"].Should().Be(TestDataHelper.DefaultEmail);
            claims["session_id"].Should().Be(TestDataHelper.DefaultSessionId);
            claims["partner_code"].Should().Be(TestDataHelper.DefaultPartner);
        }

        /// <summary>
        /// TEST QUAN TRỌNG NHẤT.
        ///
        /// User role MID: MID phải lấy từ DB, chỉ TID mới lấy từ form. Nếu ai đó sửa
        /// thành lấy cả hai từ form thì user MID tự đặt mid nào cũng được — tự nâng
        /// quyền sang merchant khác. Test tay không bao giờ phát hiện vì client thật
        /// không gửi mid rác.
        /// </summary>
        [Fact]
        public async Task IssueSsoToken_WhenRoleMid_TakesMidFromDatabaseNotFromForm()
        {
            // Arrange
            GivenValidUserWithRole(Roles.RoleMid);
            var controller = CreateController();
            var form = TestDataHelper.CreatePartnerTokenForm(mid: 999999999);   // ← mid RAC

            // Act
            var result = await controller.IssueSsoToken(form);

            // Assert
            var claims = result.ReadTokenClaims();
            claims["mid"].Should().Be(TestDataHelper.DefaultMid.ToString());   // tu DB
            claims["tid"].Should().Be(TestDataHelper.DefaultTid.ToString());   // tu form
        }

        /// <summary>
        /// Ngược lại: user role BID thì mid/tid LẤY TỪ FORM, không phải mid của chính
        /// user. Đây là chủ ý — user BID được thao tác trên nhiều mid/tid dưới quyền,
        /// và đã kiểm tra phân cấp bằng IsMidTidUnderBidAsync ở trên.
        /// </summary>
        [Fact]
        public async Task IssueSsoToken_WhenRoleBid_TakesMidAndTidFromForm()
        {
            // Arrange
            _frontend.Seed(TestDataHelper.CreateSession());
            _frontend.Seed(TestDataHelper.CreateUsersCommon(roleId: Roles.RoleBid));
            _frontend.Seed(TestDataHelper.CreateAppUser(mid: 11111111));   // mid cua chinh user
            _merchant.Seed(TestDataHelper.CreateTerminal(mid: 22222222, tid: 33333333));
            var controller = CreateController();

            // Act
            var result = await controller.IssueSsoToken(
                TestDataHelper.CreatePartnerTokenForm(mid: 22222222, tid: 33333333));

            // Assert
            var claims = result.ReadTokenClaims();
            claims["mid"].Should().Be("22222222");   // KHONG phai 11111111
            claims["tid"].Should().Be("33333333");
        }

        /// <summary>
        /// Action cố ý tra DB thay vì dùng CurrentUserMid/RoleId có sẵn từ claim, vì
        /// claim đóng vào token lúc đăng nhập có thể đã cũ — user bị đổi mid hoặc hạ
        /// quyền sau đó thì claim vẫn giữ giá trị cũ tới khi token hết hạn.
        /// </summary>
        [Fact]
        public async Task IssueSsoToken_WhenClaimDiffersFromDatabase_TakesDatabaseValue()
        {
            // Arrange — bearer token mang mid/role CU, khac han du lieu hien tai trong DB
            GivenValidUserWithRole(Roles.RoleMid);
            var controller = CreateController(TestHttpContext.Build(
                claimThem:
                [
                    new Claim(AppSettings.ClaimMid, "88888888"),
                    new Claim(AppSettings.ClaimRoleId, Roles.RoleBid.ToString())
                ]));

            // Act
            var result = await controller.IssueSsoToken(TestDataHelper.CreatePartnerTokenForm());

            // Assert
            result.ReadTokenClaims()["mid"].Should().Be(TestDataHelper.DefaultMid.ToString());
        }

        /// <summary>
        /// Token cấp cho partner không được sống lâu hơn bearer token của user.
        /// Sai chỗ này là session thu hồi rồi mà partner SDK vẫn dùng được.
        /// </summary>
        [Fact]
        public async Task IssueSsoToken_WhenIssued_TokenExpiresWithUserBearerToken()
        {
            // Arrange
            GivenValidUserWithRole(Roles.RoleMid);
            var bearerExpiry = DateTime.UtcNow.AddMinutes(17);
            var controller = CreateController(TestHttpContext.Build(tokenExpiresUtc: bearerExpiry));

            // Act
            var result = await controller.IssueSsoToken(TestDataHelper.CreatePartnerTokenForm());

            // Assert — JWT luu exp theo giay nen so voi sai so 2 giay
            result.ReadToken().ValidTo.Should().BeCloseTo(bearerExpiry, TimeSpan.FromSeconds(2));
        }

        [Fact]
        public async Task IssueSsoToken_WhenIssued_TokenHasExpectedIssuerAndAudience()
        {
            // Arrange
            GivenValidUserWithRole(Roles.RoleMid);
            var controller = CreateController();

            // Act
            var result = await controller.IssueSsoToken(TestDataHelper.CreatePartnerTokenForm());

            // Assert
            var token = result.ReadToken();
            token.Issuer.Should().Be(TestHttpContext.TestIssuer);
            token.Audiences.Should().Contain("mobile-partner-sdk");
        }

        /// <summary>
        /// Role không phải BID cũng không phải MID thì action bỏ qua cả hai khối kiểm
        /// tra và vẫn phát token — nhưng KHÔNG có claim mid/tid. Ghi lại hành vi này
        /// bằng test để nếu sau này đổi thành trả lỗi thì test đỏ, buộc phải xem lại.
        /// </summary>
        [Fact]
        public async Task IssueSsoToken_WhenRoleIsNeitherBidNorMid_IssuesTokenWithoutMidTidClaims()
        {
            // Arrange
            GivenValidUserWithRole(Roles.RoleTid);
            var controller = CreateController();

            // Act
            var result = await controller.IssueSsoToken(TestDataHelper.CreatePartnerTokenForm());

            // Assert
            var claims = result.ReadTokenClaims();
            claims.Should().NotContainKey("mid");
            claims.Should().NotContainKey("tid");
        }
    }
}
