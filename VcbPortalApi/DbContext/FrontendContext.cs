using Microsoft.EntityFrameworkCore;
using VcbPortalApi.Models.MP;
using VcbPortalApi.Models.SSO;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution thật đã có FrontendContext và MerchantContext.
// Dựng lại đủ các DbSet mà MobilePartnerController.IssueSsoToken dùng. ĐỪNG chép đè.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.DbContext
{
    public class FrontendContext : Microsoft.EntityFrameworkCore.DbContext
    {
        public FrontendContext(DbContextOptions<FrontendContext> options) : base(options) { }

        public DbSet<MpSession> MpSessions => Set<MpSession>();
        public DbSet<MpUsersCommon> MpUsersCommons => Set<MpUsersCommon>();
        public DbSet<MpAppUser> MpAppUsers => Set<MpAppUser>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<MpSession>().HasKey(x => x.UserName);
            b.Entity<MpUsersCommon>().HasKey(x => x.UserName);
            b.Entity<MpAppUser>().HasKey(x => x.Username);
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
