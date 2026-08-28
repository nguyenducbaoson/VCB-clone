using Microsoft.EntityFrameworkCore;
using VcbPortalApi.Models.MobileApp;
using VcbPortalApi.Models.MP;
using VcbPortalApi.Models.MP.User;
using VcbPortalApi.Models.MP.User.Detail;
using VcbPortalApi.Models.SSO;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — dựng theo đúng hình dạng bản thật: namespace ...DbContext.Oracle,
// class partial, DbSet virtual, hai constructor, OnConfiguring có chốt
// `if (!optionsBuilder.IsConfigured)` rồi chọn connection string theo môi trường.
// Chỉ giữ những DbSet mà code trong repo này dùng tới. ĐỪNG chép đè.
//
// HỆ QUẢ CHO TEST — đây là kết luận cuối, không còn phỏng đoán:
// `new FrontendContext()` không có options nên OnConfiguring LUÔN vào nhánh đọc
// connection string. Không có chỗ nào chèn DB test vào. Vì vậy mọi thứ gọi
// `new FrontendContext()` trong thân hàm — MpUserFull(string), InsertFull,
// SaveFull, UserActionLogHelper.TryLog — KHÔNG unit test được.
// Chỉ những chỗ nhận FrontendContext qua tham số mới test được.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.DbContext.Oracle
{
    public partial class FrontendContext : Microsoft.EntityFrameworkCore.DbContext
    {
        //Users
        public virtual DbSet<MpUserCommon> MpUserCommons { get; set; } = null!;
        public virtual DbSet<MpAppUser> MpAppUsers { get; set; } = null!;             // ROLEID 1 2 3
        public virtual DbSet<MpBcaUser> MpBcaUsers { get; set; } = null!;             // ROLEID 21 22 23 24 25
        public virtual DbSet<MpShlxUser> MpShlxUsers { get; set; } = null!;           // ROLEID 28 29
        public virtual DbSet<MpApiUser> MpApiUsers { get; set; } = null!;             // ROLEID 41
        public virtual DbSet<MpVcbUser> MpVcbUsers { get; set; } = null!;             // ROLEID 11,12,13,19,31,32,35,51,52,53,54

        public virtual DbSet<MpSession> MpSessions { get; set; } = null!;
        public virtual DbSet<MpAppUserActionLog> MpAppUserActionLogs { get; set; } = null!;

        public FrontendContext() { }

        public FrontendContext(DbContextOptions<FrontendContext> options)
            : base(options)
        { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Bản thật: AppSettings.FrontDb.ConStr, đổi sang UatDb/PilotDb theo
                // BuildSettings. Bản khung không có Oracle nên ném rõ ràng.
                throw new InvalidOperationException("FILE KHUNG: khong co connection string that.");
            }
        }

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
            // bảng). Không bỏ dòng này thì EF coi nó là kiểu dẫn xuất TPH.
            b.Ignore<MpUserFull>();
        }
    }

    /// <summary>FILE KHUNG — cùng namespace với FrontendContext ở bản thật.</summary>
    public partial class MerchantContext : Microsoft.EntityFrameworkCore.DbContext
    {
        public virtual DbSet<MpTerminal> MpTerminals { get; set; } = null!;

        public MerchantContext() { }

        public MerchantContext(DbContextOptions<MerchantContext> options) : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
                throw new InvalidOperationException("FILE KHUNG: khong co connection string that.");
        }

        protected override void OnModelCreating(ModelBuilder b) =>
            b.Entity<MpTerminal>().HasKey(x => x.RowId);
    }
}
