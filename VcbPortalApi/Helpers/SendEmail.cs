using System.Net.Mail;
using System.Text;

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — chữ ký, thứ tự xử lý và quy ước trả về CHÉP NGUYÊN VĂN từ ảnh
// code thật. ĐỪNG chép đè.
//
// MỘT CHỖ KHÁC BẢN THẬT, cùng lý do với FrontendContext.OnConfiguring: bản khung
// KHÔNG thực hiện I/O ra ngoài. Bản thật mở SmtpClient tới
// info.vietcombank.com.vn:587 rồi smtpClient.Send(mail) — nguyên văn ở khối
// comment bên dưới. Giữ nguyên đoạn đó thì mỗi lần chạy test là một lần GỬI MAIL
// THẬT, hoặc treo tới lúc timeout (đó chính là test 22 giây bên solution thật).
//
// CẢNH BÁO CHO SOLUTION THẬT: hàm này không có công tắc nào để tắt. Test nào chạy
// tới đây là thật sự gửi mail. InsertNewVcbUser gọi nó ngay sau InsertFull().
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

                // ── BẢN THẬT có đúng đoạn này. Bản khung KHÔNG chạy để tránh gửi mail ──
                //
                // using var smtpClient = new SmtpClient
                // {
                //     Host = "info.vietcombank.com.vn", // 192.168.198.52
                //     UseDefaultCredentials = false,
                // };
                //
                // smtpClient.EnableSsl = true;
                // smtpClient.Port = 587;
                //
                // smtpClient.Send(mail);

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
