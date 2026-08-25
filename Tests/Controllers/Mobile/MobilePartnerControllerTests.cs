using VcbPortalApi;
using VcbPortalApi.DbContext;
using VcbPortalApi.Models.MP;
using VcbPortalApi.Models.SSO;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using VcbPortalApi.DbContext;
using VcbPortalApi.Models.MP;
using VcbPortalApi.StaticData.MP;
using Tests.TestSupport;

namespace Tests.Controllers.Mobile
{
    /// <summary>
    /// Test cho POST /ma/partner/token — MobilePartnerController.IssueSsoToken.
    ///
    /// Action này để toàn bộ logic trong controller, không tách service, nên test
    /// đánh thẳng vào action. Mỗi lệnh return sớm trong action là một test ở đây.
    ///
    /// NGUYÊN TẮC ARRANGE: mọi test nhánh lỗi đều dựng bối cảnh HỢP LỆ HOÀN TOÀN rồi
    /// chỉ làm hỏng đúng một thứ. Nhờ vậy test fail thì chắc chắn do nhánh đó, không
    /// phải do quên seed dữ liệu chỗ khác.
    /// </summary>
    public class MobilePartnerControllerTests
    {
        private const string UserName = "VATID001";
        private const decimal UserBid = 68000000000160;
        private const decimal UserMid = 68100000000097;
        private const decimal FormTid = 40000001;

        /// <summary>
        /// Bối cảnh đầy đủ, hợp lệ cho một user role MID. Test nào cần hỏng chỗ nào
        /// thì gọi hàm này rồi chỉnh riêng chỗ đó.
        /// </summary>
        private static (FrontendContext Fe, MerchantContext Mc) BoiCanhHopLeRoleMid()
        {
            var fe = TestDb.Create<FrontendContext>();
            var mc = TestDb.Create<MerchantContext>();

            fe.Seed(new MpSession { UserName = UserName, SessionId = "session-1" });
            fe.Seed(new MpUsersCommon { UserName = UserName, Email = "a@vcb.com.vn", RoleId = Roles.RoleMid });
            fe.Seed(new MpAppUser { Username = UserName, Bid = UserBid, Mid = UserMid });
            mc.Seed(new MpTerminal { RowId = 1, Bid = UserBid, Mid = UserMid, Tid = FormTid });

            return (fe, mc);
        }

        private static (FrontendContext Fe, MerchantContext Mc) BoiCanhHopLeRoleBid()
        {
            var fe = TestDb.Create<FrontendContext>();
            var mc = TestDb.Create<MerchantContext>();

            fe.Seed(new MpSession { UserName = UserName, SessionId = "session-1" });
            fe.Seed(new MpUsersCommon { UserName = UserName, Email = "a@vcb.com.vn", RoleId = Roles.RoleBid });
            fe.Seed(new MpAppUser { Username = UserName, Bid = UserBid, Mid = UserMid });
            mc.Seed(new MpTerminal { RowId = 1, Bid = UserBid, Mid = UserMid, Tid = FormTid });

            return (fe, mc);
        }

        private static PartnerSsoTokenForm FormHopLe() => new()
        {
            PartnerCode = "PHONEPOS",
            Mid = UserMid,
            Tid = FormTid
        };

        // ═══════════════════════════════════════════════════════════════════════
        // NHÁNH LỖI — theo đúng thứ tự xuất hiện trong action
        // ═══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task IssueSsoToken_ModelStateKhongHopLe_TraVeInvalidParameters()
        {
            var (fe, mc) = BoiCanhHopLeRoleMid();
            using var _1 = fe; using var _2 = mc;

            var controller = MobileTestKit.CreateController(fe, mc);
            controller.ModelState.AddModelError(nameof(PartnerSsoTokenForm.PartnerCode), "bắt buộc");

            var result = await controller.IssueSsoToken(FormHopLe());

            MobileTestKit.AssertLoi(result, "InvalidParameters");
        }

