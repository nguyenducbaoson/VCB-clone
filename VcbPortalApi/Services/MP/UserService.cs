using Microsoft.EntityFrameworkCore;
using VcbPortalApi.DbContext.Oracle;
using VcbPortalApi.Models.MP.User;
using VcbPortalApi.Models.MP.User.Detail;
using VcbPortalApi.StaticData.MP;
using VcbPortalApi.Tools;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution thật ĐÃ CÓ userService với đúng hai hàm này; ApiCommon
// .CreateUsers gọi `userService.GetUser(userName, ct)` và
// `userService.CreateNewUser(newUser, password, cancellationToken: ct)`.
// ĐỪNG chép đè. Ở đây dựng lại đúng CHỮ KÝ để AcqHubShlxController biên dịch và
// test chạy được mà không chạm Oracle.
//
// Phần thân là PHỎNG ĐOÁN. Chỗ duy nhất code SHLX phụ thuộc vào là hợp đồng:
//   - GetUser trả null khi chưa có user  → "đã tồn tại chưa"
//   - CreateNewUser trả số dòng ghi được → > 0 là tạo xong
// Bản thật làm gì bên trong (sinh salt, ghi log, gửi mail) không ảnh hưởng.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Services.MP
{
    public interface IUserService
    {
        /// <summary>Trả null nếu chưa có user tên đó.</summary>
        Task<MpUserCommon?> GetUser(string userName, CancellationToken cancellationToken = default);

        /// <summary>Thêm user mới. Trả về số dòng ghi được (0 = không tạo được).</summary>
        Task<int> CreateNewUser(
            MpUserFull user,
            string? password = null,
            CancellationToken cancellationToken = default);
    }

    public sealed class UserService(FrontendContext frontendContext) : IUserService
    {
        public async Task<MpUserCommon?> GetUser(
            string userName, CancellationToken cancellationToken = default) =>
            await frontendContext.MpUserCommons
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserName == userName, cancellationToken);

        public async Task<int> CreateNewUser(
            MpUserFull user,
            string? password = null,
            CancellationToken cancellationToken = default)
        {
            // Mật khẩu KHÔNG bao giờ lưu nguyên văn: sinh salt riêng cho từng user rồi
            // lưu hash. Không truyền mật khẩu thì sinh ngẫu nhiên — user phải đi luồng
            // quên mật khẩu, đúng như SetPrivateData(null) của bản thật.
            var salt = Crypto.GenerateSalt();

            user.Salt = Convert.ToHexString(salt);
            user.Password = Convert.ToHexString(
                Crypto.GenerateHash(password ?? Crypto.GeneratePassword(), salt));
            user.UHash = Crypto.Sha256EncryptString(user.UserName);

            frontendContext.MpUserCommons.Add(user.ToCommonRow());

            if (Roles.IsShlxRoles(user.RoleId))
            {
                frontendContext.MpShlxUsers.Add(new MpShlxUser
                {
                    UserName = user.UserName,
                    TerminalId = user.TerminalId,
                });
            }

            return await frontendContext.SaveChangesAsync(cancellationToken);
        }
    }
}
