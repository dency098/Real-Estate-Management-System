using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using RealEstateProject.Models;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Kernel.Font;
using iText.IO.Font.Constants;
using iText.Layout.Properties;
using System.Linq;
using System.IO;
using RealEstateProject.Services;

namespace RealEstateProject.Controllers
{
    public class AdminController : Controller
    {
        private readonly RealEstateProjectContext _context;
        private readonly EmailService _emailService;

        public AdminController(RealEstateProjectContext context, EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            // 🚀 PERFORMANCE OPTIMIZATION: Bypass database notification query for AJAX alerts polling
            var actionName = context.ActionDescriptor.RouteValues["action"];
            if (string.Equals(actionName, "GetAdminAlerts", StringComparison.OrdinalIgnoreCase))
            {
                base.OnActionExecuting(context);
                return;
            }

            int adminId = 18;
            var notifications = _context.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == adminId && !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .ToList();

            ViewBag.AdminNotifications = notifications ?? new List<Notification>();
            base.OnActionExecuting(context);
        }

        // ================= ENQUIRIES =================
        public IActionResult Enquiries()
        {
            var enquiries = (from e in _context.Enquiries
                             where e.PropertyId != null && e.SenderUserId != null && e.OwnerUserId != null
                             join p in _context.Properties on e.PropertyId equals p.ProperyId
                             join sender in _context.Users on e.SenderUserId equals sender.UserId
                             join owner in _context.Users on e.OwnerUserId equals owner.UserId
                             select new
                             {
                                 e.EnquiryId,
                                 e.PropertyId,
                                 PropertyTitle = p.Title,
                                 PropertyImage = p.ProperyImages,
                                 PropertyStatus = p.Status, // ✅ IMPORTANT
                                 e.Message,
                                 e.CreatedAt,
                                 e.SenderUserId,
                                 e.OwnerUserId,
                                 e.Status,
                                 e.IsAdminApproved,
                                 SenderName = sender.FullName,
                                 OwnerName = owner.FullName
                             })
                             .OrderByDescending(e => e.CreatedAt)
                             .ToList();

            return View(enquiries);
        }

        // ================= DASHBOARD =================
        public IActionResult Dashboard()
        {
            ViewBag.TotalProperties = _context.Properties.Count();
            ViewBag.PendingProperties = _context.Properties.Count(p => p.Status == "Pending");
            ViewBag.ApprovedProperties = _context.Properties.Count(p => p.Status == "Approved");
            ViewBag.RejectedProperties = _context.Properties.Count(p => p.Status == "Rejected");
            ViewBag.SoldProperties = _context.Properties.Count(p => p.Status == "Sold"); // ✅ FIX
            ViewBag.TotalUsers = _context.Users.Count();
            ViewBag.RecentProperties = _context.Properties
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .ToList();

            return View();
        }

        // ================= NOTIFY OWNER =================
        public IActionResult NotifyOwner(int ownerId, int senderId, int enquiryId)
        {
            var enquiry = _context.Enquiries
                .Include(e => e.Property)
                .FirstOrDefault(e => e.EnquiryId == enquiryId);

            // ❌ BLOCK IF SOLD/RENTED
            if (enquiry?.Property?.Status == "Sold" || enquiry?.Property?.Status == "Rented")
                return RedirectToAction("Enquiries");

            var sender = _context.Users.FirstOrDefault(u => u.UserId == senderId);

            if (sender != null)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = ownerId,
                    Message = $"{sender.FullName} is interested in your property",
                    IsRead = false,
                    CreatedAt = DateTime.Now,
                    EnquiryId = enquiryId,
                    Status = "Pending",
                    Type = "User"
                });