        [Fact]
        public async Task IssueSsoToken_KhongCoDanhTinh_TraVeUnauthorized()
        {
            var (fe, mc) = BoiCanhHopLeRoleMid();
            using var _1 = fe; using var _2 = mc;

            // userName null -> không gắn claim nào -> CurrentUserName rỗng
            var controller = MobileTestKit.CreateController(fe, mc, TestHttpContext.Build(userName: null));

            var result = await controller.IssueSsoToken(FormHopLe());

            MobileTestKit.AssertUnauthorized(result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task IssueSsoToken_PartnerCodeRong_TraVePartnerCodeEmpty(string? partnerCode)
        {
            var (fe, mc) = BoiCanhHopLeRoleMid();
            using var _1 = fe; using var _2 = mc;

            var controller = MobileTestKit.CreateController(fe, mc);

            var form = FormHopLe();
            form.PartnerCode = partnerCode;

            var result = await controller.IssueSsoToken(form);

            MobileTestKit.AssertLoi(result, "PartnerCodeEmpty");
        }

        [Fact]
        public async Task IssueSsoToken_KhongCoHeaderAuthorization_TraVeUnauthorized()
        {
            var (fe, mc) = BoiCanhHopLeRoleMid();
            using var _1 = fe; using var _2 = mc;

            var controller = MobileTestKit.CreateController(fe, mc, TestHttpContext.Build(coBearerToken: false));

            var result = await controller.IssueSsoToken(FormHopLe());

            MobileTestKit.AssertUnauthorized(result);
        }

        [Fact]
        public async Task IssueSsoToken_BearerTokenHetHan_TraVeUnauthorized()
        {
            var (fe, mc) = BoiCanhHopLeRoleMid();
            using var _1 = fe; using var _2 = mc;

            var controller = MobileTestKit.CreateController(fe, mc, TestHttpContext.Build(tokenExpiresUtc: DateTime.UtcNow.AddMinutes(-1)));

            var result = await controller.IssueSsoToken(FormHopLe());

            MobileTestKit.AssertUnauthorized(result);
        }

        [Fact]
        public async Task IssueSsoToken_KhongCoSession_TraVeBaseError()
        {
            using var fe = TestDb.Create<FrontendContext>();
            using var mc = TestDb.Create<MerchantContext>();
            // Cố tình KHÔNG seed session
            fe.Seed(new MpUsersCommon { UserName = UserName, Email = "a@vcb.com.vn", RoleId = Roles.RoleMid });
            fe.Seed(new MpAppUser { Username = UserName, Mid = UserMid });

            var controller = MobileTestKit.CreateController(fe, mc);

            var result = await controller.IssueSsoToken(FormHopLe());

            MobileTestKit.AssertLoi(result, "BaseError");
        }

        [Fact]
        public async Task IssueSsoToken_SessionIdRong_TraVeBaseError()
        {
            using var fe = TestDb.Create<FrontendContext>();
            using var mc = TestDb.Create<MerchantContext>();
            fe.Seed(new MpSession { UserName = UserName, SessionId = "   " });
            fe.Seed(new MpUsersCommon { UserName = UserName, Email = "a@vcb.com.vn", RoleId = Roles.RoleMid });
            fe.Seed(new MpAppUser { Username = UserName, Mid = UserMid });

            var controller = MobileTestKit.CreateController(fe, mc);

            var result = await controller.IssueSsoToken(FormHopLe());

            MobileTestKit.AssertLoi(result, "BaseError");
        }

        [Fact]
        public async Task IssueSsoToken_EmailRong_TraVeBaseError()
        {
            using var fe = TestDb.Create<FrontendContext>();
            using var mc = TestDb.Create<MerchantContext>();
            fe.Seed(new MpSession { UserName = UserName, SessionId = "session-1" });
            fe.Seed(new MpUsersCommon { UserName = UserName, Email = null, RoleId = Roles.RoleMid });
            fe.Seed(new MpAppUser { Username = UserName, Mid = UserMid });

            var controller = MobileTestKit.CreateController(fe, mc);

            var result = await controller.IssueSsoToken(FormHopLe());

            MobileTestKit.AssertLoi(result, "BaseError");
        }

        // ── Nhánh user role BID ────────────────────────────────────────────────

        [Theory]
        [InlineData(null, 40000001d)]         // thiếu mid
        [InlineData(68100000000097d, null)]   // thiếu tid
        [InlineData(0d, 40000001d)]           // mid = 0
        [InlineData(null, null)]   // thiếu cả hai
        public async Task IssueSsoToken_RoleBidThieuMidHoacTid_TraVeMidOrMidEmptyUserBid(
            double? mid, double? tid)
        {
            var (fe, mc) = BoiCanhHopLeRoleBid();
            using var _1 = fe; using var _2 = mc;

            var controller = MobileTestKit.CreateController(fe, mc);

            var form = FormHopLe();
            form.Mid = mid is null ? null : (decimal)mid.Value;
            form.Tid = tid is null ? null : (decimal)tid.Value;

            var result = await controller.IssueSsoToken(form);

            MobileTestKit.AssertLoi(result, "MidOrMidEmptyUserBid");
        }

        [Fact]
        public async Task IssueSsoToken_RoleBidNhungUserKhongCoBid_TraVeUserBidInvalid()
        {
            using var fe = TestDb.Create<FrontendContext>();
            using var mc = TestDb.Create<MerchantContext>();
            fe.Seed(new MpSession { UserName = UserName, SessionId = "session-1" });
            fe.Seed(new MpUsersCommon { UserName = UserName, Email = "a@vcb.com.vn", RoleId = Roles.RoleBid });
            fe.Seed(new MpAppUser { Username = UserName, Bid = null, Mid = UserMid });   // ← thiếu BID
            mc.Seed(new MpTerminal { RowId = 1, Bid = UserBid, Mid = UserMid, Tid = FormTid });

            var controller = MobileTestKit.CreateController(fe, mc);

            var result = await controller.IssueSsoToken(FormHopLe());

            MobileTestKit.AssertLoi(result, "UserBidInvalid");
        }

        [Fact]
        public async Task IssueSsoToken_RoleBidMaMidTidKhongThuocBid_TraVeMidOrTidNotExistUserBid()
        {
            var (fe, mc) = BoiCanhHopLeRoleBid();
            using var _1 = fe; using var _2 = mc;

            var controller = MobileTestKit.CreateController(fe, mc);

            var form = FormHopLe();
            form.Tid = 99999999;   // tid không có trong phân cấp đã seed

            var result = await controller.IssueSsoToken(form);

            MobileTestKit.AssertLoi(result, "MidOrTidNotExistUserBid");
        }

        // ── Nhánh user role MID ────────────────────────────────────────────────

        [Fact]
        public async Task IssueSsoToken_RoleMidThieuTid_TraVeTidEmptyUserMid()
        {
            var (fe, mc) = BoiCanhHopLeRoleMid();
            using var _1 = fe; using var _2 = mc;

            var controller = MobileTestKit.CreateController(fe, mc);

            var form = FormHopLe();
            form.Tid = null;

            var result = await controller.IssueSsoToken(form);

            MobileTestKit.AssertLoi(result, "TidEmptyUserMid");
        }

        [Fact]
        public async Task IssueSsoToken_RoleMidNhungUserKhongCoMid_TraVeUserMidInvalid()
        {
            using var fe = TestDb.Create<FrontendContext>();
            using var mc = TestDb.Create<MerchantContext>();
            fe.Seed(new MpSession { UserName = UserName, SessionId = "session-1" });
            fe.Seed(new MpUsersCommon { UserName = UserName, Email = "a@vcb.com.vn", RoleId = Roles.RoleMid });
            fe.Seed(new MpAppUser { Username = UserName, Bid = UserBid, Mid = null });   // ← thiếu MID
            mc.Seed(new MpTerminal { RowId = 1, Bid = UserBid, Mid = UserMid, Tid = FormTid });

            var controller = MobileTestKit.CreateController(fe, mc);

            var result = await controller.IssueSsoToken(FormHopLe());

            MobileTestKit.AssertLoi(result, "UserMidInvalid");
        }

        [Fact]
        public async Task IssueSsoToken_RoleMidMaTidKhongThuocMid_TraVeTidNotExistUserMid()
        {
            var (fe, mc) = BoiCanhHopLeRoleMid();
            using var _1 = fe; using var _2 = mc;

            var controller = MobileTestKit.CreateController(fe, mc);

            var form = FormHopLe();
            form.Tid = 99999999;

            var result = await controller.IssueSsoToken(form);

            MobileTestKit.AssertLoi(result, "TidNotExistUserMid");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ĐƯỜNG THÀNH CÔNG
        // ═══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task IssueSsoToken_HopLe_TraVeTokenVoiDayDuClaim()
        {
            var (fe, mc) = BoiCanhHopLeRoleMid();
            using var _1 = fe; using var _2 = mc;

            var controller = MobileTestKit.CreateController(fe, mc);

            var result = await controller.IssueSsoToken(FormHopLe());

            var claims = MobileTestKit.DocClaimTuToken(result);
            Assert.Equal(UserName, claims[JwtRegisteredClaimNames.Sub]);
            Assert.Equal(UserName, claims[JwtRegisteredClaimNames.UniqueName]);
            Assert.Equal("a@vcb.com.vn", claims[JwtRegisteredClaimNames.Email]);
            Assert.Equal("session-1", claims["session_id"]);
            Assert.Equal("PHONEPOS", claims["partner_code"]);
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
        public async Task IssueSsoToken_RoleMid_ClaimMidLayTuDbKhongLayTuForm()
        {
            var (fe, mc) = BoiCanhHopLeRoleMid();
            using var _1 = fe; using var _2 = mc;

            var controller = MobileTestKit.CreateController(fe, mc);

            var form = FormHopLe();
            form.Mid = 999999999;   // ← client gửi mid RÁC, phải bị bỏ qua

            var result = await controller.IssueSsoToken(form);

            var claims = MobileTestKit.DocClaimTuToken(result);
            Assert.Equal(UserMid.ToString(), claims["mid"]);    // lấy từ MP_APP_USERS
            Assert.Equal(FormTid.ToString(), claims["tid"]);    // lấy từ form
        }

        /// <summary>
        /// ControllerCustom đã sẵn có CurrentUserRoleId / CurrentUserBid / CurrentUserMid
        /// đọc từ claim của bearer token, nhưng action lại CỐ Ý tra DB thay vì dùng chúng.
        ///
        /// Khác biệt thật sự: claim được đóng vào token lúc đăng nhập nên có thể đã cũ —
        /// user bị đổi mid hoặc hạ quyền sau đó thì claim vẫn giữ giá trị cũ cho tới khi
        /// token hết hạn. Với một endpoint đi PHÁT TIẾP token cho bên thứ ba, đọc DB là
        /// lựa chọn an toàn hơn. Test này khoá hành vi đó lại.
        /// </summary>
        [Fact]
        public async Task IssueSsoToken_ClaimTrongBearerTokenKhacDb_LayTheoDb()
        {
            var (fe, mc) = BoiCanhHopLeRoleMid();
            using var _1 = fe; using var _2 = mc;

            // Bearer token mang mid/role CŨ, khác hẳn dữ liệu hiện tại trong DB.
            var controller = MobileTestKit.CreateController(fe, mc, TestHttpContext.Build(claimThem: [
                    new Claim(AppSettings.ClaimMid, "88888888"),
                    new Claim(AppSettings.ClaimRoleId, Roles.RoleBid.ToString())
                ]));

            var result = await controller.IssueSsoToken(FormHopLe());

            var claims = MobileTestKit.DocClaimTuToken(result);
            Assert.Equal(UserMid.ToString(), claims["mid"]);   // theo DB, không theo claim cũ
        }

        /// <summary>
        /// Ngược lại với test trên: user role BID thì mid/tid LẤY TỪ FORM, không phải
        /// mid của chính user. Đây là chủ ý — user BID được thao tác trên nhiều mid/tid
        /// dưới quyền, và đã kiểm tra phân cấp bằng IsMidTidUnderBidAsync ở trên.
        /// </summary>
        [Fact]
        public async Task IssueSsoToken_RoleBid_ClaimMidTidLayTuForm()
        {
            using var fe = TestDb.Create<FrontendContext>();
            using var mc = TestDb.Create<MerchantContext>();
            fe.Seed(new MpSession { UserName = UserName, SessionId = "session-1" });
            fe.Seed(new MpUsersCommon { UserName = UserName, Email = "a@vcb.com.vn", RoleId = Roles.RoleBid });
            fe.Seed(new MpAppUser { Username = UserName, Bid = UserBid, Mid = 11111111 });   // mid của chính user
            mc.Seed(new MpTerminal { RowId = 1, Bid = UserBid, Mid = 22222222, Tid = 33333333 });

            var controller = MobileTestKit.CreateController(fe, mc);

            var result = await controller.IssueSsoToken(new PartnerSsoTokenForm
            {
                PartnerCode = "PHONEPOS",
                Mid = 22222222,   // mid khác, thuộc bid của user
                Tid = 33333333
            });

            var claims = MobileTestKit.DocClaimTuToken(result);
            Assert.Equal("22222222", claims["mid"]);   // KHÔNG phải 11111111
            Assert.Equal("33333333", claims["tid"]);
        }

        /// <summary>
        /// Token cấp cho partner không được sống lâu hơn bearer token của user.
        /// Sai chỗ này là session thu hồi rồi mà partner SDK vẫn dùng được.
        /// </summary>
        [Fact]
        public async Task IssueSsoToken_TokenHetHanCungLucVoiBearerTokenCuaUser()
        {
            var (fe, mc) = BoiCanhHopLeRoleMid();
            using var _1 = fe; using var _2 = mc;

            var hanBearer = DateTime.UtcNow.AddMinutes(17);
            var controller = MobileTestKit.CreateController(fe, mc, TestHttpContext.Build(tokenExpiresUtc: hanBearer));

            var result = await controller.IssueSsoToken(FormHopLe());

            var token = MobileTestKit.DocToken(result);
            // JWT lưu exp theo giây nên so với sai số 1 giây.
            Assert.True((token.ValidTo - hanBearer).Duration() < TimeSpan.FromSeconds(2),
                $"Hạn token partner {token.ValidTo:O} phải trùng hạn bearer {hanBearer:O}");
        }

        [Fact]
        public async Task IssueSsoToken_TokenDungIssuerVaAudience()
        {
            var (fe, mc) = BoiCanhHopLeRoleMid();
            using var _1 = fe; using var _2 = mc;

            var controller = MobileTestKit.CreateController(fe, mc);

            var result = await controller.IssueSsoToken(FormHopLe());

            var token = MobileTestKit.DocToken(result);
            Assert.Equal(TestHttpContext.TestIssuer, token.Issuer);
            Assert.Contains("mobile-partner-sdk", token.Audiences);
        }

        /// <summary>
        /// Role không phải BID cũng không phải MID thì action bỏ qua cả hai khối kiểm
        /// tra và vẫn phát token — nhưng KHÔNG có claim mid/tid. Ghi lại hành vi này
        /// bằng test để nếu sau này đổi thành trả lỗi thì test đỏ, buộc phải xem lại.
        /// </summary>
        [Fact]
        public async Task IssueSsoToken_RoleKhac_VanPhatTokenNhungKhongCoClaimMidTid()
        {
            using var fe = TestDb.Create<FrontendContext>();
            using var mc = TestDb.Create<MerchantContext>();
            fe.Seed(new MpSession { UserName = UserName, SessionId = "session-1" });
            fe.Seed(new MpUsersCommon { UserName = UserName, Email = "a@vcb.com.vn", RoleId = Roles.RoleTid });
            fe.Seed(new MpAppUser { Username = UserName, Bid = UserBid, Mid = UserMid });

            var controller = MobileTestKit.CreateController(fe, mc);

            var result = await controller.IssueSsoToken(FormHopLe());

            var claims = MobileTestKit.DocClaimTuToken(result);
            Assert.False(claims.ContainsKey("mid"));
            Assert.False(claims.ContainsKey("tid"));
        }
    }
}
