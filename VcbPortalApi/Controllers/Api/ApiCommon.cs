using Microsoft.AspNetCore.Mvc;
using VcbPortalApi.Models.MP.User;
using VcbPortalApi.Services.MP;
using VcbPortalApi.StaticData.MP;
using VcbPortalApi.Tools;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG. ĐỪNG chép đè — solution thật đã có ApiCommon.
//
// NHÁNH SHLX (Roles.IsShlxRoles) CHÉP NGUYÊN VĂN từ ảnh code thật, dòng 576→603.
// Đó là nhánh duy nhất luồng cài đặt SHLX đi qua, nên nó phải đúng từng chữ.
//
// BA NHÁNH CÒN LẠI DỰNG LẠI KHÔNG ĐẦY ĐỦ — thiếu Canbos.GetCanBo (nhánh VCB) và
// BMT.CheckStaticBid/CheckStaticMid (nhánh Merchant), hai thứ chạm DB mà repo này
// không có. Chúng vẫn GỌI CreateNewUser thật, và đó là điều duy nhất cần đúng ở
// đây: nếu bỏ chúng thành no-op thì bài test "ROLE_ID ngoài nhóm SHLX bị từ chối"
// sẽ xanh kể cả khi chốt chặn bị gỡ — tức là một bài test vô nghĩa.
//
// Khung cũng giữ đúng hai điều dễ bỏ sót của bản thật:
//   1. Các nhánh là if RỜI NHAU, không phải else-if.
//   2. Mọi lần bỏ qua đều `continue` LẶNG LẼ — không báo gì ra ngoài. Người gọi
//      chỉ nhận được `success` là một con số, không biết ai bị bỏ và vì sao.
//      Đây chính là lý do AcqHubShlxController phải tự lọc TRƯỚC khi gọi.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Controllers.Api
{
    public class ApiCommon(IUserService userService) : ControllerCustom
    {
        protected async Task<IActionResult> CreateUsers(
            ApiUserPayload[] users, CancellationToken cancellationToken)
        {
            var success = 0;

            foreach (var user in users)
            {
                var userName = user.UserName.KeepSafe().ToUpper();

                var mpUser = await userService.GetUser(userName, cancellationToken);

                if (mpUser != null)
                    continue;

                //Add new
                var roleId = user.RoleId ?? Roles.RoleBcaTao;

                if (!Roles.IsVcbRoles(roleId)
                    && !Roles.IsMerchantRoles(roleId)
                    && !Roles.IsBcaRoles(roleId)
                    && !Roles.IsShlxRoles(roleId)
                   )
                    continue;

                if (Roles.IsVcbRoles(roleId))
                {
                    // BẢN THẬT tra Canbos.GetCanBo(userName) và bỏ qua nếu không có,
                    // rồi lấy HoTen/MaCn/Email/SdtDiDong từ đó. Repo này không có
                    // bảng cán bộ nên dựng thẳng từ payload.
                    var newUser = new MpUserFull
                    {
                        UserName = userName,
                        RoleId = roleId,
                        FullName = user.FullName ?? userName,
                        BranchId = user.BranchId ?? 1,
                        Status = "0",
                        Email = user.Email?.KeepSafe().ToLower(),
                        Mobile = user.Mobile?.KeepNumber().PadLeft(10, '0').Trunc(10),
                        UserUpdate = AppSettings.AdminUsername
                    };

                    if (await userService.CreateNewUser(
                            newUser, cancellationToken: cancellationToken) > 0)
                        success++;
                }

                if (Roles.IsBcaNewRoles(roleId))
                {
                    //BCA
                    if (user.Tinh == null)
                        continue;

                    var password = user.Password ?? $"bca@{DateTime.Now.Year}";
                    var fullName = userName;

                    if (!string.IsNullOrEmpty(user.FullName))
                        fullName = user.FullName.Trim();

                    var tinh = (decimal)user.Tinh;
                    var huyen = user.Huyen ?? 0;
                    var xa = user.Xa ?? 0;

                    var newUser = new MpUserFull
                    {
                        UserName = userName,
                        RoleId = roleId,
                        FullName = fullName,
                        BranchId = 1,
                        Status = "0",
                        Email = user.Email?.KeepSafe().ToLower(),
                        Avatar = "images/logo/bca.png",
                        Tinh = tinh,
                        Huyen = huyen,
                        Xa = xa,

                        UserUpdate = AppSettings.AdminUsername
                    };

                    if (await userService.CreateNewUser(
                            newUser, password, cancellationToken: cancellationToken) > 0)
                        success++;
                }

                if (Roles.IsMerchantRoles(roleId))
                {
                    var password = user.Password ?? $"vcbp@{DateTime.Now.Year}";

                    var fullName = user.FullName ?? "";

                    var bid = user.Bid ?? -1;
                    var mid = user.Mid ?? -1;

                    // BẢN THẬT dùng BMT.CheckStaticBid/CheckStaticMid để suy branchId
                    // và bỏ qua dòng nếu không tra được. Repo này không có bảng đó.
                    decimal branchId = user.BranchId ?? -1;

                    var newUser = new MpUserFull
                    {
                        UserName = userName,

                        RoleId = roleId,
                        FullName = fullName.Trim().ToUpper(),
                        BranchId = branchId,
                        Status = "0",
                        Email = user.Email?.KeepSafe().ToLower(),
                        Bid = bid,
                        Mid = mid,

                        UserUpdate = AppSettings.AdminUsername
                    };

                    if (await userService.CreateNewUser(
                            newUser, password, cancellationToken: cancellationToken) > 0)
                        success++;
                }

                if (Roles.IsShlxRoles(roleId))
                {
                    if (user.Company == null)
                        continue;

                    var password = user.Password ?? $"Shlx@{DateTime.Now:yyMMdd}";
                    var fullName = userName;

                    if (!string.IsNullOrEmpty(user.FullName))
                        fullName = user.FullName.Trim();

                    var newUser = new MpUserFull
                    {
                        UserName = userName,
                        RoleId = roleId,
                        FullName = fullName,
                        BranchId = 1,
                        Status = "0",
                        Email = user.Email?.KeepSafe().ToLower(),
                        Mobile = user.Mobile?.KeepNumber().PadLeft(10, '0').Trunc(10),
                        Avatar = null,
                        TerminalId = user.Company.ToUpper().Trunc(10).Trim(),
                        UserUpdate = AppSettings.AdminUsername
                    };

                    if (await userService.CreateNewUser(
                            newUser, password, cancellationToken: cancellationToken) > 0)
                        success++;
                }
            }

            return Ok(new { total = users.Length, success });
        }
    }
}