                _context.SaveChanges();
            }

            return RedirectToAction("Enquiries");
        }

        // ================= APPROVE DEAL =================
        public IActionResult ApproveDeal(int id)
        {
            var enquiry = _context.Enquiries
                .Include(e => e.Property)
                .Include(e => e.SenderUser)
                .Include(e => e.OwnerUser)
                .FirstOrDefault(e => e.EnquiryId == id);

            if (enquiry == null) return NotFound();

            // ❌ BLOCK IF SOLD/RENTED
            if (enquiry.Property?.Status == "Sold" || enquiry.Property?.Status == "Rented")
                return RedirectToAction("Enquiries");

            enquiry.IsAdminApproved = true;
            enquiry.Status = "Approved";
            enquiry.Stage = 1;

            _context.SaveChanges();

            // SEND EMAIL
            string subject = $"Process Deal Started - #{enquiry.EnquiryId}";

            if (enquiry.SenderUser != null && !string.IsNullOrEmpty(enquiry.SenderUser.Email))
            {
                string buyerBody = $@"
        <h2>Hello {enquiry.SenderUser.FullName},</h2>
        <p>The deal process has started for the property you are interested in.</p>
        <p><strong>Property:</strong> {enquiry.Property?.Title}</p>
        <p><strong>Seller:</strong> {enquiry.OwnerUser?.FullName}</p>
        <br/>
        <p>Thank you,<br/>RealEstate Team</p>";
                _emailService.SendEmail(enquiry.SenderUser.Email, subject, buyerBody);
            }

            if (enquiry.OwnerUser != null && !string.IsNullOrEmpty(enquiry.OwnerUser.Email))
            {
                string sellerBody = $@"
        <h2>Hello {enquiry.OwnerUser.FullName},</h2>
        <p>The deal process has started for your property.</p>
        <p><strong>Property:</strong> {enquiry.Property?.Title}</p>
        <p><strong>Buyer:</strong> {enquiry.SenderUser?.FullName}</p>
        <br/>
        <p>Thank you,<br/>RealEstate Team</p>";
                _emailService.SendEmail(enquiry.OwnerUser.Email, subject, sellerBody);
            }

            var admin = _context.Users.FirstOrDefault(u => u.Role == "Admin");
            if (admin != null && !string.IsNullOrEmpty(admin.Email))
            {
                string adminBody = $@"
        <h2>Hello Admin,</h2>
        <p>The deal process has started.</p>
        <p><strong>Property:</strong> {enquiry.Property?.Title}</p>
        <p><strong>Seller:</strong> {enquiry.OwnerUser?.FullName}</p>
        <p><strong>Buyer:</strong> {enquiry.SenderUser?.FullName}</p>
        <br/>
        <p>Thank you,<br/>System</p>";
                _emailService.SendEmail(admin.Email, subject, adminBody);
            }

            return RedirectToAction("Enquiries");
        }

        // ================= RENT REMINDER =================
        [HttpPost]
        public IActionResult SendRentReminder(int id)
        {
            var enquiry = _context.Enquiries
                .Include(e => e.Property)
                .Include(e => e.SenderUser)
                .FirstOrDefault(e => e.EnquiryId == id);

            if (enquiry == null) return NotFound();

            if (enquiry.SenderUserId.HasValue)
            {
                // Website Notification
                AddNotification(enquiry.SenderUserId.Value, $"🔔 Rent Reminder: Your monthly rent of ₹{(enquiry.PropertyPrice ?? 0):N0} for \"{enquiry.Property?.Title}\" is due in 2 days. Please pay your rent on time.");
                _context.SaveChanges();

                // Email to Tenant
                if (enquiry.SenderUser != null && !string.IsNullOrEmpty(enquiry.SenderUser.Email))
                {
                    string subject = $"Rent Payment Reminder - {enquiry.Property?.Title}";
                    string body = $@"
                        <h2>Hello {enquiry.SenderUser.FullName},</h2>
                        <p>This is a friendly reminder that your monthly rent is due in <strong>2 days</strong>.</p>
                        <p><strong>Property:</strong> {enquiry.Property?.Title}</p>
                        <p><strong>Monthly Rent:</strong> ₹{(enquiry.PropertyPrice ?? 0):N0}</p>
                        <p>Please make sure to pay your rent on time to avoid any inconvenience.</p>
                        <br/>
                        <p>Thank you,<br/>RealEstate Team</p>";
                    _emailService.SendEmail(enquiry.SenderUser.Email, subject, body);
                }

                TempData["Success"] = "Rent reminder sent to tenant (notification + email).";
            }
            else
            {
                TempData["Error"] = "Could not send reminder: Tenant not found.";
            }

            return RedirectToAction("ProcessDeal", new { id });
        }

        // ================= CLOSE RENT DEAL =================
        [HttpPost]
        public IActionResult CloseRentDeal(int id)
        {
            var enquiry = _context.Enquiries
                .Include(e => e.Property)
                .Include(e => e.SenderUser)
                .Include(e => e.OwnerUser)
                .FirstOrDefault(e => e.EnquiryId == id);

            if (enquiry == null) return NotFound();

            enquiry.Status = "Closed";
            enquiry.IsRentClosed = true;
            enquiry.IsCloseRentRequested = false;

            if (enquiry.Property != null)
            {
                enquiry.Property.Status = "Approved"; // Make it available again for rent
            }

            // Notify Tenant
            if (enquiry.SenderUserId.HasValue)
            {
                AddNotification(enquiry.SenderUserId.Value, $"Your rent deal for \"{enquiry.Property?.Title}\" has been closed by admin. The property is now available for others.");
            }

            // Notify Owner
            if (enquiry.OwnerUserId.HasValue)
            {
                AddNotification(enquiry.OwnerUserId.Value, $"The rent deal for your property \"{enquiry.Property?.Title}\" has been closed. Your property is now available for rent again.");
            }

            _context.SaveChanges();

            TempData["Success"] = "Rent deal closed successfully. Property is now available again.";
            return RedirectToAction("Enquiries");
        }

        // ================= TOKEN STAGE =================
      
        // ================= TOKEN STAGE =================
        public IActionResult PayToken(int id)
        {
            var enquiry = _context.Enquiries
                .Include(e => e.Property)
                .FirstOrDefault(e => e.EnquiryId == id);

            if (enquiry == null)
                return NotFound();

            // ❌ BLOCK IF PROPERTY ALREADY SOLD/RENTED
            if (enquiry.Property?.Status == "Sold" || enquiry.Property?.Status == "Rented")
                return RedirectToAction("Enquiries");

            // ✅ SET PROPERTY PRICE
            enquiry.PropertyPrice = enquiry.Property.Price;

            if (string.Equals(enquiry.Property?.PropertyType, "Rent", StringComparison.OrdinalIgnoreCase))
            {
                // Set default Rent Payment fields
                enquiry.SecurityDepositAmount = (enquiry.PropertyPrice ?? 0) * 3; // 3 months rent as security deposit
                enquiry.FirstMonthRentAmount = enquiry.PropertyPrice; // 1 month rent
                enquiry.AgreementChargesAmount = 2500; // standard fixed charge
                
                enquiry.IsSecurityDepositPaid = false;
                enquiry.IsFirstMonthRentPaid = false;
                enquiry.IsAgreementChargesPaid = false;

                // Move to stage 3
                enquiry.Stage = 3;

                AddNotification(enquiry.SenderUserId.Value, "Please pay rent booking charges.");
                AddNotification(enquiry.OwnerUserId.Value, "Waiting for tenant's booking payments.");
            }
            else
            {
                // ✅ FIXED TOKEN AMOUNT
                enquiry.TokenAmount = 15000;

                // ✅ RESET TOKEN FLAGS
                enquiry.IsTokenPaid = false;
                enquiry.IsTokenPaidByBuyer = false;

                // ✅ MOVE TO TOKEN STAGE (STAGE 5 for Sale)
                enquiry.Stage = 5;

                // ✅ NOTIFICATIONS
                AddNotification(
                    enquiry.SenderUserId.Value,
                    "Please pay token amount ₹15,000"
                );

                AddNotification(
                    enquiry.OwnerUserId.Value,
                    "Waiting for buyer token payment ₹15,000"
                );
            }

            _context.SaveChanges();

            return RedirectToAction("ProcessDeal", new { id });
        }
        // ================= REGISTRATION =================
        public IActionResult CompleteRegistration(int id)
        {
            var enquiry = _context.Enquiries
                .Include(e => e.Property)
                .Include(e => e.SenderUser)
                .Include(e => e.OwnerUser)
                .FirstOrDefault(x => x.EnquiryId == id);

            if (enquiry == null) return NotFound();

            if (enquiry.Property?.Status == "Sold" || enquiry.Property?.Status == "Rented")
                return RedirectToAction("Enquiries");

            bool isRent = string.Equals(enquiry.Property?.PropertyType, "Rent", StringComparison.OrdinalIgnoreCase);
            bool canAdvance = isRent 
                ? (enquiry.IsDocumentUploaded)
                : (enquiry.IsTokenPaid || enquiry.IsBuyerPaid);

            if (canAdvance)
            {
                if (isRent)
                {
                    enquiry.IsDocumentApprovedByAdmin = true;
                    enquiry.Stage = 5; // Move to Security Deposit Payment
                    enquiry.SecurityDepositAmount = (enquiry.PropertyPrice ?? 0) * 3;
                    enquiry.FirstMonthRentAmount = enquiry.PropertyPrice;
                    enquiry.AgreementChargesAmount = 2500;
                    enquiry.IsSecurityDepositPaid = false;
                    enquiry.IsFirstMonthRentPaid = false;
                    enquiry.IsAgreementChargesPaid = false;
                }
                else
                {
                    enquiry.Stage = 6; // Legal Registration stage for Sale
                }
                _context.SaveChanges();

                // SEND EMAIL
                string processName = isRent ? "Rental Agreement Verification" : "Registration Process";
                string subject = $"{processName} Starts - #{enquiry.EnquiryId}";

                if (enquiry.SenderUser != null && !string.IsNullOrEmpty(enquiry.SenderUser.Email))
                {
                    string buyerBody = $@"
        <h2>Hello {enquiry.SenderUser.FullName},</h2>
        <p>The {processName.ToLower()} has started for the property.</p>
        <p><strong>Property:</strong> {enquiry.Property?.Title}</p>
        <p><strong>Owner:</strong> {enquiry.OwnerUser?.FullName}</p>
        <br/>
        <p>Thank you,<br/>RealEstate Team</p>";
                    _emailService.SendEmail(enquiry.SenderUser.Email, subject, buyerBody);
                }

                if (enquiry.OwnerUser != null && !string.IsNullOrEmpty(enquiry.OwnerUser.Email))
                {
                    string sellerBody = $@"
        <h2>Hello {enquiry.OwnerUser.FullName},</h2>
        <p>The {processName.ToLower()} has started for your property.</p>
        <p><strong>Property:</strong> {enquiry.Property?.Title}</p>
        <p><strong>Tenant:</strong> {enquiry.SenderUser?.FullName}</p>
        <br/>
        <p>Thank you,<br/>RealEstate Team</p>";
                    _emailService.SendEmail(enquiry.OwnerUser.Email, subject, sellerBody);
                }

                var admin = _context.Users.FirstOrDefault(u => u.Role == "Admin");
                if (admin != null && !string.IsNullOrEmpty(admin.Email))
                {
                    string adminBody = $@"
        <h2>Hello Admin,</h2>
        <p>The {processName.ToLower()} has started.</p>
        <p><strong>Property:</strong> {enquiry.Property?.Title}</p>
        <p><strong>Owner:</strong> {enquiry.OwnerUser?.FullName}</p>
        <p><strong>Tenant:</strong> {enquiry.SenderUser?.FullName}</p>
        <br/>
        <p>Thank you,<br/>System</p>";
                    _emailService.SendEmail(admin.Email, subject, adminBody);
                }
            }

            return RedirectToAction("ProcessDeal", new { id });
        }

        // ================= COMMISSION =================
        public IActionResult PayCommission(int id)
        {
            var enquiry = _context.Enquiries
                .Include(e => e.Property)
                .FirstOrDefault(e => e.EnquiryId == id);

            if (enquiry == null) return NotFound();

            if (enquiry.Property?.Status == "Sold" || enquiry.Property?.Status == "Rented")
                return RedirectToAction("Enquiries");

            if (string.Equals(enquiry.Property?.PropertyType, "Rent", StringComparison.OrdinalIgnoreCase))
            {
                enquiry.BrokerageChargesAmount = enquiry.PropertyPrice; // 1 month rent as brokerage fee
                enquiry.IsBrokeragePaid = false;
                enquiry.Stage = 6;

                AddNotification(enquiry.SenderUserId.Value, "Please pay Brokerage Charges.");
                AddNotification(enquiry.OwnerUserId.Value, "Waiting for Tenant's Brokerage payment.");
            }
            else
            {
                enquiry.CommissionAmount = enquiry.PropertyPrice * 0.02M;

                enquiry.IsBuyerPaid = false;
                enquiry.IsOwnerPaid = false;
                enquiry.IsCommissionPaid = false;
                enquiry.Stage = 7; // Commission stage for Sale

                AddNotification(enquiry.SenderUserId.Value, "Pay commission");
                AddNotification(enquiry.OwnerUserId.Value, "Pay commission");
            }

            _context.SaveChanges();
            return RedirectToAction("ProcessDeal", new { id });
        }

        // ================= FINAL STAGE =================
        // ================= FINAL STAGE =================
        public IActionResult UpdateStage(int id, int stage)
        {
            var enquiry = _context.Enquiries
                .Include(e => e.Property)
                .Include(e => e.SenderUser)
                .Include(e => e.OwnerUser)
                .FirstOrDefault(e => e.EnquiryId == id);

            if (enquiry == null)
                return NotFound();

            if (enquiry.Property?.Status == "Sold" || enquiry.Property?.Status == "Rented")
                return RedirectToAction("Enquiries");

            enquiry.Stage = stage;

            if (stage == 6)
            {
                var admin = _context.Users.FirstOrDefault(u => u.Role == "Admin");
                // ONLY COMPLETE CURRENT DEAL
                enquiry.Status = "Completed";

                // ONLY MARK PROPERTY SOLD
                var property = _context.Properties
                    .FirstOrDefault(p => p.ProperyId == enquiry.PropertyId);

                if (property != null)
                {
                    bool isRentProperty = string.Equals(property.PropertyType, "Rent", StringComparison.OrdinalIgnoreCase);
                    property.Status = isRentProperty ? "Rented" : "Sold";
                }

                bool isRent = string.Equals(enquiry.Property?.PropertyType, "Rent", StringComparison.OrdinalIgnoreCase);

                if (isRent)
                {
                    // Add rent reminder
                    _context.Notifications.Add(new Notification
                    {
                        UserId = enquiry.SenderUserId.Value,
                        Message = $"Rent Due Reminder: Your monthly rent of ₹{enquiry.PropertyPrice?.ToString("N0")} is due in 2 days.",
                        IsRead = false,
                        CreatedAt = DateTime.Now,
                        EnquiryId = enquiry.EnquiryId,
                        Status = "Unread",
                        Type = "General"
                    });
                }

                // SEND EMAIL
                string subject = isRent ? $"Rent Deal Active - #{enquiry.EnquiryId}" : $"Property Sold - #{enquiry.EnquiryId}";

                if (enquiry.SenderUser != null && !string.IsNullOrEmpty(enquiry.SenderUser.Email))
                {
                    string buyerBody = isRent 
                        ? $@"<h2>Hello {enquiry.SenderUser.FullName},</h2><p>Congratulations! Your rent agreement deal has been successfully completed and is now active.</p><p><strong>Property:</strong> {enquiry.Property?.Title}</p><p><strong>Monthly Rent:</strong> ₹{enquiry.PropertyPrice:N0}</p><br/><p>Thank you,<br/>RealEstate Team</p>"
                        : $@"<h2>Hello {enquiry.SenderUser.FullName},</h2><p>Congratulations! The property has been successfully sold to you.</p><p><strong>Property:</strong> {enquiry.Property?.Title}</p><p><strong>Seller:</strong> {enquiry.OwnerUser?.FullName}</p><br/><p>Thank you,<br/>RealEstate Team</p>";
                    _emailService.SendEmail(enquiry.SenderUser.Email, subject, buyerBody);
                }

                if (enquiry.OwnerUser != null && !string.IsNullOrEmpty(enquiry.OwnerUser.Email))
                {
                    string sellerBody = isRent
                        ? $@"<h2>Hello {enquiry.OwnerUser.FullName},</h2><p>Congratulations! Your property rent deal has been completed successfully and is now active.</p><p><strong>Property:</strong> {enquiry.Property?.Title}</p><p><strong>Tenant:</strong> {enquiry.SenderUser?.FullName}</p><br/><p>Thank you,<br/>RealEstate Team</p>"
                        : $@"<h2>Hello {enquiry.OwnerUser.FullName},</h2><p>Congratulations! Your property has been successfully sold.</p><p><strong>Property:</strong> {enquiry.Property?.Title}</p><p><strong>Buyer:</strong> {enquiry.SenderUser?.FullName}</p><br/><p>Thank you,<br/>RealEstate Team</p>";
                    _emailService.SendEmail(enquiry.OwnerUser.Email, subject, sellerBody);
                }

                if (admin != null && !string.IsNullOrEmpty(admin.Email))
                {
                    string adminBody = $@"
        <h2>Hello Admin,</h2>
        <p>A property has been successfully {(isRent ? "rented" : "sold")}.</p>
        <p><strong>Property:</strong> {enquiry.Property?.Title}</p>
        <p><strong>Seller/Owner:</strong> {enquiry.OwnerUser?.FullName}</p>
        <p><strong>Buyer/Tenant:</strong> {enquiry.SenderUser?.FullName}</p>
        <br/>
        <p>Thank you,<br/>System</p>";
                    _emailService.SendEmail(admin.Email, subject, adminBody);
                }
            }

            _context.SaveChanges();

            return RedirectToAction("ProcessDeal", new { id });
        }
      

        // ================= PROCESS DEAL =================
        public IActionResult ProcessDeal(int id)
        {
            var enquiry = _context.Enquiries
                .Include(e => e.Property)
                .Include(e => e.OwnerUser)
                .Include(e => e.SenderUser)
                .FirstOrDefault(e => e.EnquiryId == id);

            if (enquiry == null) return NotFound();

            return View(enquiry);
        }

        // ================= INVOICE =================
        public IActionResult GenerateInvoice(int id)
        {
            var enquiry = _context.Enquiries
                .Include(e => e.Property)
                .Include(e => e.SenderUser)
                .Include(e => e.OwnerUser)
                .FirstOrDefault(e => e.EnquiryId == id);

            if (enquiry == null) return NotFound();

            using (var stream = new MemoryStream())
            {
                var writer = new PdfWriter(stream);
                var pdf = new PdfDocument(writer);
                var document = new Document(pdf);
                var bold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

                document.Add(new Paragraph("REAL ESTATE DEAL INVOICE").SetFont(bold).SetFontSize(20).SetTextAlignment(TextAlignment.CENTER));
                document.Add(new Paragraph($"Invoice ID: {enquiry.EnquiryId}"));
                document.Add(new Paragraph($"Property: {enquiry.Property?.Title}"));
                document.Add(new Paragraph($"Status: SOLD").SetFont(bold));

                document.Close();
                
                byte[] pdfBytes = stream.ToArray();

                return File(pdfBytes, "application/pdf", $"Invoice_{id}.pdf");
            }
        }

        // ================= HELPER =================
        private void AddNotification(int userId, string message)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = userId,
                Message = message,
                CreatedAt = DateTime.Now,
                IsRead = false,
                Status = "Unread",
                Type = "General"
            });
        }

        // ================= DEAL HISTORY =================
        public IActionResult DealHistory()
        {
            var deals = (from e in _context.Enquiries
                         join p in _context.Properties on e.PropertyId equals p.ProperyId
                         join b in _context.Users on e.SenderUserId equals b.UserId
                         join s in _context.Users on e.OwnerUserId equals s.UserId
                         select new
                         {
                             e.EnquiryId,
                             PropertyTitle = p.Title,
                             PropertyType = p.PropertyType,
                             Price = e.PropertyPrice ?? 0,
                             Stage = e.Stage ?? 0,
                             Status = e.Status ?? "Pending",
                             Buyer = b.FullName,
                             Owner = s.FullName
                         })
                         .OrderByDescending(x => x.EnquiryId)
                         .ToList();

            return View(deals);
        }


        // 📌 MANAGE ENQUIRIES(FIXED PRICE ISSUE)
                public IActionResult ManageEnquiries()
        {
            var enquiries = _context.Enquiries
                .Include(e => e.Property)
                .Where(e => e.PropertyId != null)
                .ToList();

            return View(enquiries);
        }
        public IActionResult Users()
        {

            var users = _context.Users
                .OrderByDescending(u => u.UserId)
                .ToList();

            return View(users);
        }
        public JsonResult GetAdminAlerts()
        {
            var data = _context.Properties
                .AsNoTracking()
                .Include(p => p.User)
                .Where(p =>
                    p.Status == "Pending" ||
                    p.Status == "EditPending" ||
                    p.Status == "DeleteRequested"
                )
                .OrderByDescending(p => p.CreatedAt)
                .Take(10)
                .Select(p => new
                {
                    id = p.ProperyId,
                    title = p.Title,
                    user = p.User.FullName,
                    status = p.Status,
                    time = p.CreatedAt.ToString("dd MMM hh:mm tt")
                })
                .ToList();

            return Json(data);
        }
    }
}
