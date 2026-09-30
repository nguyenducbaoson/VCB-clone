using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VcbPortalApi.Services.AcqHub;

namespace VcbPortalApi.Controllers.Frontend
{
    /// <summary>
    /// Cầu nối giữa màn hình "Cài đặt SHLX" của portal và ACQHUB.
    ///
    /// ROUTE: tiền tố "apimp" viết thẳng ở đây, KHÔNG dùng hằng.
    /// Ở Pilot tiền tố là "apivp" — nếu solution thật có hằng dùng chung cho
    /// tiền tố này thì thay chuỗi bên dưới bằng hằng đó, giống các controller
    /// sẵn có. Sai tiền tố thì Angular gọi vào sẽ ra 404.
    /// </summary>
    [Authorize(Policy = "MenuMpPolicy")]
    [ApiController]
    public class AcqHubShlxController(
        IAcqHubShlxClient acqHub,
        ILogger<AcqHubShlxController> logger) : ControllerCustom
    {
        [HttpPost("apimp/acqh/para/shlx")]
        public async Task<IActionResult> ShlxConfig(
            [FromBody] ShlxConfigRequest request, CancellationToken ct)
        {
            if (request?.items is null || request.items.Count == 0)
                return BadRequest(new { code = "01", message = "Danh sach terminal rong." });

            // Luồng này ghi cấu hình ở hệ thống ngoài, không rollback được,
            // nên phải truy ra được ai đã cài terminal nào.
            logger.LogInformation(
                "SHLX config: user={User} soLuong={Count} terminals={Terminals}",
                CurrentUserName,
                request.items.Count,
                string.Join(",", request.items.Select(x => x.terminalId)));

            // Trả NGUYÊN response ACQHUB — frontend đọc results[].success cho từng
            // terminal, vì một lô có thể thành công một phần.
            var res = await acqHub.ConfigAsync(request.items, ct);
            return Ok(res);
        }
    }
}
