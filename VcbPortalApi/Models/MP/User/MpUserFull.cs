using VcbPortalApi.DbContext.Oracle;
using VcbPortalApi.Models.MP.User.Detail;
using VcbPortalApi.StaticData.MP;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG. Khối property và TOÀN BỘ thân constructor MpUserFull(string) dưới
// đây được CHÉP NGUYÊN VĂN từ ảnh code thật (dòng 6 → 133).
//
// PHẦN TÔI THÊM, code thật không có — đánh dấu rõ ở từng chỗ:
//   1. `public MpUserFull() { }` — để test dựng được đối tượng mà không đụng DB
//      (lớp cha MpUserCommon có sẵn ctor rỗng nên nhiều khả năng bản thật cũng có).
//   2. ToVcbRow() — dựng dòng MP_VCB_USERS cho InsertFull()/SaveFull() bên lớp
//      cha (hai hàm đó KHÔNG virtual, đúng như bản thật).
//
// LƯU Ý HÀNH VI (không phải bug của bản khung): nhánh Roles.IsVcbRoles KHÔNG gán
// UserType = UserType.VCB. Chép đúng như ảnh. Test MpUserFullTests có ghi lại điều này.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Models.MP.User
{
    public class MpUserFull : MpUserCommon
    {
        public decimal? BranchId { get; set; }
        public string? TerminalId { get; set; }
        public decimal? Bid { get; set; }
        public decimal? Mid { get; set; }
        public decimal? Tid { get; set; }
        public decimal? Tinh { get; set; }
        public decimal? Huyen { get; set; }
        public decimal? Xa { get; set; }
        public string? RoleName { get; set; }
        public string? BranchName { get; set; }

        //VCB
        public int MaDv { get; set; }
        public string? TenDv { get; set; }
        public int? MaPhong { get; set; }
        public string? TenPhong { get; set; }
        public DateTime? NamSinh { get; set; }
        public int MaCb { get; set; }
        public string? MaJob { get; set; }
        public string? TenJob { get; set; }
        public int? MaChucVu { get; set; }
        public string? TenChucVu { get; set; }

        /// <summary>CHỈ CÓ Ở BẢN KHUNG — dựng đối tượng rỗng, không chạm DB.</summary>
        public MpUserFull() { }

        public MpUserFull(string username) : base(username)
        {
            if (UserType == UserType.NULL)
                return;

            RoleName = Roles.GetRoleName(RoleId);

            using var frontendContext = new FrontendContext();

            if (UserType == UserType.APP)
            {
                var app = frontendContext.MpAppUsers.FirstOrDefault(x => x.UserName == UserName);

                if (app == null)
                {
                    UserType = UserType.COMMON;
                    return;
                }

                Bid = app.Bid;
                Mid = app.Mid;
                Tid = app.Tid;
                BranchId = app.BranchId;
                BranchName = Branches.GetBranchName(app.BranchId);

                return;
            }

            if (UserType == UserType.BCA)
            {
                var bca = frontendContext.MpBcaUsers.FirstOrDefault(x => x.UserName == UserName);

                if (bca == null)
                {
                    UserType = UserType.COMMON;
                    return;
                }

                Tinh = bca.Tinh;
                Huyen = bca.Huyen;
                Xa = bca.Xa;

                return;
            }

            if (Roles.IsVcbRoles(RoleId))
            {
                var vcb = frontendContext.MpVcbUsers.FirstOrDefault(x => x.UserName == UserName);

                if (vcb == null)
                {
                    UserType = UserType.COMMON;
                    return;
                }

                BranchId = vcb.BranchId;
                BranchName = Branches.GetBranchName(vcb.BranchId);

                MaDv = vcb.MaDv ?? 0;
                TenDv = vcb.TenDv;
                MaPhong = vcb.MaPhong;
                TenPhong = vcb.TenPhong;
                NamSinh = vcb.NamSinh;
                MaCb = vcb.MaCb ?? 0;
                MaJob = vcb.MaJob;
                TenJob = vcb.TenJob;
                MaChucVu = vcb.MaChucVu;
                TenChucVu = vcb.TenChucVu;

                return;
            }

            if (Roles.IsShlxRoles(RoleId))
            {
                var shlx = frontendContext.MpShlxUsers.FirstOrDefault(x => x.UserName == UserName);

                if (shlx == null)
                {
                    UserType = UserType.COMMON;
                    return;
                }

                TerminalId = shlx.TerminalId;

                return;
            }

            if (Roles.IsApiRoles(RoleId))
            {
                var api = frontendContext.MpApiUsers.FirstOrDefault(x => x.UserName == UserName);

                if (api == null)
                {
                    UserType = UserType.COMMON;
                    return;
                }

                Bid = api.Bid;

                return;
            }
        }

        /// <summary>
        /// CHỈ CÓ Ở BẢN KHUNG. Dòng MP_VCB_USERS tương ứng, để InsertFull/SaveFull
        /// bên lớp cha ghi được cả bảng chi tiết.
        /// </summary>
        internal MpVcbUser ToVcbRow() => new()
        {
            UserName = UserName,
            BranchId = BranchId,
            MaDv = MaDv,
            TenDv = TenDv,
            MaPhong = MaPhong,
            TenPhong = TenPhong,
            NamSinh = NamSinh,
            MaCb = MaCb,
            MaJob = MaJob,
            TenJob = TenJob,
            MaChucVu = MaChucVu,
            TenChucVu = TenChucVu
        };
    }
}
