using System.Net.Mail;
using System.Text;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — CHÉP NGUYÊN VĂN từ ảnh code thật. ĐỪNG chép đè.
// (Trước đây tôi để nhầm ở VcbPortalApi.Tools; bản thật nằm ở VcbPortalApi.Helpers.)
//
// CẢNH BÁO CHO NGƯỜI VIẾT TEST: hàm này gọi smtpClient.Send(mail) tới SMTP THẬT
// (info.vietcombank.com.vn:587). Không có khe nào để tắt. Test nào chạy tới đây là
// thật sự gửi mail — hoặc treo tới lúc timeout. InsertNewVcbUser gọi nó ngay sau
// InsertFull(), nên chỉ an toàn khi InsertFull() ném trước.
// ─────────────────────────────────────────────────────────────────────────────
namespace VcbPortalApi.Helpers
{
    public static class SendEmail
    {
        public static string Send(string to, string messages)
        {
            return Send(to, "Vietcombank Portal", messages);
        }

        public static string Send(string to, string subject, string messages, string? cc = null, string? bcc = null)
        {
            try
            {
                using var mail = new MailMessage();

                if (!string.IsNullOrEmpty(to))
                {
                    var toList = to.Split(";", StringSplitOptions.RemoveEmptyEntries).Distinct();

                    foreach (var address in toList)
                        mail.To.Add(address);
                }

                if (!string.IsNullOrEmpty(cc))
                {
                    var ccList = cc.Split(";", StringSplitOptions.RemoveEmptyEntries).Distinct();

                    foreach (var address in ccList)
                        mail.CC.Add(address);
                }

                if (!string.IsNullOrEmpty(bcc))
                {
                    var bccList = bcc.Split(";", StringSplitOptions.RemoveEmptyEntries).Distinct();

                    foreach (var address in bccList)
                        mail.Bcc.Add(address);
                }

                mail.From = new MailAddress("vcbportal@info.vietcombank.com.vn", "Vietcombank Portal (Automated email - Do not reply)");
                mail.Subject = subject;
                mail.IsBodyHtml = true;
                mail.DeliveryNotificationOptions = DeliveryNotificationOptions.Never;
                mail.BodyEncoding = Encoding.GetEncoding("utf-8");
                mail.Body = messages;

                using var smtpClient = new SmtpClient
                {
                    Host = "info.vietcombank.com.vn", // 192.168.198.52
                    UseDefaultCredentials = false,
                };

                smtpClient.EnableSsl = true;
                smtpClient.Port = 587;

                smtpClient.Send(mail);

                AppSettings.Logger.Warn($"{to}|{subject}|{messages}");
            }
            catch (Exception ex)
            {
                return ex.Message;
            }

            return "OK";
        }
    }
}
