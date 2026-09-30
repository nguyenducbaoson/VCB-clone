using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using VcbPortalApi.Controllers.Api;
using VcbPortalApi.Models.MP.User;
using VcbPortalApi.Services.AcqHub;
using VcbPortalApi.Services.MP;
using VcbPortalApi.StaticData.MP;
using VcbPortalApi.Tools;

namespace VcbPortalApi.Controllers.Frontend
{
    /// <summary>
    /// Cầu nối giữa màn hình "Cài đặt SHLX" của portal và ACQHUB, kèm việc tạo
    /// user portal cho những terminal ACQHUB đã cài xong.
    ///
    /// Kế thừa <see cref="ApiCommon"/> để dùng chính <c>CreateUsers</c> đang chạy
    /// cho các luồng khác — một chỗ ghi user cho cả hệ thống.
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
        IUserService userService,
        ILogger<AcqHubShlxController> logger) : ApiCommon(userService)
    {
        [HttpPost("apimp/acqh/para/shlx")]
        public async Task<IActionResult> ShlxConfig(
            [FromBody] ShlxUploadRequest request, CancellationToken ct)
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

            // ToConfigItem() bỏ lại 5 cột user — ACQHUB chỉ nhận đúng 6 cột nó khai.
            var res = await acqHub.ConfigAsync(
                request.items.Select(x => x.ToConfigItem()).ToList(), ct);

            // ACQHUB TRƯỚC, user portal SAU. Ngược lại là tạo login cho một terminal
            // có thể chưa hề được cài — mà lỗi đó không tự lộ ra, user vẫn đăng nhập
            // được, chỉ không có dữ liệu.
            res.users = await TaoUserChoTerminalDaCai(request.items, res.results, ct);

            // Trả NGUYÊN response ACQHUB — frontend đọc results[].success cho từng
            // terminal, vì một lô có thể thành công một phần.
            return Ok(res);
        }

        /// <summary>
        /// Với mỗi terminal ACQHUB đã cài xong: chưa có user thì tạo bằng
        /// <c>ApiCommon.CreateUsers</c>, có rồi thì bỏ qua.
        ///
        /// GỌI TỪNG DÒNG MỘT chứ không đưa cả mảng vào CreateUsers. Lý do: nó chỉ
        /// trả về <c>{ total, success }</c> — một con số — và bỏ qua mọi dòng hỏng
        /// một cách lặng lẽ. Đưa 250 dòng vào rồi nhận về "243" thì không biết 7
        /// dòng nào trượt, mà lúc đó ACQHUB ĐÃ cài xong cả 250 terminal, không
        /// rollback được. Gọi lẻ thì mỗi dòng có kết quả riêng, màn hình chỉ được
        /// ra terminal nào đã cài mà chưa có user.
        ///
        /// Chi phí thêm gần như bằng không: CreateUsers duyệt mảng một vòng, phần
        /// chạm DB vẫn đúng chừng ấy lần.
        /// </summary>
        private async Task<List<ShlxUserResult>> TaoUserChoTerminalDaCai(
            List<ShlxUploadItem> items, List<ShlxConfigResult> results, CancellationToken ct)
        {
            var daCai = results
                .Where(x => x.success)
                .Select(x => x.terminal_id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var ketQua = new List<ShlxUserResult>();

            foreach (var item in items)
            {
                // ACQHUB từ chối terminal này thì không có gì để gắn user vào.
                if (!daCai.Contains(item.terminalId))
                    continue;

                var ketQuaDong = new ShlxUserResult { terminal_id = item.terminalId };
                ketQua.Add(ketQuaDong);

                if (string.IsNullOrWhiteSpace(item.userName))
                {
                    ketQuaDong.message = "Thieu USER_NAME, khong tao duoc user.";
                    continue;
                }

                // CreateUsers tự chuẩn hoá lại y hệt dòng này. Tính trước ở đây chỉ
                // để BÁO CÁO ra đúng tên user mà nó sẽ ghi.
                var userName = item.userName.KeepSafe().ToUpper();
                ketQuaDong.user_name = userName;

                var roleId = item.roleId ?? Roles.RoleShlx;

                // MÀN HÌNH NÀY CHỈ ĐƯỢC TẠO USER SHLX.
                // ROLE_ID là một ô Excel do người dùng gõ. Đưa thẳng vào CreateUsers
                // — nơi chấp nhận cả role VCB, BCA, Merchant — thì ai có quyền cài
                // SHLX cũng tự tạo được tài khoản quản trị (RoleId 19) chỉ bằng cách
                // sửa một ô trong file.
                if (!Roles.IsShlxRoles(roleId))
                {
                    ketQuaDong.message =
                        $"ROLE_ID {roleId} khong phai role SHLX (28, 29). Bo qua.";

                    logger.LogWarning(
                        "SHLX config: user={User} thu tao user {UserName} voi ROLE_ID {RoleId} ngoai nhom SHLX.",
                        CurrentUserName, userName, roleId);

                    continue;
                }

                try
                {
                    // CreateUsers cũng kiểm điều này, nhưng nó `continue` lặng lẽ —
                    // "đã có sẵn" và "ghi hỏng" đều ra success = 0. Hỏi trước để
                    // phân biệt được hai thứ đó trên màn hình.
                    if (await userService.GetUser(userName, ct) != null)
                    {
                        ketQuaDong.message = "User da ton tai, giu nguyen.";
                        continue;
                    }

                    var payload = new ApiUserPayload
                    {
                        UserName = userName,
                        RoleId = roleId,
                        FullName = item.fullName,
                        Email = item.email,
                        Mobile = item.mobile,

                        // Company CHÍNH LÀ TerminalId — nhánh SHLX của CreateUsers
                        // viết `TerminalId = user.Company.ToUpper().Trunc(10).Trim()`.
                        // Để null là nó `continue` và không tạo gì cả.
                        Company = item.terminalId,

                        // Password = null: để CreateUsers tự sinh "Shlx@yyMMdd".
                        // Đặt ở đây là hai chỗ cùng quyết định một mật khẩu.
                        Password = null,
                    };

                    var soTao = SoUserDaTao(await CreateUsers([payload], ct));

                    ketQuaDong.created = soTao > 0;
                    ketQuaDong.message = soTao > 0 ? "" : "CreateUsers khong tao user nao.";
                }
                catch (Exception ex)
                {
                    // ACQHUB đã ghi xong terminal này rồi — ném ra đây là bỏ dở cả
                    // những dòng phía sau mà không ai biết đã tới đâu.
                    logger.LogError(ex,
                        "SHLX config: tao user {UserName} cho terminal {TerminalId} that bai.",
                        userName, item.terminalId);

                    ketQuaDong.message = "Loi khi tao user, xem log.";
                }
            }

            return ketQua;
        }

        /// <summary>
        /// Móc thân JSON ra khỏi thứ SendAcqRequest trả về.
        ///
        /// BẢN THẬT TRẢ <see cref="ContentResult"/> — <c>Content(json, "application/json")</c>
        /// — KHÔNG phải ObjectResult. Đoán nhầm kiểu thì <c>as ObjectResult</c> ra null
        /// và cả phần tạo user bị bỏ qua LẶNG LẼ: không lỗi, không log, response chỉ
        /// thiếu khoá "users". Nhận cả hai kiểu để đổi bên kia không làm hỏng bên này.
        ///
        /// Trả null khi không đọc được — bên gọi trả nguyên response, vì ACQHUB đã
        /// ghi xong rồi, không được nuốt kết quả cài đặt.
        /// </summary>
        internal static JObject? DocThanJson(IActionResult result)
        {
            object? goc = result switch
            {
                ContentResult c => c.Content,
                ObjectResult o => o.Value,
                _ => null,
            };

            if (goc is null) return null;

            try
            {
                return goc is string s ? JObject.Parse(s) : JObject.FromObject(goc);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gắn body đã thêm "users" trở lại, GIỮ NGUYÊN kiểu ban đầu. Với ContentResult
        /// thì sửa thẳng Content của object cũ — dựng Content() mới là mất ContentType
        /// và StatusCode, frontend nhận khác đi mà không có gì báo.
        /// </summary>
        internal static IActionResult TraLaiDungKieu(IActionResult goc, JObject body)
        {
            var json = body.ToString(Newtonsoft.Json.Formatting.None);

            if (goc is ContentResult c)
            {
                c.Content = json;
                return c;
            }

            return new OkObjectResult(
                (goc as ObjectResult)?.Value is string ? json : (object)body);
        }

        /// <summary>
        /// Đọc <c>success</c> ra khỏi <c>Ok(new { total, success })</c> của CreateUsers.
        ///
        /// PHẢI có StringComparison.OrdinalIgnoreCase. Cả reflection
        /// <c>GetProperty("success")</c> lẫn <c>JObject["success"]</c> đều khớp tên
        /// ĐÚNG CHỮ HOA THƯỜNG rồi trả null lặng lẽ khi trượt — hôm nào ai đó sửa
        /// CreateUsers thành <c>Ok(new { total, Success })</c> thì hàm này trả 0 cho
        /// mọi dòng, màn hình báo "chưa tạo được user" hàng loạt trong khi user đã
        /// nằm trong DB. Đã dựng lại đúng tình huống đó để kiểm, xem
        /// AcqHubShlxControllerTests.Doc_duoc_success_du_CreateUsers_doi_chu_hoa.
        ///
        /// Chắc hơn nữa là đổi CreateUsers trả về một lớp có tên thay vì kiểu vô
        /// danh — nhưng nó là hàm dùng chung, sửa thì phải rà hết chỗ gọi.
        /// </summary>
        internal static int SoUserDaTao(IActionResult result) =>
            (result as ObjectResult)?.Value is { } value
                ? JObject.FromObject(value)
                    .GetValue("success", StringComparison.OrdinalIgnoreCase)
                    ?.Value<int>() ?? 0
                : 0;
    }
}
