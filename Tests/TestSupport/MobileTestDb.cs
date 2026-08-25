using Microsoft.EntityFrameworkCore;
using VcbPortalApi.DbContext;
using VcbPortalApi.Models.MP;
using VcbPortalApi.Models.SSO;

namespace Tests.TestSupport
{
    /// <summary>
    /// Dựng FrontendContext / MerchantContext trên bộ nhớ cho test của
    /// MobilePartnerController, kèm các hàm đổ dữ liệu mẫu.
    ///
    /// Mỗi lần Create sinh một database riêng theo Guid nên test chạy song song
    /// không giẫm dữ liệu của nhau.
    /// </summary>
    public static class MobileTestDb
    {
        public static FrontendContext CreateFrontend() =>
            new(new DbContextOptionsBuilder<FrontendContext>()
                .UseInMemoryDatabase($"fe-{Guid.NewGuid()}").Options);

        public static MerchantContext CreateMerchant() =>
            new(new DbContextOptionsBuilder<MerchantContext>()
                .UseInMemoryDatabase($"mc-{Guid.NewGuid()}").Options);

        public static void SeedSession(FrontendContext db, string userName, string? sessionId = "session-1")
        {
            db.Add(new MpSession { UserName = userName, SessionId = sessionId });
            db.SaveChanges();
        }

        public static void SeedUsersCommon(
            FrontendContext db, string userName, string? email = "user@vcb.com.vn", decimal? roleId = null)
        {
            db.Add(new MpUsersCommon { UserName = userName, Email = email, RoleId = roleId });
            db.SaveChanges();
        }

        public static void SeedAppUser(
            FrontendContext db, string userName, decimal? bid = null, decimal? mid = null)
        {
            db.Add(new MpAppUser { Username = userName, Bid = bid, Mid = mid });
            db.SaveChanges();
        }

        /// <summary>
        /// Một dòng phân cấp bid → mid → tid. MobileHelper là static nên không fake
        /// được: muốn IsMidTidUnderBidAsync trả true thì phải seed đúng dòng tương ứng.
        /// </summary>
        public static void SeedTerminal(MerchantContext db, decimal bid, decimal mid, decimal tid)
        {
            db.Add(new MpTerminal
            {
                RowId = db.MpTerminals.Count() + 1,
                Bid = bid,
                Mid = mid,
                Tid = tid
            });
            db.SaveChanges();
        }
    }
}
