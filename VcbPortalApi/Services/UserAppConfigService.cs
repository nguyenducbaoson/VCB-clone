using Microsoft.EntityFrameworkCore;
using VcbPortalApi.DbContext;

// FILE KHUNG - solution that da co service nay. DUNG chep de.
namespace VcbPortalApi.Services
{
    public sealed class UserAppConfigService(FrontendContext context, MerchantContext merchantContext)
    {
        public Task<string?> GetMinAppVersionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>("1.0.0");
    }
}
