using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using VcbPortalApi.DbContext.Oracle;
using VcbPortalApi.Helpers;
using VcbPortalApi.Models.Hcm;
using VcbPortalApi.Models.MP;
using VcbPortalApi.Models.MP.User;
using VcbPortalApi.Services.Redis;
using VcbPortalApi.StaticData.MP;
using VcbPortalApi.Tools;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG. TOÀN BỘ nội dung dưới đây CHÉP NGUYÊN VĂN từ ảnh code thật:
// đầu class, trọn hàm Authenticate, InsertNewVcbUser và CheckModified.
// Không sửa một dòng logic nào.
//
// DUY NHẤT một thứ dựng lại vì không có trong ảnh: GenerateUserHashData —
// giữ đúng chữ ký, test không khẳng định nội dung chuỗi băm.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Controllers.Frontend
{
    [Authorize(Policy = "MenuMpPolicy")]
    [ApiController]
    public class FepController(FrontendContext frontendContext, IConnectionMultiplexer redis) : ControllerCustom
    {
        private readonly IDatabase _redisDb = redis.GetDatabase();

#pragma warning disable
        [AllowAnonymous]
        [HttpPost(BuildSettings.FixedEndpoint + "/user/auth")]
        public async Task<IActionResult> Authenticate([FromBody] SignInPayload payload)
        {
            string userName = payload.UserName.KeepSafe().ToUpper().Trim();
            string password = payload.Password;

            if (BuildSettings.IsUat || BuildSettings.IsDev)
            {
                //không check captcha
            }
            else
            {
                var validateCaptcha = new SimpleCaptcha().Validate(payload.UserEnteredCaptchaCode, payload.CaptchaId);

                if (
                    //!BuildSettings.StressTestUsers.Contains(userName) &&
                    !validateCaptcha)
                {
                    return new ErrorMessage("wrong_captcha").Simplify();
                }
            }

            MpUserFull mpUserFull = new MpUserFull(userName);

            VCanBo? canbo = null;
            var maJob = "";

            if (mpUserFull.UserType == UserType.NULL || mpUserFull.UserType == UserType.VCB && mpUserFull.RoleId != Roles.RoleAdmin)
            {
                //kiểm tra có phải là vcb không
                var canbos = await _redisDb.GetByIndexAsync<VCanBo>("v_canbo", "TaiKhoanDomain", userName);

                if (canbos.Count == 0)
                {
                    if (mpUserFull.UserType == UserType.VCB)
                    {
                        mpUserFull.Status = "D";
                        mpUserFull.UserUpdate = AppSettings.SystemUser;
                        mpUserFull.SaveFull();
                    }

                    return HttpError.BaseError();
                }

                if (canbos.Count != 1)
                    return new ErrorMessage("Lỗi HCM").Simplify();

                canbo = canbos[0];
                maJob = canbo.MaJob.ToUpper().Trim();

                if (mpUserFull.UserType == UserType.NULL)
                {
                    //nếu chưa có user thì tạo mới user vcb
                    var inserted = InsertNewVcbUser(userName, mpUserFull, canbo);

                    if (inserted)
                        return Ok(new { message = "Đã gửi email thông tin đăng nhập" });
                    else
                        return HttpError.BaseError();
                }
                else
                {
                    //đã tồn tại user => cập nhật thông tin cho user khi có thay đổi
                    CheckModified(mpUserFull, canbo);
                }
            }

            if (mpUserFull.Status != "O")
                return HttpError.BaseError();

            bool check;

            if (BuildSettings.IsDev
                || BuildSettings.IsUat && !userName.Equals(AppSettings.AdminUsername)
                //|| BuildSettings.StressTest && BuildSettings.StressTestUsers.Contains(userName)
                )
            {
                //Không check password
                return Ok(new { accessToken = GenarateToken(new SessionData(HttpContext, mpUserFull, null), maJob, mpUserFull.TerminalId) });
            }

            //Check password
            check = Crypto.ValidateHash(userName + password, mpUserFull.Salt, mpUserFull.Password);

            if (!check)
            {
                UserActionLogHelper.TryLog(
                    UserActionLogTypes.Action.LoginMobile,
                    UserActionLogTypes.ResultCode.WrongPassword,
                    userName,
                    message: "Wrong password",
                    requestIp: GetRealIp(),
                    source: UserActionLogTypes.Source.Web);

                return HttpError.BaseError();
            }

            if (!Crypto.IsStrongPassword(password))
                return new ErrorMessage($"Mật khẩu không an toàn. Xin vui lòng đặt lại mật khẩu mới.").Simplify();

            return Ok(new { accessToken = GenarateToken(new SessionData(HttpContext, mpUserFull, null), maJob, mpUserFull.TerminalId) });
        }


        private static bool InsertNewVcbUser(string userName, MpUserFull mpUserFull, VCanBo canbo)
        {
            var newPassword = Crypto.GeneratePassword();

            var salt = Crypto.GenerateSalt();
            var hash = Crypto.GenerateHash(userName + newPassword, salt);

            var role = Roles.RoleNghiepVu;

            if (string.IsNullOrEmpty(canbo.MaJob))
                return false;

            var checkJd = AppSettings.JdWhiteList.Any(x => x == canbo.MaJob.ToUpper().Trim());

            if (checkJd)
                role = canbo.MaChucVu == null || canbo.MaChucVu == 0 ? Roles.RoleTtv : Roles.RoleKsv;

            var defaultUDataHash = Crypto.GenerateHash(
                GenerateUserHashData(userName, role),
                salt);

            var avatar = canbo.SamAccountName != null ? $"images/thumbnail/{userName}.jpeg" : null;

            mpUserFull.UserName = userName;
            mpUserFull.RoleId = role;
            mpUserFull.FullName = canbo.HoTen;
            mpUserFull.BranchId = canbo.MaCn;
            mpUserFull.Status = "O";
            mpUserFull.Email = canbo.Email?.KeepSafe().ToLower();
            mpUserFull.Mobile = canbo.SdtDiDong?.KeepNumber().Trunc(10);
            mpUserFull.Avatar = avatar;
            mpUserFull.UHash = Convert.ToBase64String(defaultUDataHash);
            mpUserFull.Password = Convert.ToBase64String(hash);
            mpUserFull.Salt = Convert.ToBase64String(salt);
            mpUserFull.UserUpdate = AppSettings.SystemUser;

            mpUserFull.MaDv = canbo.MaDv;
            mpUserFull.TenDv = canbo.TenDv;
            mpUserFull.MaPhong = canbo.MaPhong;
            mpUserFull.TenPhong = canbo.TenPhong;
            mpUserFull.NamSinh = canbo.NamSinh;
            mpUserFull.MaCb = canbo.MaCb;
            mpUserFull.MaJob = canbo.MaJob;
            mpUserFull.TenJob = canbo.TenJob;
            mpUserFull.MaChucVu = canbo.MaChucVu;
            mpUserFull.TenChucVu = canbo.TenChucVu;

            var saved = mpUserFull.InsertFull();

            if (saved < 1)
                return false;

            //không cho đăng nhập luôn mà gửi mật khẩu về email
            var send = SendEmail.Send(
                mpUserFull.Email,
                $"Thông tin tài khoản truy cập VCB Portal - {DateTime.Now:yyyyMMddHHmmss}",
                $"------------------------------------------------------------<br/>" +
                $"Tên đăng nhập : {mpUserFull.UserName}<br/>" +
                $"Mật khẩu : {newPassword}<br/>" +
                $"------------------------------------------------------------<br/>" +
                $"Website :<br/>" +
                $"Đơn vị chấp nhận thanh toán : <a href='https://mp.vietcombank.com.vn/mp/'>https://mp.vietcombank.com.vn/mp/</a><br/>" +
                $"Bộ Công An / Sát hạch lái xe : <a href='https://mp.vietcombank.com.vn/bca/'>https://mp.vietcombank.com.vn/bca/</a><br/>" +
                $"Pilot / Dự phòng : <a href='https://mp.vietcombank.com.vn/vp/'>https://mp.vietcombank.com.vn/vp/</a><br/>"
                );

            if (send == "OK")
                return true;

            return false;
        }

        private static void CheckModified(MpUserFull mpUserFull, VCanBo canbo)
        {
            if (string.IsNullOrEmpty(mpUserFull.MaJob) || mpUserFull.MaJob != canbo.MaJob)
            {
                mpUserFull.BranchId = canbo.MaCn;

                mpUserFull.MaDv = canbo.MaDv;
                mpUserFull.TenDv = canbo.TenDv;

                mpUserFull.MaPhong = canbo.MaPhong;
                mpUserFull.TenPhong = canbo.TenPhong;

                mpUserFull.FullName = canbo.HoTen;

                var mobile = canbo.SdtDiDong?.KeepNumber().Trunc(10);
                mpUserFull.Mobile = mobile;

                mpUserFull.Email = canbo.Email.KeepEmailAddressSafe().ToLower();
                mpUserFull.NamSinh = canbo.NamSinh;
                mpUserFull.MaCb = canbo.MaCb;
                mpUserFull.MaJob = canbo.MaJob;
                mpUserFull.TenJob = canbo.TenJob;
                mpUserFull.MaChucVu = canbo.MaChucVu;
                mpUserFull.TenChucVu = canbo.TenChucVu;

                //avatar thumb
                if (canbo.SamAccountName != null && (mpUserFull.Avatar == null || !mpUserFull.Avatar.Contains(mpUserFull.UserName)))
                    mpUserFull.Avatar = $"images/thumbnail/{mpUserFull.UserName}.jpeg";

                mpUserFull.UserUpdate = AppSettings.SystemUser;

                mpUserFull.SaveFull();
            }
        }

#pragma warning restore

        /// <summary>DỰNG LẠI — không có trong ảnh. Xem ghi chú ở đầu file.</summary>
        private static string GenerateUserHashData(string userName, decimal role) =>
            $"{userName}|{role}";
    }
}
