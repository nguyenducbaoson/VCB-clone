using VcbPortalApi.Models.Hcm;
using VcbPortalApi.Models.MP.User.Detail;
using VcbPortalApi.Models.MP.User;
using VcbPortalApi.Models.SSO;
using VcbPortalApi.StaticData.MP;

namespace VcbPortalApi.UnitTests.Helpers
{
    /// <summary>
    /// Nhà máy dựng dữ liệu mẫu. Mỗi tham số có giá trị mặc định hợp lệ, test chỉ
    /// đặt lại đúng thứ nó quan tâm — nhờ vậy Arrange ngắn và người đọc thấy ngay
    /// đâu là dữ liệu có ý nghĩa với test đó.
    /// </summary>
    public static class TestDataHelper
    {
        /// <summary>Viết HOA vì code chuẩn hoá username bằng <c>ToUpper()</c> trước khi so khớp.</summary>
        public const string DefaultUserName = "VATID001";

        public const string DefaultMaJob = "JD_TTV";


        /// <summary>
        /// Dòng MP_USERS_COMMON. Salt/Password/UHash BẮT BUỘC có giá trị: ba cột đó
        /// khai không nullable nên EF coi là required, seed thiếu là
        /// <c>DbUpdateException: Required properties ... are missing</c>.
        /// </summary>
        public static MpUserCommon CreateUsersCommon(
            string userName = DefaultUserName,
            decimal roleId = Roles.RoleMid,
            string? fullName = "Nguyen Van A",
            string? email = "user@vcb.com.vn",
            string status = "A",
            string? mobile = "0900000001",
            string? avatar = null) => new()
            {
                UserName = userName,
                RoleId = roleId,
                FullName = fullName,
                Email = email,
                Status = status,
                Mobile = mobile,
                Avatar = avatar,
                UserUpdate = "admin",
                Salt = "c2FsdA==",
                Password = "cGFzcw==",
                UHash = "aGFzaA=="
            };

        public static MpAppUser CreateAppUser(
            string userName = DefaultUserName,
            string? fcmToken = "fcm-token-cu",
            string? fid = "fid-cu",
            string? deviceId = "device-cu") => new()
            {
                UserName = userName,
                FcmToken = fcmToken,
                Fid = fid,
                DeviceId = deviceId
            };

        // ── Bảng chi tiết theo loại user ────────────────────────────────────────

        public static MpVcbUser CreateVcbUser(
            string userName = DefaultUserName,
            decimal? branchId = 203,
            int? maDv = 5,
            int? maCb = 77,
            string? maJob = DefaultMaJob) => new()
            {
                UserName = userName,
                BranchId = branchId,
                MaDv = maDv,
                TenDv = "Phong Kinh doanh",
                MaPhong = 12,
                TenPhong = "To The",
                NamSinh = new DateTime(1990, 1, 1),
                MaCb = maCb,
                MaJob = maJob,
                TenJob = "Chuyen vien",
                MaChucVu = 0,
                TenChucVu = "Nhan vien"
            };

        public static MpBcaUser CreateBcaUser(string userName = DefaultUserName) =>
            new() { UserName = userName, Tinh = 1, Huyen = 2, Xa = 3 };

        public static MpShlxUser CreateShlxUser(
            string userName = DefaultUserName,
            string? terminalId = "TERM01") =>
            new() { UserName = userName, TerminalId = terminalId };

        public static MpApiUser CreateApiUser(
            string userName = DefaultUserName,
            decimal? bid = 999) =>
            new() { UserName = userName, Bid = bid };

        /// <summary>Bản ghi cán bộ lấy từ hệ thống nhân sự (HCM).</summary>
        public static VCanBo CreateCanBo(
            string? maJob = DefaultMaJob,
            int? maChucVu = 0,
            string? samAccountName = "vcb\a.nguyen",
            string? email = "A.Nguyen@VIETCOMBANK.com.vn",
            string? sdtDiDong = "0900 000 001",
            decimal? maCn = 203,
            string? hoTen = "Nguyen Van A") => new()
            {
                SamAccountName = samAccountName,
                HoTen = hoTen,
                MaCn = maCn,
                Email = email,
                SdtDiDong = sdtDiDong,
                NamSinh = new DateTime(1990, 1, 1),
                MaCb = 77,
                MaJob = maJob,
                TenJob = "Chuyen vien",
                MaChucVu = maChucVu,
                TenChucVu = "Nhan vien",
                MaDv = 5,
                TenDv = "Phong Kinh doanh",
                MaPhong = 12,
                TenPhong = "To The"
            };
    }
}
