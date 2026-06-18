using Microsoft.AspNetCore.Mvc;
using RealEstateProject.Models;

public class NotificationController : Controller
{
    private readonly RealEstateProjectContext _context;

    public NotificationController(RealEstateProjectContext context)
    {
        _context = context;
    }

    [HttpPost]
    public IActionResult Respond(int notificationId, string response)
    {
        var notification = _context.Notifications
            .FirstOrDefault(n => n.NotificationId == notificationId);

        if (notification != null)
        {
            notification.Status = response;
            notification.IsRead = true;

            var enquiry = _context.Enquiries
                .FirstOrDefault(e => e.EnquiryId == notification.EnquiryId);

            if (enquiry != null)
            {
                var owner = _context.Users.FirstOrDefault(u => u.UserId == enquiry.OwnerUserId);

                // 🔔 Notify Admin back
                var adminNotification = new Notification
                {
                    UserId = 18, // Admin
                    Message = $"Owner {(response == "Approved" ? "accepted" : "rejected")} enquiry for property ID {enquiry.PropertyId}",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                };

                _context.Notifications.Add(adminNotification);
            }

            _context.SaveChanges();
        }

        return RedirectToAction("Index", "Home");
    }
}
