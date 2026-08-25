using Microsoft.EntityFrameworkCore;
using VcbPortalApi.Models.SSO;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution thật đã có DbContext riêng trong thư mục DbContext/.
// Bản này chỉ khai đủ 3 DbSet mà luồng SSO/trạng thái partner dùng tới, để repo
// build và test chạy được. ĐỪNG chép đè lên DbContext thật; thay vào đó kiểm tra
// xem 3 entity dưới đây đã được khai trong DbContext thật chưa.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.DbContext
{
    public class VcbPortalDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        public VcbPortalDbContext(DbContextOptions<VcbPortalDbContext> options) : base(options) { }

        public DbSet<MpAppUser> MpAppUsers => Set<MpAppUser>();
        public DbSet<MpAppPartnerCardReg> MpAppPartnerCardRegs => Set<MpAppPartnerCardReg>();
        public DbSet<MpSsoLog> MpSsoLogs => Set<MpSsoLog>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            // Không gọi ToTable() ở đây: ToTable thuộc EF Core Relational, mà bản khung
            // chỉ tham chiếu provider InMemory. Ánh xạ tên bảng Oracle
            // (MP_APP_USERS, MP_APP_PARTNER_CARD_REG, MP_SSO_LOG) là việc của DbContext thật.
            b.Entity<MpAppUser>().HasKey(x => x.Username);

            // Bảng thật không có khoá chính — một username nhiều dòng là bình thường.
            // Nhưng entity keyless thì EF không cho Add(), nên dùng khoá giả RowId.
            b.Entity<MpAppPartnerCardReg>().HasKey(x => x.RowId);

            b.Entity<MpSsoLog>().HasKey(x => x.Id);
        }
    }
}
