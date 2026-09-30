using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        ILogger<AcqHubShlxController> logger) : ControllerCustom
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
        /// Với mỗi terminal ACQHUB đã cài xong: chưa có user thì tạo, có rồi thì bỏ
        /// qua. Mỗi dòng một kết quả để màn hình chỉ ra được dòng nào hỏng vì sao.
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

                var userName = item.userName.KeepSafe().ToUpper();
                ketQuaDong.user_name = userName;

                var roleId = item.roleId ?? Roles.RoleShlx;

                // MÀN HÌNH NÀY CHỈ ĐƯỢC TẠO USER SHLX.
                // ROLE_ID là một ô Excel do người dùng gõ. Nhận thẳng nó rồi đẩy qua
                // ApiCommon.CreateUsers — nơi chấp nhận cả role VCB, BCA, Merchant —
                // thì ai có quyền cài SHLX cũng tự tạo được tài khoản quản trị
                // (RoleId 19) chỉ bằng cách sửa một ô trong file.
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
                    // "Đã tồn tại chưa" — giống hệt chốt `if (mpUser != null) continue;`
                    // của ApiCommon.CreateUsers.
                    if (await userService.GetUser(userName, ct) != null)
                    {
                        ketQuaDong.message = "User da ton tai, giu nguyen.";
                        continue;
                    }

                    var newUser = new MpUserFull
                    {
                        UserName = userName,
                        RoleId = roleId,
                        FullName = string.IsNullOrWhiteSpace(item.fullName)
                            ? userName
                            : item.fullName.Trim(),
                        BranchId = 1,
                        Status = "0",
                        Email = item.email?.KeepSafe().ToLower(),
                        Mobile = item.mobile?.KeepNumber().PadLeft(10, '0').Trunc(10),
                        Avatar = null,
                        TerminalId = item.terminalId.ToUpper().Trunc(10).Trim(),
                        UserUpdate = AppSettings.AdminUsername,
                    };

                    var ghiDuoc = await userService.CreateNewUser(newUser, MatKhauMacDinh(), ct);

                    ketQuaDong.created = ghiDuoc > 0;
                    ketQuaDong.message = ghiDuoc > 0 ? "" : "Khong ghi duoc user.";
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
        /// Mật khẩu ban đầu, đúng công thức nhánh SHLX của ApiCommon.CreateUsers.
        ///
        /// CÔNG THỨC CỐ ĐỊNH, KHÔNG PHẢI BÍ MẬT: mọi user tạo trong cùng một ngày
        /// đều trùng mật khẩu, và ai đọc được dòng này cũng đoán ra. Nó chỉ dùng
        /// được vì portal bắt đổi mật khẩu ở lần đăng nhập đầu — nếu không bắt thì
        /// đây là lỗ hổng, không phải tiện lợi.
        /// </summary>
        internal static string MatKhauMacDinh() => $"Shlx@{DateTime.Now:yyMMdd}";
    }
}
