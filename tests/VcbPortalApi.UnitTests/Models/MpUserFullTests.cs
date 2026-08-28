using Microsoft.EntityFrameworkCore;
using VcbPortalApi.DbContext;
using VcbPortalApi.Models.MP.User;
using VcbPortalApi.Models.SSO;
using VcbPortalApi.StaticData.MP;
using VcbPortalApi.UnitTests.Fixtures;
using VcbPortalApi.UnitTests.Helpers;

namespace VcbPortalApi.UnitTests.Models
{
    /// <summary>
    /// Constructor MpUserFull(string) tuỳ UserType / RoleId mà đọc sang một bảng chi
    /// tiết khác nhau. Mỗi nhánh một test, cộng trường hợp chưa có user và trường hợp
    /// có user nhưng thiếu dòng chi tiết.
    ///
    /// Constructor tự mở new FrontendContext() nên phải trỏ AmbientOptions sang InMemory.
    /// </summary>
    [Collection(StaticStateCollection.Name)]
    public class MpUserFullTests : IDisposable
    {
        private readonly FrontendContext _db;

        public MpUserFullTests()
        {
            var options = new DbContextOptionsBuilder<FrontendContext>()
                .UseInMemoryDatabase($"test-{Guid.NewGuid()}").Options;

            FrontendContext.AmbientOptions = options;
            _db = new FrontendContext(options);

            Branches.Names = new Dictionary<decimal, string> { [203] = "CN Ha Noi" };
        }

        public void Dispose()
        {
            FrontendContext.AmbientOptions = null;
            Branches.Names = [];
            _db.Dispose();
        }

        private static MpUserFull Load() => new(TestDataHelper.DefaultUserName);

        /// <summary>Không có dòng MP_USERS_COMMON thì dừng ngay, không đọc bảng chi tiết.</summary>
        [Fact]
        public void Ctor_WhenUserNotInCommon_LeavesUserTypeNull()
        {
            var user = Load();

            user.UserType.Should().Be(UserType.NULL);
            user.RoleName.Should().BeNull();
        }

        /// <summary>User mobile: nạp bid/mid/tid và tên chi nhánh.</summary>
        [Fact]
        public void Ctor_WhenAppUser_MapsMerchantAndBranch()
        {
            _db.Seed(TestDataHelper.CreateUsersCommon(roleId: Roles.RoleMid));
            _db.Seed(new MpAppUser
            {
                UserName = TestDataHelper.DefaultUserName,
                Bid = 1,
                Mid = 2,
                Tid = 3,
                BranchId = 203
            });

            var user = Load();

            user.UserType.Should().Be(UserType.APP);
            user.Bid.Should().Be(1);
            user.Mid.Should().Be(2);
            user.Tid.Should().Be(3);
            user.BranchName.Should().Be("CN Ha Noi");
            user.RoleName.Should().Be(Roles.GetRoleName(Roles.RoleMid));
        }

        /// <summary>Có trong COMMON nhưng chưa có dòng chi tiết thì hạ xuống COMMON.</summary>
        [Fact]
        public void Ctor_WhenDetailRowMissing_FallsBackToCommon()
        {
            _db.Seed(TestDataHelper.CreateUsersCommon(roleId: Roles.RoleMid));

            var user = Load();

            user.UserType.Should().Be(UserType.COMMON);
            user.Bid.Should().BeNull();
        }

        /// <summary>User Bộ Công an: nạp địa bàn tỉnh/huyện/xã.</summary>
        [Fact]
        public void Ctor_WhenBcaUser_MapsAdministrativeArea()
        {
            _db.Seed(TestDataHelper.CreateUsersCommon(roleId: Roles.RoleBca));
            _db.Seed(TestDataHelper.CreateBcaUser());

            var user = Load();

            user.UserType.Should().Be(UserType.BCA);
            user.Tinh.Should().Be(1);
            user.Huyen.Should().Be(2);
            user.Xa.Should().Be(3);
        }

        /// <summary>Cán bộ VCB: nạp chi nhánh, phòng ban, chức vụ.</summary>
        [Fact]
        public void Ctor_WhenVcbRole_MapsStaffFields()
        {
            _db.Seed(TestDataHelper.CreateUsersCommon(roleId: Roles.RoleTtv));
            _db.Seed(TestDataHelper.CreateVcbUser());

            var user = Load();

            user.UserType.Should().Be(UserType.VCB);
            user.BranchId.Should().Be(203);
            user.BranchName.Should().Be("CN Ha Noi");
            user.MaDv.Should().Be(5);
            user.MaPhong.Should().Be(12);
            user.MaCb.Should().Be(77);
            user.MaJob.Should().Be(TestDataHelper.DefaultMaJob);
        }

        /// <summary>MaDv/MaCb bên MpUserFull không nullable — cột NULL quy về 0.</summary>
        [Fact]
        public void Ctor_WhenVcbNumbersAreNull_MapsThemToZero()
        {
            _db.Seed(TestDataHelper.CreateUsersCommon(roleId: Roles.RoleTtv));
            _db.Seed(TestDataHelper.CreateVcbUser(maDv: null, maCb: null));

            var user = Load();

            user.MaDv.Should().Be(0);
            user.MaCb.Should().Be(0);
        }

        /// <summary>User sát hạch lái xe: chỉ nạp terminal.</summary>
        [Fact]
        public void Ctor_WhenShlxRole_MapsTerminalId()
        {
            _db.Seed(TestDataHelper.CreateUsersCommon(roleId: Roles.RoleShlx));
            _db.Seed(TestDataHelper.CreateShlxUser());

            var user = Load();

            user.TerminalId.Should().Be("TERM01");
        }

        /// <summary>User API: chỉ nạp bid.</summary>
        [Fact]
        public void Ctor_WhenApiRole_MapsBidOnly()
        {
            _db.Seed(TestDataHelper.CreateUsersCommon(roleId: Roles.RoleApi));
            _db.Seed(TestDataHelper.CreateApiUser());

            var user = Load();

            user.Bid.Should().Be(999);
            user.Mid.Should().BeNull();
            user.Tid.Should().BeNull();
        }
    }
}
