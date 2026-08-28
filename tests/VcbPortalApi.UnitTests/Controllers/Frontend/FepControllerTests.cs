using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using VcbPortalApi.Controllers.Frontend;
using VcbPortalApi.DbContext;
using VcbPortalApi.Models.Hcm;
using VcbPortalApi.Models.MobileApp;
using VcbPortalApi.Models.MP;
using VcbPortalApi.Models.MP.User;
using VcbPortalApi.Models.SSO;
using VcbPortalApi.StaticData.MP;
using VcbPortalApi.Tools;
using VcbPortalApi.UnitTests.Fixtures;
using VcbPortalApi.UnitTests.Helpers;

namespace VcbPortalApi.UnitTests.Controllers.Frontend
{
    /// <summary>
    /// Chia làm ba phần theo thứ QUAN SÁT ĐƯỢC TỪ ĐÂU:
    ///
    ///   1. Endpoint Authenticate — chỉ khẳng định trên IActionResult, số lần gọi
    ///      Redis và email đã gửi. KHÔNG đọc DB, vì dòng ghi ra là do
    ///      InsertFull()/SaveFull() tạo, mà hai hàm đó ở solution thật ghi xuống
    ///      Oracle chứ không vào InMemory DB của test.
    ///
    ///   2. InsertNewVcbUser — gọi thẳng, truyền vào một MpUserFull do test tự cầm,
    ///      rồi kiểm các trường trên chính đối tượng đó. Hàm gán hết trường TRƯỚC khi
    ///      gọi InsertFull(), nên phần ánh xạ kiểm được mà không cần DB.
    ///
    ///   3. CheckModified — tương tự.
    ///
    /// LƯU Ý: BuildSettings.Env là private const = BuildEnv.Dev nên IsDev luôn true;
    /// khối kiểm captcha và khối kiểm mật khẩu là code chết ở bản build này.
    /// </summary>
    [Collection(StaticStateCollection.Name)]
    public class FepControllerTests : IDisposable
    {
        private const string UserName = TestDataHelper.DefaultUserName;
        private const string MaJob = TestDataHelper.DefaultMaJob;

        private readonly FrontendContext _db;
        private readonly Mock<IDatabase> _redisDb = new();
        private readonly Mock<IConnectionMultiplexer> _redis = new();
        private readonly List<string?> _emailedTo = [];
        private string _sendResult = "OK";
        private int _captchaCalls;

        public FepControllerTests()
        {
            var options = new DbContextOptionsBuilder<FrontendContext>()
                .UseInMemoryDatabase($"test-{Guid.NewGuid()}").Options;

            FrontendContext.AmbientOptions = options;
            _db = new FrontendContext(options);

            AppSettings.JdWhiteList.Clear();

            _redis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_redisDb.Object);

            // Trả false: test nào lỡ chạm nhánh captcha thì phải đỏ, không im lặng đi qua.
            SimpleCaptcha.Validator = (_, _) => { _captchaCalls++; return false; };

            SendEmail.Sender = (to, _, _) => { _emailedTo.Add(to); return _sendResult; };
        }

        public void Dispose()
        {
            FrontendContext.AmbientOptions = null;
            AppSettings.JdWhiteList.Clear();
            SimpleCaptcha.Validator = null!;
            SendEmail.Sender = null!;
            _db.Dispose();
        }

        // ── Dựng dữ liệu ────────────────────────────────────────────────────────

        private Task<IActionResult> Authenticate(string userName = UserName, string password = "Abcd1234!") =>
            new FepController(_db, _redis.Object)
            {
                ControllerContext = TestHttpContext.Build(userName: null)
            }
            .Authenticate(new SignInPayload { UserName = userName, Password = password });

