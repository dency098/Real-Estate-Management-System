using System.Threading.Tasks;

namespace RealEstateProject.Services;

public interface IEmailSender
{
    Task SendEmailAsync(string toEmail, string subject, string htmlBody);
}