using System.Net;
using System.Net.Mail;

namespace RealEstateProject.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public void SendEmail(string toEmail, string subject, string body)
        {
            var smtp = _config.GetSection("Smtp");

            MailMessage mail = new MailMessage();

            mail.From = new MailAddress(smtp["Username"]);
            mail.To.Add(toEmail);

            mail.Subject = subject;
            mail.Body = body;
            mail.IsBodyHtml = true;

            SmtpClient client = new SmtpClient(smtp["Host"])
            {
                Port = Convert.ToInt32(smtp["Port"]),
                Credentials = new NetworkCredential(
                    smtp["Username"],
                    smtp["Password"]
                ),
                EnableSsl = Convert.ToBoolean(smtp["EnableSsl"])
            };

            client.Send(mail);
        }

        public void SendEmailWithAttachment(string toEmail, string subject, string body, byte[] attachmentData, string attachmentName)
        {
            var smtp = _config.GetSection("Smtp");

            MailMessage mail = new MailMessage();

            mail.From = new MailAddress(smtp["Username"]);
            mail.To.Add(toEmail);

            mail.Subject = subject;
            mail.Body = body;
            mail.IsBodyHtml = true;

            if (attachmentData != null && attachmentData.Length > 0)
            {
                mail.Attachments.Add(new Attachment(new MemoryStream(attachmentData), attachmentName, "application/pdf"));
            }

            SmtpClient client = new SmtpClient(smtp["Host"])
            {
                Port = Convert.ToInt32(smtp["Port"]),
                Credentials = new NetworkCredential(
                    smtp["Username"],
                    smtp["Password"]
                ),
                EnableSsl = Convert.ToBoolean(smtp["EnableSsl"])
            };

            client.Send(mail);
        }
    }
}