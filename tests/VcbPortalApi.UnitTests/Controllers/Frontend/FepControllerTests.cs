using VcbPortalApi.Models.MobileApp;
using VcbPortalApi.Helpers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using VcbPortalApi.Controllers.Frontend;
using VcbPortalApi.DbContext;
using VcbPortalApi.Models.Hcm;
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
    /// Endpoint POST apimp/user/auth — FepController.Authenticate.
    ///
    /// LƯU Ý: BuildSettings.Env là private const = BuildEnv.Dev, nên IsDev luôn true
    /// và hai khối là code chết ở bản build này: kiểm captcha, và kiểm mật khẩu.
    /// Không test nào chạm tới được nhánh sai captcha / sai mật khẩu / mật khẩu yếu.
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

            SendEmail.Sender = (to, _, _) => { _emailedTo.Add(to); return "OK"; };
        }

        public void Dispose()
        {
            FrontendContext.AmbientOptions = null;
            AppSettings.JdWhiteList.Clear();
            SimpleCaptcha.Validator = null!;
            SendEmail.Sender = null!;
            _db.Dispose();
        }

        // ── Arrange ─────────────────────────────────────────────────────────────

        private Task<IActionResult> Authenticate(string userName = UserName, string password = "Abcd1234!") =>
            new FepController(_db, _redis.Object)
            {
                ControllerContext = TestHttpContext.Build(userName: null)
            }
            .Authenticate(new SignInPayload { UserName = userName, Password = password });

        /// <summary>Cán bộ VCB. Thiếu dòng MP_VCB_USERS thì UserType tụt xuống COMMON và bỏ qua khối HCM.</summary>
        private void SeedVcbUser(string status = "O", string? maJob = MaJob)
        {
            _db.Seed(TestDataHelper.CreateUsersCommon(roleId: Roles.RoleTtv, status: status));
            _db.Seed(TestDataHelper.CreateVcbUser(maJob: maJob));
        }

        /// <summary>User mobile — dùng khi test muốn bỏ qua khối HCM.</summary>
        private void SeedAppUser(string status = "O", string userName = UserName)
        {
            _db.Seed(TestDataHelper.CreateUsersCommon(userName: userName, roleId: Roles.RoleMid, status: status));
            _db.Seed(new MpAppUser { UserName = userName, Bid = 1 });
        }

        private MpUserCommon? Common() =>
            _db.MpUsersCommons.AsNoTracking().FirstOrDefault(x => x.UserName == UserName);

        private string? SavedMaJob() =>
            _db.MpVcbUsers.AsNoTracking().FirstOrDefault(x => x.UserName == UserName)?.MaJob;

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

        // ── Đối chiếu HCM ───────────────────────────────────────────────────────

        /// <summary>Chưa có tài khoản, cũng không phải cán bộ VCB — từ chối, không tạo gì.</summary>
        [Fact]
        public async Task Authenticate_WhenUserUnknownAndNotInHcm_ReturnsBaseError()
        {
            HcmReturnsNothing();

            var result = await Authenticate();

            result.ShouldBeError(MobileApiError.CodeBaseError);
            Common().Should().BeNull();
        }

        /// <summary>Cán bộ đã nghỉ việc: HCM hết bản ghi thì khoá tài khoản, không chỉ từ chối.</summary>
        [Fact]
        public async Task Authenticate_WhenVcbUserNoLongerInHcm_DeactivatesAccount()
        {
            SeedVcbUser();
            HcmReturnsNothing();

            var result = await Authenticate();

            result.ShouldBeError(MobileApiError.CodeBaseError);
            Common()!.Status.Should().Be("D");
            Common()!.UserUpdate.Should().Be(AppSettings.SystemUser);
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
            _redisDb.Verify(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);
        }

        // ── Tạo tài khoản mới ───────────────────────────────────────────────────

        /// <summary>Lần đầu đăng nhập: tạo tài khoản, gửi mật khẩu qua email, KHÔNG cấp token.</summary>
        [Fact]
        public async Task Authenticate_WhenNewVcbUser_CreatesAccountAndEmailsPassword()
        {
            HcmReturns(TestDataHelper.CreateCanBo());

            var result = await Authenticate();

            result.ShouldHaveMessage("Đã gửi email thông tin đăng nhập");
            Common().Should().NotBeNull();
            SavedMaJob().Should().Be(MaJob);
            _emailedTo.Should().ContainSingle();
        }

        /// <summary>MaJob rỗng thì không tạo được tài khoản — nhánh return false của InsertNewVcbUser.</summary>
        [Fact]
        public async Task Authenticate_WhenNewUserCannotBeCreated_ReturnsBaseError()
        {
            HcmReturns(TestDataHelper.CreateCanBo(maJob: ""));

            var result = await Authenticate();

            result.ShouldBeError(MobileApiError.CodeBaseError);
            Common().Should().BeNull();
            _emailedTo.Should().BeEmpty();
        }

        /// <summary>Gửi mail hỏng thì trả lỗi, nhưng user đã nằm trong DB và không rollback.</summary>
        [Fact]
        public async Task Authenticate_WhenPasswordEmailFails_ReturnsBaseErrorButKeepsUser()
        {
            SendEmail.Sender = (_, _, _) => "ERROR";
            HcmReturns(TestDataHelper.CreateCanBo());

            var result = await Authenticate();

            result.ShouldBeError(MobileApiError.CodeBaseError);
            Common().Should().NotBeNull();
        }

        // ── Đồng bộ tài khoản đã có ─────────────────────────────────────────────

        /// <summary>MaJob đổi thì cập nhật hồ sơ theo HCM ngay trong lần đăng nhập.</summary>
        [Fact]
        public async Task Authenticate_WhenExistingVcbUserChangedInHcm_SyncsProfile()
        {
            SeedVcbUser(maJob: "JD_CU");
            HcmReturns(TestDataHelper.CreateCanBo(maJob: "JD_MOI", hoTen: "Ten moi"));

            var result = await Authenticate();

            result.ShouldHaveAccessToken();
            SavedMaJob().Should().Be("JD_MOI");
            Common()!.FullName.Should().Be("Ten moi");
        }

        /// <summary>MaJob không đổi thì không ghi lại gì, kể cả khi họ tên bên HCM đã khác.</summary>
        [Fact]
        public async Task Authenticate_WhenExistingVcbUserUnchanged_LeavesProfileAlone()
        {
            SeedVcbUser(maJob: MaJob);
            HcmReturns(TestDataHelper.CreateCanBo(maJob: MaJob, hoTen: "Ten moi"));

            var result = await Authenticate();

            result.ShouldHaveAccessToken();
            Common()!.FullName.Should().NotBe("Ten moi");
        }

        // ── Trạng thái tài khoản ────────────────────────────────────────────────

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

        // ── Chuẩn hoá tên đăng nhập ─────────────────────────────────────────────

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
            _db.MpAppUserActionLogs.Should().BeEmpty("khong di qua nhanh ghi log sai mat khau");
        }
    }
}
