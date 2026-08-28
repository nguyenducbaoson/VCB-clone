// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — solution thật gửi mail qua SMTP. ĐỪNG chép đè.
// Giữ đúng chữ ký Send(to, subject, body) và quy ước "trả về OK là thành công",
// vì FepController.InsertNewVcbUser so đúng chuỗi "OK".
//
// KHÔNG CÓ TRONG BẢN THẬT: delegate Sender, để test thay chỗ gửi mail.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Tools
{
    public static class SendEmail
    {
        public static Func<string?, string, string, string> Sender { get; set; } = SmtpSend;

        public static string Send(string? to, string subject, string body) => Sender(to, subject, body);

        private static string SmtpSend(string? to, string subject, string body) =>
            throw new NotImplementedException("Ban khung khong gui mail that.");
    }
}