        /// <summary>Cán bộ VCB. Thiếu dòng MP_VCB_USERS thì UserType tụt xuống COMMON.</summary>
        private void SeedVcbUser(string status = "O", string? maJob = MaJob, decimal roleId = Roles.RoleTtv)
        {
            _db.Seed(TestDataHelper.CreateUsersCommon(roleId: roleId, status: status));
            _db.Seed(TestDataHelper.CreateVcbUser(maJob: maJob));
        }

        /// <summary>User mobile — dùng khi test muốn bỏ qua khối HCM.</summary>
        private void SeedAppUser(string status = "O", string userName = UserName)
        {
            _db.Seed(TestDataHelper.CreateUsersCommon(userName: userName, roleId: Roles.RoleMid, status: status));
            _db.Seed(new MpAppUser { UserName = userName, Bid = 1 });
        }

        /// <summary>
        /// GetByIndexAsync đọc index bằng StringGetAsync(RedisKey) rồi đọc item bằng
        /// StringGetAsync(RedisKey[]) — hai overload khác nhau nên set riêng được, và
        /// test không phụ thuộc cách hàm đó ghép chuỗi key.
        /// </summary>
        private void HcmReturns(params VCanBo[] canbos)
        {
            var index = canbos.Select((_, i) => KeyValuePair.Create($"pk{i}", UserName))
                              .ToDictionary(x => x.Key, x => x.Value);

            _redisDb.Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                    .ReturnsAsync((RedisValue)JsonSerializer.Serialize(index));

            _redisDb.Setup(x => x.StringGetAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
                    .ReturnsAsync(canbos.Select(c => (RedisValue)JsonSerializer.Serialize(c)).ToArray());
        }

        private void HcmReturnsNothing() =>
            _redisDb.Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                    .ReturnsAsync(RedisValue.Null);

        private void VerifyHcmNotQueried() =>
            _redisDb.Verify(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);

        // ── Gọi hàm private ─────────────────────────────────────────────────────
        // Hai hàm này là `private static` và GIỮ NGUYÊN như bản thật — không nới
        // thành internal chỉ để test gọi được. Đổi tên hàm thì hỏng lúc CHẠY, nên
        // Invoke ném MissingMethodException nói rõ tên.

        /// <summary>
        /// BỎ QUA ngoại lệ đến từ bước ghi DB ở CUỐI hàm (<c>InsertFull</c>/<c>SaveFull</c>).
        /// Ở solution thật hai hàm đó chạm Oracle và ném (ORA-50032...), trong khi
        /// thứ đang test là phần ánh xạ trường diễn ra TRƯỚC đó.
        ///
        /// Phân biệt bằng một cờ có sẵn trong code: cả hai hàm đều gán
        /// <c>UserUpdate = AppSettings.SystemUser</c> ở bước cuối cùng trước khi ghi.
        ///   - cờ đã có  → ánh xạ xong, ngoại lệ đến từ khâu ghi → bỏ qua
        ///   - cờ chưa có → hỏng ở giữa chừng → ném tiếp
        /// Nhờ vế thứ hai mà test <c>CheckModified_WhenHcmEmailIsNull_Throws</c> vẫn
        /// bắt được lỗi thật, không bị nuốt mất.
        /// </summary>
        private static bool MappingDone(MpUserFull mpUserFull) =>
            mpUserFull.UserUpdate == AppSettings.SystemUser;

        private static bool InsertNewVcbUser(MpUserFull mpUserFull, VCanBo canbo, string userName = UserName)
        {
            try
            {
                return (bool)Invoke(nameof(InsertNewVcbUser), userName, mpUserFull, canbo)!;
            }
            catch (Exception) when (MappingDone(mpUserFull))
            {
                // InsertFull() ne'm: khong biet duoc gia tri tra ve. Cac test kiem
                // gia tri tra ve deu thoat truoc khi toi buoc ghi.
                return false;
            }
        }

        private static void CheckModified(MpUserFull mpUserFull, VCanBo canbo)
        {
            try
            {
                Invoke(nameof(CheckModified), mpUserFull, canbo);
            }
            catch (Exception) when (MappingDone(mpUserFull))
            {
            }
        }

        private static object? Invoke(string name, params object?[] args)
        {
            var method = typeof(FepController)
                .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(nameof(FepController), name);

            try
            {
                return method.Invoke(null, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // 1. ENDPOINT Authenticate — chỉ khẳng định trên response
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Chưa có tài khoản, cũng không phải cán bộ VCB — từ chối.</summary>
        [Fact]
        public async Task Authenticate_WhenUserUnknownAndNotInHcm_ReturnsBaseError()
        {
            HcmReturnsNothing();

            var result = await Authenticate();

            result.ShouldBeError(MobileApiError.CodeBaseError);
            _emailedTo.Should().BeEmpty();
        }

        /// <summary>
        /// Cán bộ đã nghỉ việc: còn tài khoản MP nhưng HCM hết bản ghi → từ chối.
        /// Code còn đặt Status = "D" rồi SaveFull() ngay trước khi trả lỗi, nhưng
        /// việc ghi đó KHÔNG quan sát được từ ngoài endpoint — xem ghi chú đầu file.
        /// </summary>
        // CHẠM DB: nhánh này đi qua InsertFull()/SaveFull(). Ở solution thật hai hàm đó
        // ghi Oracle nên test sẽ đỏ cho tới khi MpUserFull nhận được FrontendContext.

        [Fact]
        public async Task Authenticate_WhenVcbUserNoLongerInHcm_ReturnsBaseError()
        {
            SeedVcbUser();
            HcmReturnsNothing();

            var result = await Authenticate();

            result.ShouldBeError(MobileApiError.CodeBaseError);
        }

        /// <summary>Một tài khoản khớp nhiều bản ghi HCM là dữ liệu hỏng — không đoán bừa.</summary>
        [Fact]
        public async Task Authenticate_WhenHcmReturnsMoreThanOneRecord_ReturnsHcmError()
        {
            SeedVcbUser();
            HcmReturns(TestDataHelper.CreateCanBo(), TestDataHelper.CreateCanBo());

            var result = await Authenticate();

            result.ShouldBeError(MobileApiError.CodeBaseError);
            result.ShouldHaveMessage("Lỗi HCM");
        }

        /// <summary>
        /// LỖI THẬT: <c>maJob = canbo.MaJob.ToUpper().Trim()</c> không có <c>?.</c>.
        /// Bản ghi HCM thiếu mã JD làm cả request nổ — dù InsertNewVcbUser ngay sau đó
        /// có sẵn nhánh xử lý MaJob rỗng, không bao giờ chạy tới.
        /// </summary>
        [Fact]
        public async Task Authenticate_WhenHcmRecordHasNullMaJob_Throws()
        {
            HcmReturns(TestDataHelper.CreateCanBo(maJob: null));

            var act = async () => await Authenticate();

            await act.Should().ThrowAsync<NullReferenceException>();
        }

        /// <summary>User không phải cán bộ VCB thì bỏ qua khối HCM, không gọi Redis.</summary>
        [Fact]
        public async Task Authenticate_WhenAppUser_SkipsHcmLookup()
        {
            SeedAppUser();

            var result = await Authenticate();

            result.ShouldHaveAccessToken();
            VerifyHcmNotQueried();
        }

        /// <summary>
        /// Cán bộ VCB có quyền quản trị thì KHÔNG đối chiếu HCM — đó là vế
        /// <c>&amp;&amp; mpUserFull.RoleId != Roles.RoleAdmin</c>. Tài khoản admin
        /// đăng nhập được kể cả khi đã rời khỏi HCM.
        /// </summary>
        [Fact]
        public async Task Authenticate_WhenVcbAdmin_SkipsHcmLookup()
        {
            SeedVcbUser(roleId: Roles.RoleAdmin);

            var result = await Authenticate();

            result.ShouldHaveAccessToken();
            VerifyHcmNotQueried();
        }

        /// <summary>
        /// GHI LẠI LỖ HỔNG: cán bộ VCB có dòng MP_USERS_COMMON nhưng THIẾU dòng
        /// MP_VCB_USERS thì constructor hạ UserType xuống COMMON, điều kiện vào khối
        /// HCM không khớp, và toàn bộ khâu đối chiếu bị bỏ qua — cấp token luôn.
        /// Tức xoá một dòng ở bảng chi tiết là thoát được kiểm tra "còn làm việc hay đã nghỉ".
        /// </summary>
        [Fact]
        public async Task Authenticate_WhenVcbUserMissingDetailRow_SkipsHcmAndStillIssuesToken()
        {
            _db.Seed(TestDataHelper.CreateUsersCommon(roleId: Roles.RoleTtv, status: "O"));
            // cố tình KHÔNG seed MP_VCB_USERS

            var result = await Authenticate();

            result.ShouldHaveAccessToken();
            VerifyHcmNotQueried();
        }

        /// <summary>Cán bộ VCB đăng nhập lần đầu: tạo tài khoản, gửi mật khẩu qua email, KHÔNG cấp token.</summary>
        // CHẠM DB: nhánh này đi qua InsertFull()/SaveFull(). Ở solution thật hai hàm đó
        // ghi Oracle nên test sẽ đỏ cho tới khi MpUserFull nhận được FrontendContext.

        [Fact]
        public async Task Authenticate_WhenNewVcbUser_EmailsPasswordInsteadOfIssuingToken()
        {
            HcmReturns(TestDataHelper.CreateCanBo());

            var result = await Authenticate();

            result.ShouldHaveMessage("Đã gửi email thông tin đăng nhập");
            _emailedTo.Should().ContainSingle();
        }

        /// <summary>MaJob rỗng thì không tạo được tài khoản, và không gửi mail.</summary>
        [Fact]
        public async Task Authenticate_WhenNewUserCannotBeCreated_ReturnsBaseError()
        {
            HcmReturns(TestDataHelper.CreateCanBo(maJob: ""));

            var result = await Authenticate();

            result.ShouldBeError(MobileApiError.CodeBaseError);
            _emailedTo.Should().BeEmpty();
        }

        /// <summary>Gửi mật khẩu thất bại thì coi như tạo tài khoản thất bại.</summary>
        // CHẠM DB: nhánh này đi qua InsertFull()/SaveFull(). Ở solution thật hai hàm đó
        // ghi Oracle nên test sẽ đỏ cho tới khi MpUserFull nhận được FrontendContext.

        [Fact]
        public async Task Authenticate_WhenPasswordEmailFails_ReturnsBaseError()
        {
            _sendResult = "ERROR";
            HcmReturns(TestDataHelper.CreateCanBo());

            var result = await Authenticate();

            result.ShouldBeError(MobileApiError.CodeBaseError);
            _emailedTo.Should().ContainSingle("da co gang gui truoc khi bao loi");
        }

        /// <summary>Cán bộ đã có tài khoản và còn trong HCM thì đăng nhập được.</summary>
        // CHẠM DB: nhánh này đi qua InsertFull()/SaveFull(). Ở solution thật hai hàm đó
        // ghi Oracle nên test sẽ đỏ cho tới khi MpUserFull nhận được FrontendContext.

        [Fact]
        public async Task Authenticate_WhenExistingVcbUserStillInHcm_IssuesToken()
        {
            SeedVcbUser(maJob: "JD_CU");
            HcmReturns(TestDataHelper.CreateCanBo(maJob: "JD_MOI"));

            var result = await Authenticate();

            result.ShouldHaveAccessToken();
        }

        /// <summary>Chỉ trạng thái "O" mới đăng nhập được.</summary>
        [Theory]
        [InlineData("D")]
        [InlineData("A")]
        public async Task Authenticate_WhenStatusIsNotActive_ReturnsBaseError(string status)
        {
            SeedAppUser(status: status);

            var result = await Authenticate();

            result.ShouldBeError(MobileApiError.CodeBaseError);
        }

        /// <summary>Tên đăng nhập được viết hoa và cắt khoảng trắng trước khi tra cứu.</summary>
        [Theory]
        [InlineData("vatid001")]
        [InlineData("  VATID001  ")]
        public async Task Authenticate_NormalisesUserNameBeforeLookup(string typed)
        {
            SeedAppUser();

            var result = await Authenticate(userName: typed);

            result.ShouldHaveAccessToken();
        }

        // ── Nhánh không chạm tới được ở bản Dev ─────────────────────────────────

        /// <summary>Khối else chứa SimpleCaptcha là code chết — nhánh wrong_captcha không có đường tới.</summary>
        [Fact]
        public async Task Authenticate_InDevBuild_NeverChecksCaptcha()
        {
            SeedAppUser();

            var result = await Authenticate();

            _captchaCalls.Should().Be(0);
            result.ShouldHaveAccessToken();
        }

        public static TheoryData<string> AnyUserName => [UserName, AppSettings.AdminUsername];

        /// <summary>
        /// Mật khẩu KHÔNG được kiểm, kể cả tài khoản quản trị: IsDev đứng trước nên
        /// đoản mạch luôn vế !userName.Equals(AdminUsername). ValidateHash, ghi log sai
        /// mật khẩu và kiểm độ mạnh mật khẩu đều là code chết ở bản Dev.
        /// </summary>
        [Theory]
        [MemberData(nameof(AnyUserName))]
        public async Task Authenticate_InDevBuild_AcceptsAnyPassword(string userName)
        {
            SeedAppUser(userName: userName);

            var result = await Authenticate(userName: userName, password: "sai-bet-nhe");

            result.ShouldHaveAccessToken();
        }

        // ══════════════════════════════════════════════════════════════════════
        // 2. InsertNewVcbUser — kiểm trên đối tượng, không đụng DB
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Không có mã JD thì thoát ngay, chưa gán trường nào.</summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void InsertNewVcbUser_WhenMaJobEmpty_ReturnsFalse(string? maJob)
        {
            var user = new MpUserFull();

            var result = InsertNewVcbUser(user, TestDataHelper.CreateCanBo(maJob: maJob));

            result.Should().BeFalse();
            user.UserName.Should().BeNull("thoat truoc khi gan bat ky truong nao");
        }

        /// <summary>Mã JD ngoài white list chỉ được role nghiệp vụ, không giao dịch được.</summary>
        [Fact]
        public void InsertNewVcbUser_WhenJobNotInWhiteList_AssignsRoleNghiepVu()
        {
            AppSettings.JdWhiteList.Add("JD_KHAC");
            var user = new MpUserFull();

            InsertNewVcbUser(user, TestDataHelper.CreateCanBo(maJob: MaJob));

            user.RoleId.Should().Be(Roles.RoleNghiepVu);
        }

        /// <summary>Trong white list mà chưa có chức vụ (null hoặc 0) là thanh toán viên.</summary>
        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        public void InsertNewVcbUser_WhenJobInWhiteListWithoutTitle_AssignsRoleTtv(int? maChucVu)
        {
            AppSettings.JdWhiteList.Add(MaJob);
            var user = new MpUserFull();

            InsertNewVcbUser(user, TestDataHelper.CreateCanBo(maChucVu: maChucVu));

            user.RoleId.Should().Be(Roles.RoleTtv);
        }

        /// <summary>Có chức vụ thì lên kiểm soát viên.</summary>
        [Fact]
        public void InsertNewVcbUser_WhenJobInWhiteListWithTitle_AssignsRoleKsv()
        {
            AppSettings.JdWhiteList.Add(MaJob);
            var user = new MpUserFull();

            InsertNewVcbUser(user, TestDataHelper.CreateCanBo(maChucVu: 3));

            user.RoleId.Should().Be(Roles.RoleKsv);
        }

        /// <summary>Mã JD bên HCM có chữ thường / khoảng trắng thừa vẫn khớp white list.</summary>
        [Theory]
        [InlineData("jd_ttv")]
        [InlineData("  JD_TTV  ")]
        public void InsertNewVcbUser_NormalisesHcmJobBeforeMatchingWhiteList(string maJob)
        {
            AppSettings.JdWhiteList.Add(MaJob);
            var user = new MpUserFull();

            InsertNewVcbUser(user, TestDataHelper.CreateCanBo(maJob: maJob));

            user.RoleId.Should().Be(Roles.RoleTtv);
        }

        /// <summary>
        /// CẠM BẪY CẤU HÌNH: code chỉ chuẩn hoá vế HCM (<c>canbo.MaJob.ToUpper().Trim()</c>),
        /// vế white list so nguyên văn. Khai mã JD chữ thường trong cấu hình là không
        /// bao giờ khớp — cán bộ lặng lẽ tụt xuống role nghiệp vụ, không lỗi, không log.
        /// </summary>
        [Fact]
        public void InsertNewVcbUser_WhenWhiteListEntryIsLowercase_NeverMatches()
        {
            AppSettings.JdWhiteList.Add(MaJob.ToLowerInvariant());
            var user = new MpUserFull();

            InsertNewVcbUser(user, TestDataHelper.CreateCanBo(maJob: MaJob));

            user.RoleId.Should().Be(Roles.RoleNghiepVu);
        }

        /// <summary>Hồ sơ chép từ HCM: trạng thái mở, email chữ thường, số điện thoại 10 số.</summary>
        [Fact]
        public void InsertNewVcbUser_MapsProfileFromHcm()
        {
            var user = new MpUserFull();
            var canbo = TestDataHelper.CreateCanBo(
                hoTen: "Nguyen Van B",
                maCn: 777,
                email: "B.Nguyen@VIETCOMBANK.com.vn",
                sdtDiDong: "+84 900 000 0012");

            InsertNewVcbUser(user, canbo);

            user.UserName.Should().Be(UserName);
            user.Status.Should().Be("O");
            user.FullName.Should().Be("Nguyen Van B");
            user.BranchId.Should().Be(777);
            user.Email.Should().Be("b.nguyen@vietcombank.com.vn");
            user.Mobile.Should().Be("8490000000").And.HaveLength(10);
            user.UserUpdate.Should().Be(AppSettings.SystemUser);
            user.MaDv.Should().Be(canbo.MaDv);
            user.MaCb.Should().Be(canbo.MaCb);
            user.MaJob.Should().Be(canbo.MaJob);
        }

        /// <summary>Có tài khoản AD thì avatar trỏ vào ảnh thumbnail theo username.</summary>
        [Fact]
        public void InsertNewVcbUser_WhenCanBoHasSamAccount_SetsThumbnailAvatar()
        {
            var user = new MpUserFull();

            InsertNewVcbUser(user, TestDataHelper.CreateCanBo());

            user.Avatar.Should().Be($"images/thumbnail/{UserName}.jpeg");
        }

        /// <summary>Không có tài khoản AD thì không có ảnh để trỏ tới.</summary>
        [Fact]
        public void InsertNewVcbUser_WhenCanBoHasNoSamAccount_LeavesAvatarNull()
        {
            var user = new MpUserFull();

            InsertNewVcbUser(user, TestDataHelper.CreateCanBo(samAccountName: null));

            user.Avatar.Should().BeNull();
        }

        /// <summary>Mỗi user một salt riêng, kéo theo hash cũng khác.</summary>
        [Fact]
        public void InsertNewVcbUser_UsesADifferentSaltForEachUser()
        {
            var first = new MpUserFull();
            var second = new MpUserFull();

            InsertNewVcbUser(first, TestDataHelper.CreateCanBo(), userName: "VCB0001");
            InsertNewVcbUser(second, TestDataHelper.CreateCanBo(), userName: "VCB0002");

            first.Salt.Should().NotBe(second.Salt);
            first.Password.Should().NotBe(second.Password);
            first.UHash.Should().NotBe(second.UHash);
        }

        /// <summary>
        /// GHI LẠI: HCM thiếu email thì tài khoản VẪN được tạo, mật khẩu "gửi" vào
        /// địa chỉ null — người dùng không bao giờ nhận được. Nhánh tạo mới dùng
        /// <c>canbo.Email?.KeepSafe()</c> nên không nổ; đối chiếu với CheckModified bên dưới.
        /// </summary>
        [Fact]
        public void InsertNewVcbUser_WhenCanBoHasNoEmail_StillAssignsFieldsWithNullEmail()
        {
            var user = new MpUserFull();

            InsertNewVcbUser(user, TestDataHelper.CreateCanBo(email: null));

            user.Email.Should().BeNull();
            user.UserName.Should().Be(UserName);
        }

        // ══════════════════════════════════════════════════════════════════════
        // 3. CheckModified — kiểm trên đối tượng, không đụng DB
        // ══════════════════════════════════════════════════════════════════════

        private static MpUserFull ExistingUser(string? maJob = MaJob, string? avatar = null) => new()
        {
            UserName = UserName,
            MaJob = maJob,
            Avatar = avatar,
            FullName = "Ten cu",
            Email = "cu@vietcombank.com.vn",
            Mobile = "0911111111",
            UserUpdate = "nguoi-cu"
        };

        /// <summary>
        /// MaJob không đổi thì bỏ qua HẾT — kể cả khi họ tên, email, chi nhánh bên HCM
        /// đã khác. Đây là quyết định nghiệp vụ có thật: đổi phòng/chi nhánh mà giữ
        /// nguyên JD sẽ KHÔNG được đồng bộ sang MP.
        /// </summary>
        [Fact]
        public void CheckModified_WhenMaJobUnchanged_DoesNotTouchAnything()
        {
            var user = ExistingUser();

            CheckModified(user, TestDataHelper.CreateCanBo(hoTen: "Ten moi", maCn: 999));

            user.FullName.Should().Be("Ten cu");
            user.BranchId.Should().BeNull();
            user.UserUpdate.Should().Be("nguoi-cu");
        }

        /// <summary>MaJob đổi thì chép lại hồ sơ và đánh dấu người sửa là hệ thống.</summary>
        [Fact]
        public void CheckModified_WhenMaJobChanged_SyncsProfile()
        {
            var user = ExistingUser(maJob: "JD_CU");
            var canbo = TestDataHelper.CreateCanBo(maJob: "JD_MOI", hoTen: "Ten moi", maCn: 777);

            CheckModified(user, canbo);

            user.MaJob.Should().Be("JD_MOI");
            user.FullName.Should().Be("Ten moi");
            user.BranchId.Should().Be(777);
            user.MaDv.Should().Be(canbo.MaDv);
            user.MaCb.Should().Be(canbo.MaCb);
            user.UserUpdate.Should().Be(AppSettings.SystemUser);
        }

        /// <summary>So khớp phân biệt hoa thường, nên chỉ khác kiểu chữ cũng đồng bộ lại.</summary>
        [Fact]
        public void CheckModified_WhenMaJobDiffersOnlyByCase_TreatsItAsChanged()
        {
            var user = ExistingUser(maJob: MaJob.ToLowerInvariant());

            CheckModified(user, TestDataHelper.CreateCanBo());

            user.MaJob.Should().Be(MaJob);
            user.UserUpdate.Should().Be(AppSettings.SystemUser);
        }

        /// <summary>MaJob rỗng luôn được coi là cần đồng bộ lại.</summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void CheckModified_WhenExistingMaJobEmpty_SyncsEvenIfHcmMatches(string? maJob)
        {
            var user = ExistingUser(maJob: maJob);

            CheckModified(user, TestDataHelper.CreateCanBo(maJob: maJob, hoTen: "Ten moi"));

            user.FullName.Should().Be("Ten moi");
        }

        /// <summary>Đồng bộ cũng chuẩn hoá email và số điện thoại như lúc tạo mới.</summary>
        [Fact]
        public void CheckModified_NormalisesEmailAndMobile()
        {
            var user = ExistingUser(maJob: "JD_CU");
            var canbo = TestDataHelper.CreateCanBo(
                maJob: "JD_MOI",
                email: "B.Nguyen@VIETCOMBANK.com.vn",
                sdtDiDong: "+84 900 000 0012");

            CheckModified(user, canbo);

            user.Email.Should().Be("b.nguyen@vietcombank.com.vn");
            user.Mobile.Should().Be("8490000000");
        }

        /// <summary>
        /// LỖI THẬT: <c>canbo.Email.KeepEmailAddressSafe()</c> KHÔNG có <c>?.</c>,
        /// trong khi InsertNewVcbUser lại có. Cán bộ thiếu email: tạo mới thì trót lọt,
        /// còn ĐỒNG BỘ thì nổ. Compiler đã cảnh báo CS8604 đúng dòng đó.
        /// </summary>
        [Fact]
        public void CheckModified_WhenHcmEmailIsNull_Throws()
        {
            var user = ExistingUser(maJob: "JD_CU");

            var act = () => CheckModified(user, TestDataHelper.CreateCanBo(maJob: "JD_MOI", email: null));

            act.Should().Throw<Exception>();
        }

        /// <summary>Chưa có avatar thì gán ảnh thumbnail theo username.</summary>
        [Fact]
        public void CheckModified_WhenAvatarMissing_SetsThumbnail()
        {
            var user = ExistingUser(maJob: "JD_CU");

            CheckModified(user, TestDataHelper.CreateCanBo(maJob: "JD_MOI"));

            user.Avatar.Should().Be($"images/thumbnail/{UserName}.jpeg");
        }

        /// <summary>Avatar đang trỏ vào ảnh user khác thì bị gán lại cho đúng người.</summary>
        [Fact]
        public void CheckModified_WhenAvatarBelongsToAnotherUser_Rewrites()
        {
            var user = ExistingUser(maJob: "JD_CU", avatar: "images/thumbnail/VCB9999.jpeg");

            CheckModified(user, TestDataHelper.CreateCanBo(maJob: "JD_MOI"));

            user.Avatar.Should().Be($"images/thumbnail/{UserName}.jpeg");
        }

        /// <summary>Avatar đã đúng người thì giữ nguyên, không đè ảnh tự tải lên.</summary>
        [Fact]
        public void CheckModified_WhenAvatarAlreadyMatchesUser_KeepsIt()
        {
            var existing = $"images/avatar-tu-tai/{UserName}.png";
            var user = ExistingUser(maJob: "JD_CU", avatar: existing);

            CheckModified(user, TestDataHelper.CreateCanBo(maJob: "JD_MOI"));

            user.Avatar.Should().Be(existing);
        }

        /// <summary>Cán bộ không có tài khoản AD thì không có ảnh thumbnail để trỏ tới.</summary>
        [Fact]
        public void CheckModified_WhenCanBoHasNoSamAccount_LeavesAvatarAlone()
        {
            var user = ExistingUser(maJob: "JD_CU");

            CheckModified(user, TestDataHelper.CreateCanBo(maJob: "JD_MOI", samAccountName: null));

            user.Avatar.Should().BeNull();
        }
    }
}
