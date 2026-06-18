using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateProject.Models;
using RealEstateProject.Services;
using System.ComponentModel.DataAnnotations;

namespace RealEstateProject.Controllers
{
    public class EnquiryController : Controller
    {
        private readonly RealEstateProjectContext _context;
        private readonly EmailService _emailService;
        public EnquiryController(RealEstateProjectContext context, EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // =========================
        // SEND ENQUIRY (ADMIN/USER → OWNER)
        // =========================
        [HttpPost]
        public IActionResult SendEnquiry(int PropertyId, string Message)
        {
            var property = _context.Properties.FirstOrDefault(p => p.ProperyId == PropertyId);
            if (property == null) return NotFound();

            if (property.Status == "Sold" || property.Status == "Rented")
                return BadRequest("Property is no longer available");

            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
                return RedirectToAction("Login", "Account");

            int senderId = Convert.ToInt32(userIdStr);

            if (property.UserId == senderId)
                return BadRequest("You cannot enquire on your own property");

            var sender = _context.Users.FirstOrDefault(u => u.UserId == senderId);
            if (sender == null) return BadRequest("User not found");

            if (string.IsNullOrWhiteSpace(Message))
                return BadRequest("Message required");

            var enquiry = new Enquiry
            {
                PropertyId = PropertyId,
                SenderUserId = senderId,
                OwnerUserId = property.UserId,
                Message = Message,
                CreatedAt = DateTime.Now,

                Status = "Pending",
                IsAdminApproved = false,
                Stage = 0,

                PropertyPrice = property.Price,
                TokenAmount = 0,
                CommissionAmount = 0,
                AdminCommission = 0
            };

            _context.Enquiries.Add(enquiry);

            _context.Notifications.Add(new Notification
            {
                UserId = property.UserId,
                Message = $"{sender.FullName} sent you an enquiry",
                IsRead = false,
                CreatedAt = DateTime.Now,
                Status = "Unread",
                Type = "Enquiry"
            });

            _context.SaveChanges();
            var owner = _context.Users.FirstOrDefault(u => u.UserId == property.UserId);

            if (owner != null && !string.IsNullOrEmpty(owner.Email))
            {
                string subject = "New Property Enquiry";

                string body = $@"
        <h2>Hello {owner.FullName},</h2>

        <p>
            <strong>{sender.FullName}</strong> is interested in your property:
        </p>

        <p>
            <strong>Property:</strong> {property.Title}
        </p>

        <p>
            <strong>Message:</strong> {Message}
        </p>

        <br/>

        <p>
            Would you like to continue this conversation?
        </p>

        <br/>

        <p>
            Thank you,<br/>
            RealEstate Team
        </p>
    ";

                _emailService.SendEmail(owner.Email, subject, body);
            }

            TempData["Success"] = "your enquiry successfully sent";
            return RedirectToAction("ViewProperty", "User", new { id = PropertyId });
        }

        // =========================
        // APPROVE (YES)
        // =========================
        //public IActionResult Approve(int id)
        //{
        //    var enquiry = _context.Enquiries.FirstOrDefault(e => e.EnquiryId == id);

        //    if (enquiry == null) return NotFound();

        //    enquiry.Status = "Approved";
        //    _context.SaveChanges();

        //    TempData["Success"] = "Enquiry Approved!";
        //    // return RedirectToAction("Enquiries", "Admin"); // ✅ FIX
        //    return Redirect(Request.Headers["Referer"].ToString());
        //}
        public IActionResult Approve(int id)
        {
            var enquiry = _context.Enquiries
                .Include(e => e.Property)
                .Include(e => e.SenderUser)
                .FirstOrDefault(e => e.EnquiryId == id);

            if (enquiry == null)
                return NotFound();

            enquiry.Status = "Approved";

            // =========================
            // WEBSITE NOTIFICATION
            // =========================
            _context.Notifications.Add(new Notification
            {
                UserId = enquiry.SenderUserId,
                EnquiryId = enquiry.EnquiryId,
                Message = $"Seller approved your enquiry for '{enquiry.Property?.Title}'",
                IsRead = false,
                CreatedAt = DateTime.Now,
                Status = "Unread",
                Type = "Enquiry"
            });

            _context.SaveChanges();

            // =========================
            // EMAIL TO BUYER
            // =========================
            if (enquiry.SenderUser != null &&
                !string.IsNullOrEmpty(enquiry.SenderUser.Email))
            {
                string subject = "Enquiry Approved";

                string body = $@"
            <h2>Hello {enquiry.SenderUser.FullName},</h2>

            <p>
                Your enquiry for property
                <strong>{enquiry.Property?.Title}</strong>
                has been approved by the seller.
            </p>

            <p>
                The deal process will begin shortly.
            </p>

            <br/>

            <p>
                Thank you,<br/>
                RealEstate Team
            </p>
        ";

                _emailService.SendEmail(
                    enquiry.SenderUser.Email,
                    subject,
                    body
                );
            }

            TempData["Success"] = "Enquiry Approved!";

            return Redirect(Request.Headers["Referer"].ToString());
        }

        // =========================
        // REJECT (NO)
        // =========================
        //public IActionResult Reject(int id)
        //{
        //    var enquiry = _context.Enquiries.FirstOrDefault(e => e.EnquiryId == id);

        //    if (enquiry == null) return NotFound();

        //    enquiry.Status = "Rejected";
        //    _context.SaveChanges();

        //    TempData["Success"] = "Enquiry Rejected!";
        //    // return RedirectToAction("Enquiries", "Admin"); // ✅ FIX
        //    return Redirect(Request.Headers["Referer"].ToString());
        //}
       
        public IActionResult Reject(int id)
        {
            var enquiry = _context.Enquiries
                .Include(e => e.Property)
                .Include(e => e.SenderUser)
                .FirstOrDefault(e => e.EnquiryId == id);

            if (enquiry == null)
                return NotFound();

            enquiry.Status = "Rejected";

            // =========================
            // WEBSITE NOTIFICATION
            // =========================
            _context.Notifications.Add(new Notification
            {
                UserId = enquiry.SenderUserId,
                Message = $"Seller rejected your enquiry",
                IsRead = false,
                Status = "Rejected",
                Type = "Enquiry",
                CreatedAt = DateTime.Now
            });

            _context.SaveChanges();

            // =========================
            // EMAIL TO BUYER
            // =========================
            if (enquiry.SenderUser != null &&
                !string.IsNullOrEmpty(enquiry.SenderUser.Email))
            {
                string subject = "Enquiry Rejected";

                string body = $@"
            <h2>Hello {enquiry.SenderUser.FullName},</h2>

            <p>
                Your enquiry for property
                <strong>{enquiry.Property?.Title}</strong>
                has been rejected by the seller.
            </p>

            <br/>

            <p>
                Thank you,<br/>
                RealEstate Team
            </p>
        ";

                _emailService.SendEmail(
                    enquiry.SenderUser.Email,
                    subject,
                    body
                );
            }

            TempData["Success"] = "Enquiry Rejected!";

            return Redirect(Request.Headers["Referer"].ToString());
        }


        // =========================
        // OWNER MESSAGES VIEW
        // =========================

        public IActionResult OwnerMessages()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
                return RedirectToAction("Login", "Account");

            int ownerId = Convert.ToInt32(userIdStr);

            var enquiries = _context.Enquiries
                .Where(e => e.OwnerUserId == ownerId)
                .OrderByDescending(e => e.CreatedAt)
                .ToList();

            return View(enquiries);
        }

        public IActionResult Create(int id)
        {
            var property = _context.Properties
                .FirstOrDefault(p => p.ProperyId == id);

            if (property == null || property.Status == "Sold" || property.Status == "Rented")
            {
                TempData["Error"] = "This property is already sold!";
                return RedirectToAction("Index", "Property");
            }

            return View();
        }
    }
}



