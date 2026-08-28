using Microsoft.EntityFrameworkCore;
using VcbPortalApi.Models.MobileApp;
using VcbPortalApi.Models.MP;
using VcbPortalApi.Models.MP.User;
using VcbPortalApi.Models.MP.User.Detail;
using VcbPortalApi.Models.SSO;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution thật đã có FrontendContext và MerchantContext.
// Dựng lại đủ các DbSet mà MobilePartnerController.IssueSsoToken và constructor
// MpUserFull dùng. ĐỪNG chép đè.
//
// HAI CÁCH KHỞI TẠO, cả hai đều có trong code thật:
//   - new FrontendContext(options) — DI, luồng mobile/SSO.
//   - new FrontendContext()        — MpUserFull/FepController gọi trực tiếp trong
//     thân hàm. Bản thật tự đọc connection string; ở đây lấy từ AmbientOptions
//     để test trỏ được vào InMemory. Đây là CHỖ DUY NHẤT tôi thêm so với bản thật.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.DbContext
{
    public class FrontendContext : Microsoft.EntityFrameworkCore.DbContext
    {
        public FrontendContext(DbContextOptions<FrontendContext> options) : base(options) { }

        /// <summary>
        /// KHÔNG CÓ TRONG BẢN THẬT — hai thành viên dưới đây là khe duy nhất để test
        /// trỏ được DB vào <c>new FrontendContext()</c>, thứ mà MpUserFull,
        /// FepController và UserActionLogHelper gọi thẳng trong thân hàm.
        ///
        /// TẠM THỜI: đang chờ ảnh FrontendContext thật. Nếu bản thật là kiểu scaffold
        /// chuẩn (ctor rỗng + OnConfiguring có <c>if (!optionsBuilder.IsConfigured)</c>)
        /// thì bỏ hẳn được khe này, test không phải sửa dòng nào.
        /// </summary>
        public static DbContextOptions<FrontendContext>? AmbientOptions { get; set; }

        public FrontendContext() : base(
            AmbientOptions ?? throw new InvalidOperationException(
                "FILE KHUNG: chua gan FrontendContext.AmbientOptions."))
        { }

        public DbSet<MpSession> MpSessions => Set<MpSession>();
        
        public DbSet<MpAppUser> MpAppUsers => Set<MpAppUser>();

        public DbSet<MpUserCommon> MpUsersCommons => Set<MpUserCommon>();
        public DbSet<MpVcbUser> MpVcbUsers => Set<MpVcbUser>();
        public DbSet<MpBcaUser> MpBcaUsers => Set<MpBcaUser>();
        public DbSet<MpShlxUser> MpShlxUsers => Set<MpShlxUser>();
        public DbSet<MpApiUser> MpApiUsers => Set<MpApiUser>();
        public DbSet<MpAppUserActionLog> MpAppUserActionLogs => Set<MpAppUserActionLog>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<MpSession>().HasKey(x => x.UserName);
            
            b.Entity<MpAppUser>().HasKey(x => x.UserName);

            b.Entity<MpUserCommon>().HasKey(x => x.UserName);
            b.Entity<MpVcbUser>().HasKey(x => x.UserName);
            b.Entity<MpBcaUser>().HasKey(x => x.UserName);
            b.Entity<MpShlxUser>().HasKey(x => x.UserName);
            b.Entity<MpApiUser>().HasKey(x => x.UserName);
            b.Entity<MpAppUserActionLog>().HasKey(x => x.Id);

            // MpUserFull kế thừa MpUserCommon nhưng KHÔNG phải entity (nó ghép nhiều
            // bảng). Không bỏ dòng này thì EF coi nó là kiểu dẫn xuất TPH và đòi
            // ánh xạ cả cột bảng chi tiết.
            b.Ignore<MpUserFull>();
        }
    }

    public class MerchantContext : Microsoft.EntityFrameworkCore.DbContext
    {
        public MerchantContext(DbContextOptions<MerchantContext> options) : base(options) { }

        public DbSet<MpTerminal> MpTerminals => Set<MpTerminal>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<MpTerminal>().HasKey(x => x.RowId);
        }
    }
}
