using System.ComponentModel.DataAnnotations.Schema;
using VcbPortalApi.StaticData.MP;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG. Phần CHÉP NGUYÊN VĂN từ ảnh code thật: enum UserType và khối
// property của MpUserCommon (UserName → UserType).
//
// PHẦN TÔI ĐOÁN — sửa lại cho khớp rồi test vẫn chạy nguyên:
//   1. Thân constructor MpUserCommon(string username): ảnh bị cắt ở dòng 37, chỉ
//      thấy `public MpUserCommon() { }`. Tôi dựng lại theo đúng những gì
//      MpUserFull yêu cầu ở phía sau:
//        - không tìm thấy dòng MP_USERS_COMMON  → UserType = NULL (ctor MpUserFull
//          `if (UserType == UserType.NULL) return;` mới có nghĩa)
//        - tìm thấy → nạp field, rồi suy UserType từ RoleId. MpUserFull so
//          `UserType == APP` / `== BCA` NHƯNG lại so `Roles.IsVcbRoles(RoleId)`
//          cho các nhánh sau, nên chỗ suy ra APP/BCA chỉ có thể nằm ở lớp cha.
//   2. Bảng MP_USERS_COMMON ở repo này đang có HAI entity: MpUsersCommon (dựng
//      trước cho luồng mobile, ít cột) và MpUserCommon (luồng FEP, nhiều cột).
//      Solution thật chỉ có một. Không gộp vì sẽ phải sửa lan sang MobileHelper.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Models.MP.User
{
    public enum UserType
    {
        NULL,   // Chưa có user
        COMMON, // Mới chỉ có trong COMMON chưa có trong bảng chi tiết tương ứng
        APP,    // Mobile App
        BCA,    // Bộ công an
        SHLX,   // Sát hạch lái xe
        API,    // API only
        VCB     // Cán bộ Vietcombank
    }

    public class MpUserCommon
    {
        public string UserName { get; set; } = null!;
        public decimal RoleId { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string Status { get; set; } = null!;
        public string? Mobile { get; set; }
        public string? Avatar { get; set; }
        public string? UserUpdate { get; set; }
        public DateTime? ExpDate { get; set; }

        public string Salt { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string UHash { get; set; } = null!;

        [NotMapped]
        public UserType UserType { get; set; } = UserType.NULL;

        public MpUserCommon() { }

        // ── Từ đây trở xuống là phần DỰNG LẠI, không phải code thật ─────────────

        public MpUserCommon(string username)
        {
            UserName = username;

            using var frontendContext = new DbContext.Oracle.FrontendContext();

            var common = frontendContext.MpUserCommons.FirstOrDefault(x => x.UserName == username);

            if (common == null)
            {
                UserType = UserType.NULL;
                return;
            }

            RoleId = common.RoleId;
            FullName = common.FullName;
            Email = common.Email;
            Status = common.Status;
            Mobile = common.Mobile;
            Avatar = common.Avatar;
            UserUpdate = common.UserUpdate;
            ExpDate = common.ExpDate;
            Salt = common.Salt;
            Password = common.Password;
            UHash = common.UHash;

            // Suy UserType từ RoleId. Bằng chứng đây là việc của LỚP CHA:
            // FepController.Authenticate dòng 73 kiểm `UserType == UserType.VCB`,
            // mà nhánh VCB trong ctor MpUserFull KHÔNG hề gán giá trị đó — vậy nó
            // phải được gán từ trước, tức ở đây.
            UserType =
                Roles.IsAppRoles(RoleId) ? UserType.APP :
                Roles.IsBcaRoles(RoleId) ? UserType.BCA :
                Roles.IsVcbRoles(RoleId) ? UserType.VCB :
                Roles.IsShlxRoles(RoleId) ? UserType.SHLX :
                Roles.IsApiRoles(RoleId) ? UserType.API :
                UserType.COMMON;
        }
        // ── Ghi DB ─────────────────────────────────────────────────────────────
        // KHÔNG virtual, đúng như solution thật. Bản thật ghi thẳng xuống Oracle;
        // bản khung ghi qua FrontendContext để test đọc lại kiểm tra được.

        /// <summary>Thêm mới user. Trả về số dòng ghi được.</summary>
        public int InsertFull()
        {
            using var frontendContext = new DbContext.Oracle.FrontendContext();

            frontendContext.MpUserCommons.Add(ToCommonRow());

            if (this is MpUserFull full)
                frontendContext.MpVcbUsers.Add(full.ToVcbRow());

            return frontendContext.SaveChanges();
        }

        /// <summary>Cập nhật user đã có. Trả về số dòng ghi được.</summary>
        public int SaveFull()
        {
            using var frontendContext = new DbContext.Oracle.FrontendContext();

            var common = frontendContext.MpUserCommons.FirstOrDefault(x => x.UserName == UserName);

            if (common == null)
                return 0;

            frontendContext.Remove(common);
            frontendContext.MpUserCommons.Add(ToCommonRow());

            if (this is MpUserFull full)
            {
                var vcb = frontendContext.MpVcbUsers.FirstOrDefault(x => x.UserName == UserName);

                if (vcb != null)
                    frontendContext.Remove(vcb);

                frontendContext.MpVcbUsers.Add(full.ToVcbRow());
            }

            return frontendContext.SaveChanges();
        }

        private MpUserCommon ToCommonRow() => new()
        {
            UserName = UserName,
            RoleId = RoleId,
            FullName = FullName,
            Email = Email,
            Status = Status,
            Mobile = Mobile,
            Avatar = Avatar,
            UserUpdate = UserUpdate,
            ExpDate = ExpDate,
            Salt = Salt,
            Password = Password,
            UHash = UHash
        };
    }
}
