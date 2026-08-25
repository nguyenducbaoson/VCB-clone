using Microsoft.EntityFrameworkCore;
using VcbPortalApi.DbContext;

// FILE KHUNG - solution that da co service nay. DUNG chep de.
namespace VcbPortalApi.Services
{
    public class UserAppConfigService(FrontendContext context)
    {
        public Task<string?> GetMinAppVersionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>("1.0.0");
    }
}
