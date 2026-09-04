using System.Reflection;
using System.Runtime.ExceptionServices;
using VcbPortalApi.Controllers.Frontend;
using VcbPortalApi.Models.Hcm;
using VcbPortalApi.Models.MP.User;
using VcbPortalApi.StaticData.MP;
using VcbPortalApi.UnitTests.Fixtures;
using VcbPortalApi.UnitTests.Helpers;

namespace VcbPortalApi.UnitTests.Controllers.Frontend
{
    /// <summary>
    /// Test cho hai hàm đồng bộ cán bộ VCB: <c>InsertNewVcbUser</c> và <c>CheckModified</c>.
    /// Gọi qua reflection, truyền vào một <c>MpUserFull</c> do test tự cầm, rồi kiểm
    /// các trường trên chính đối tượng đó.
    ///
    /// KHÔNG PHỤ THUỘC DB. Cả hai hàm gán hết trường TRƯỚC khi gọi
    /// <c>InsertFull()</c>/<c>SaveFull()</c>, nên phần ánh xạ kiểm được kể cả khi bước
    /// ghi hỏng. Vì vậy nhóm này chạy được ở CẢ solution thật lẫn repo khung.
    ///
    /// Các đường đăng nhập nằm ở <see cref="FepControllerAuthenticateTests"/> — nhóm đó
    /// có chạm DB nên tách riêng file.
    /// </summary>
    [Collection(StaticStateCollection.Name)]
    public class FepControllerTests : IDisposable
    {
        private const string UserName = TestDataHelper.DefaultUserName;
        private const string MaJob = TestDataHelper.DefaultMaJob;

        public FepControllerTests() => AppSettings.JdWhiteList.Clear();

        public void Dispose() => AppSettings.JdWhiteList.Clear();
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

        private static bool InsertNewVcbUser(string userName, MpUserFull mpUserFull, VCanBo canbo)
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
        // InsertNewVcbUser — kiểm trên đối tượng, không đụng DB
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Không có mã JD thì thoát ngay, chưa gán trường nào.</summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void InsertNewVcbUser_WhenMaJobEmpty_ReturnsFalse(string? maJob)
        {
            var user = new MpUserFull();

            var result = InsertNewVcbUser(UserName, user, TestDataHelper.CreateCanBo(maJob: maJob));

            result.Should().BeFalse();
            user.UserName.Should().BeNull("thoat truoc khi gan bat ky truong nao");
        }

        /// <summary>Mã JD ngoài white list chỉ được role nghiệp vụ, không giao dịch được.</summary>
        [Fact]
        public void InsertNewVcbUser_WhenJobNotInWhiteList_AssignsRoleNghiepVu()
        {
            AppSettings.JdWhiteList.Add("JD_KHAC");
            var user = new MpUserFull();

            InsertNewVcbUser(UserName, user, TestDataHelper.CreateCanBo(maJob: MaJob));

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

            InsertNewVcbUser(UserName, user, TestDataHelper.CreateCanBo(maChucVu: maChucVu));

            user.RoleId.Should().Be(Roles.RoleTtv);
        }

        /// <summary>Có chức vụ thì lên kiểm soát viên.</summary>
        [Fact]
        public void InsertNewVcbUser_WhenJobInWhiteListWithTitle_AssignsRoleKsv()
        {
            AppSettings.JdWhiteList.Add(MaJob);
            var user = new MpUserFull();

            InsertNewVcbUser(UserName, user, TestDataHelper.CreateCanBo(maChucVu: 3));

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

            InsertNewVcbUser(UserName, user, TestDataHelper.CreateCanBo(maJob: maJob));

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

            InsertNewVcbUser(UserName, user, TestDataHelper.CreateCanBo(maJob: MaJob));

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

            InsertNewVcbUser(UserName, user, canbo);

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

            InsertNewVcbUser(UserName, user, TestDataHelper.CreateCanBo());

            user.Avatar.Should().Be($"images/thumbnail/{UserName}.jpeg");
        }

        /// <summary>Không có tài khoản AD thì không có ảnh để trỏ tới.</summary>
        [Fact]
        public void InsertNewVcbUser_WhenCanBoHasNoSamAccount_LeavesAvatarNull()
        {
            var user = new MpUserFull();

            InsertNewVcbUser(UserName, user, TestDataHelper.CreateCanBo(samAccountName: null));

            user.Avatar.Should().BeNull();
        }

        /// <summary>Mỗi user một salt riêng, kéo theo hash cũng khác.</summary>
        [Fact]
        public void InsertNewVcbUser_UsesADifferentSaltForEachUser()
        {
            var first = new MpUserFull();
            var second = new MpUserFull();

            InsertNewVcbUser("VCB0001", first, TestDataHelper.CreateCanBo());
            InsertNewVcbUser("VCB0002", second, TestDataHelper.CreateCanBo());

            first.Salt.Should().NotBe(second.Salt);
            first.Password.Should().NotBe(second.Password);
            first.UHash.Should().NotBe(second.UHash);
        }

        /// <summary>HCM thiếu số điện thoại: dùng <c>?.</c> nên không nổ, để null.</summary>
        [Fact]
        public void InsertNewVcbUser_WhenCanBoHasNoMobile_LeavesMobileNull()
        {
            var user = new MpUserFull();

            InsertNewVcbUser(UserName, user, TestDataHelper.CreateCanBo(sdtDiDong: null));

            user.Mobile.Should().BeNull("dung `?.` nen khong no, va khong bien thanh chuoi rong");
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

            InsertNewVcbUser(UserName, user, TestDataHelper.CreateCanBo(email: null));

            user.Email.Should().BeNull();
            user.UserName.Should().Be(UserName);
        }

        // ══════════════════════════════════════════════════════════════════════
        // CheckModified — kiểm trên đối tượng, không đụng DB
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
        public void CheckModified_WhenHcmHasNoMobile_ClearsMobile()
        {
            var user = ExistingUser(maJob: "JD_CU");

            CheckModified(user, TestDataHelper.CreateCanBo(maJob: "JD_MOI", sdtDiDong: null));

            user.Mobile.Should().BeNull("dong bo ghi de bang null chu khong giu so cu");
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
