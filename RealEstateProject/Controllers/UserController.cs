using iText.IO.Font.Constants;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Layout.Element;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Razorpay.Api;
using RealEstateProject.Models;
using iText.Layout;

using RealEstateProject.Services;

namespace RealEstateProject.Controllers
{
    public class UserController : Controller
    {
        private readonly RealEstateProjectContext _context;
        private readonly IConfiguration _config;
        private readonly EmailService _emailService;

        // ✅ UPDATE CONSTRUCTOR
        public UserController(RealEstateProjectContext context, IConfiguration config, EmailService emailService)
        {
            _context = context;
            _config = config;
            _emailService = emailService;
        }

        
        public IActionResult AboutUs()
        {
            return View();
        }
        public IActionResult Home()
        {
            return View();
        }

        public IActionResult ContactUs()
        {
            return View();
        }

        public IActionResult Properties(string searchQuery, string propertyType, decimal? minPrice, decimal? maxPrice)
        {
            var query = _context.Properties
                .AsNoTracking()
                .Include(p => p.City)
                .Include(p => p.ProperyImages)
                .Include(p => p.User)
                .Where(p => p.Status == "Approved" || p.User.Role == "Admin");

            // Filter by Title, Description, Address, or CityName
            if (!string.IsNullOrEmpty(searchQuery))
            {
                var term = searchQuery.Trim();
                query = query.Where(p =>
                    p.Title.Contains(term) ||
                    (p.Description != null && p.Description.Contains(term)) ||
                    (p.Address != null && p.Address.Contains(term)) ||
                    (p.City != null && p.City.CityName.Contains(term))
                );
            }

            // Filter by Property Type
            if (!string.IsNullOrEmpty(propertyType) && !string.Equals(propertyType, "Property Type", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.PropertyType == propertyType);
            }

            // Filter by Min Price
            if (minPrice.HasValue)
            {
                query = query.Where(p => p.Price >= minPrice.Value);
            }

            // Filter by Max Price
            if (maxPrice.HasValue)
            {
                query = query.Where(p => p.Price <= maxPrice.Value);
            }

            var data = query.OrderByDescending(p => p.CreatedAt).ToList();

            // Store current search values in ViewBag so we can prepopulate fields in the views
            ViewBag.SearchQuery = searchQuery;
            ViewBag.PropertyType = propertyType;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;

            return View(data);
        }


        [HttpPost]
        public IActionResult ContactUs(string Name, string Email, string Message)
        {
            TempData["Success"] = "Message sent!";
            return RedirectToAction("ContactUs");
        }

        // 🔔 LOAD USER NOTIFICATIONS AND FAVORITES - HIGHLY OPTIMIZED
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            int userId = 0;
            var userIdString = HttpContext.Session.GetString("UserId");

            if (!string.IsNullOrWhiteSpace(userIdString))
            {
                userId = Convert.ToInt32(userIdString);
                var favorites = _context.Favorites
                    .AsNoTracking()
                    .Where(f => f.UserId == userId)
                    .Select(f => f.PropertyId)
                    .ToList();
                ViewBag.FavoritePropertyIds = favorites;
            }
            else
            {
                ViewBag.FavoritePropertyIds = new List<int>();
            }

            // ✅ SELLER gets enquiries as notifications (Only loaded if user is logged in to save database hits)
            List<Enquiry> notifications = new List<Enquiry>();
            List<Notification> generalNotifications = new List<Notification>();
            if (userId > 0)
            {
                notifications = _context.Enquiries
                    .AsNoTracking()
                    .Include(e => e.Property)
                    .Include(e => e.SenderUser)
                    .Where(e => e.OwnerUserId == userId && e.Status == "Pending")
                    .OrderByDescending(e => e.EnquiryId)
                    .ToList();

                // Fetch actual system notifications (like rent reminders, deal closures, etc)
                generalNotifications = _context.Notifications
                    .AsNoTracking()
                    .Where(n => n.UserId == userId && !n.IsRead)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToList();
            }
            ViewBag.Notifications = notifications;
            ViewBag.GeneralNotifications = generalNotifications;

            // 🚀 DYNAMIC PROPERTY TYPES LOAD FOR SEARCH (Session Cached to avoid redundant SQL queries on every page click)
            var cachedTypes = HttpContext.Session.GetString("CachedPropertyTypes");
            List<string> propertyTypesList;
            if (string.IsNullOrEmpty(cachedTypes))
            {
                propertyTypesList = _context.Properties
                    .AsNoTracking()
                    .Where(p => p.Status == "Approved")
                    .Select(p => p.PropertyType)
                    .Distinct()
                    .ToList() ?? new List<string>();
                HttpContext.Session.SetString("CachedPropertyTypes", string.Join(",", propertyTypesList));
            }
            else
            {
                propertyTypesList = cachedTypes.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            }
            ViewBag.PropertyTypes = propertyTypesList;

            base.OnActionExecuting(context);
        }

        // 🔔 MARK NOTIFICATION AS READ
        public IActionResult MarkAsRead(int id)
        {
            var notification = _context.Notifications.Find(id);

            if (notification != null)
            {
                notification.IsRead = true;
                _context.SaveChanges();
            }

            return RedirectToAction("Index");
        }

        // ================= HOME PAGE =================
        public IActionResult Index()
        {
            var data = _context.Properties
                .AsNoTracking()
                .Include(p => p.City)
                .Include(p => p.ProperyImages)
                .Include(p => p.User) // ⚠️ IMPORTANT (for Role check)
                .Where(p => p.Status == "Approved" || p.User.Role == "Admin")
                .OrderByDescending(p => p.CreatedAt)
                .Take(20)
                .ToList();

            return View(data);
        }

        // ================= ADD PROPERTY =================
        public IActionResult Property()
        {
            ViewBag.States = _context.States.ToList();
            ViewBag.Cities = _context.Cities.ToList();
            ViewBag.Amenities = _context.Amenities.ToList();
            var count = _context.Amenities.Count();  // check count
            ViewBag.Categories = _context.PropertyCategories.ToList();

            return View("AddProperty");
        }

        [HttpPost]
        public IActionResult AddProperty(Property model, List<IFormFile> ImageFiles, List<int> SelectedAmenities, IFormFile? VideoFile)
        {
            try
            {
                // VIDEO UPLOAD
                if (VideoFile != null && VideoFile.Length > 0)
                {
                    string videoFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/videos");
                    if (!Directory.Exists(videoFolder)) Directory.CreateDirectory(videoFolder);
                    string videoName = Guid.NewGuid().ToString() + Path.GetExtension(VideoFile.FileName);
                    string videoPath = Path.Combine(videoFolder, videoName);
                    using (var stream = new FileStream(videoPath, FileMode.Create))
                    {
                        VideoFile.CopyTo(stream);
                    }
                    model.VideoUrl = "/videos/" + videoName;
                }

                // IMAGE UPLOAD
                if (ImageFiles != null && ImageFiles.Count > 0)
                {
                    string folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");

                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);

                    if (model.ProperyImages == null)
                        model.ProperyImages = new List<ProperyImage>();

                    foreach (var imageFile in ImageFiles)
                    {
                        if (imageFile.Length > 0)
                        {
                            string fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                            string filePath = Path.Combine(folder, fileName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                imageFile.CopyTo(stream);
                            }

                            var image = new ProperyImage
                            {
                                ImagePath = "/images/" + fileName
                            };

                            model.ProperyImages.Add(image);
                        }
                    }
                }

                model.UserId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
                model.CreatedAt = DateTime.Now;
                model.Status = "Pending";

                // ✅ SAVE PROPERTY FIRST
                _context.Properties.Add(model);
                _context.SaveChanges();

                // ✅ SAVE AMENITIES (IMPORTANT)
                if (SelectedAmenities != null && SelectedAmenities.Any())
                {
                    foreach (var amenityId in SelectedAmenities)
                    {
                        _context.PropertyAmenities.Add(new PropertyAmenity
                        {
                            PropertyId = model.ProperyId,
                            AmenityId = amenityId
                        });
                    }

                    _context.SaveChanges();
                }

                TempData["Success"] = "Property submitted!";
                return RedirectToAction("MyProperties");
            }
            catch (Exception ex)
            {
                return Content(ex.InnerException?.Message ?? ex.Message);
            }
        }

        // ================= VIEW PROPERTY =================
        public IActionResult ViewProperty(int id)
        {
            var property = _context.Properties
                .Include(p => p.City)
                .Include(p => p.ProperyImages)
                 .Include(p => p.PropertyAmenities)
                    .ThenInclude(pa => pa.Amenity)
                .Include(p => p.Reviews)
                    .ThenInclude(r => r.User)
                .Include(p => p.User)
                .FirstOrDefault(p => p.ProperyId == id);

            if (property == null)
            {
                return NotFound();
            }

            return View(property);
        }

        [HttpPost]
        public IActionResult AddReview(int propertyId, int rating, string comment)
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
            {
                TempData["Error"] = "Please login to leave a review.";
                return RedirectToAction("Login", "Account");
            }

            int userId = Convert.ToInt32(userIdStr);

            var review = new Review
            {
                PropertyId = propertyId,
                UserId = userId,
                Rating = rating,
                Comment = comment
            };

            _context.Reviews.Add(review);
            _context.SaveChanges();

            TempData["Success"] = "Review added successfully!";
            return RedirectToAction("ViewProperty", new { id = propertyId });
        }

        // ================= PROPERTY MANAGEMENT =================
        //public IActionResult Property()
        //{
        //    ViewBag.States = _context.States.ToList();
        //    ViewBag.Cities = _context.Cities.ToList();
        //    ViewBag.Categories = _context.PropertyCategories.ToList();
        //    return View();
        //}

        [HttpPost]
        public IActionResult Property(RealEstateProject.Models.Property model, List<IFormFile> ImageFiles)
        {
            try
            {
                if (ImageFiles != null && ImageFiles.Count > 0)
                {
                    string folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                    model.ProperyImages = new List<ProperyImage>();

                    foreach (var imageFile in ImageFiles)
                    {
                        if (imageFile.Length > 0)
                        {
                            string fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                            string filePath = Path.Combine(folder, fileName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                imageFile.CopyTo(stream);
                            }

                            model.ProperyImages.Add(new ProperyImage { ImagePath = "/images/" + fileName });
                        }
                    }
                }

                model.UserId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
                model.CreatedAt = DateTime.Now;
                model.Status = "Pending";

                _context.Properties.Add(model);
                _context.SaveChanges();

                TempData["Success"] = "Property submitted!";
                return RedirectToAction("MyProperties");
            }
            catch (Exception ex)
            {
                return Content(ex.InnerException?.Message ?? ex.Message);
            }
        }

        public IActionResult MyProperties()
        {
            var userId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
            var data = _context.Properties
                .Include(p => p.City)
                .Include(p => p.Category)
                .Include(p => p.ProperyImages)
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .ToList();

            return View(data);
        }

        public IActionResult ListProperties()
        {
            var data = _context.Properties
                .Include(p => p.City)
                .Include(p => p.ProperyImages)
                .Include(p => p.User)
                .Where(p => p.Status == "Approved")
                .OrderByDescending(p => p.CreatedAt)
                .ToList();

            return View(data);
        }

        [HttpPost]
        public IActionResult ToggleFavorite(int propertyId)
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
            {
                return Json(new { success = false, message = "Please login first" });
            }

            int userId = Convert.ToInt32(userIdStr);
            var existing = _context.Favorites.FirstOrDefault(f => f.UserId == userId && f.PropertyId == propertyId);

            if (existing != null)
            {
                _context.Favorites.Remove(existing);
                _context.SaveChanges();
                return Json(new { success = true, isFavorite = false });
            }
            else
            {
                _context.Favorites.Add(new Favorite { UserId = userId, PropertyId = propertyId });
                _context.SaveChanges();
                return Json(new { success = true, isFavorite = true });
            }
        }

        public IActionResult Favorites()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
                return RedirectToAction("Login", "Account");

            int userId = Convert.ToInt32(userIdStr);

            var favorites = _context.Favorites
                .Include(f => f.Property)
                    .ThenInclude(p => p.City)
                .Include(f => f.Property)
                    .ThenInclude(p => p.ProperyImages)
                .Include(f => f.Property)
                    .ThenInclude(p => p.User)
                .Where(f => f.UserId == userId)
                .Select(f => f.Property)
                .ToList();

            return View(favorites);
        }
        // GET
        public IActionResult EditProperty(int id)
        {
            ViewBag.States = _context.States.ToList();
            ViewBag.Cities = _context.Cities.ToList();
            ViewBag.Categories = _context.PropertyCategories.ToList();

            var property = _context.Properties.Find(id);

            if (property == null)
                return NotFound();

            return View(property);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditProperty(RealEstateProject.Models.Property model, List<IFormFile> ImageFiles, IFormFile? VideoFile)
        {
            var property = _context.Properties
                .Include(p => p.ProperyImages)
                .FirstOrDefault(x => x.ProperyId == model.ProperyId);

            if (property == null)
                return NotFound();

            // ================= UPDATE FIELDS =================
            property.Title = model.Title;
            property.Description = model.Description;
            property.Price = model.Price;

            property.PropertyType = model.PropertyType;
            property.AreaSqft = model.AreaSqft;
            property.Bedroom = model.Bedroom;
            property.Bathrooms = model.Bathrooms;
            property.Furnishing = model.Furnishing;

            property.Address = model.Address;
            property.Pincode = model.Pincode;

            property.CityId = model.CityId;
            property.CategoryId = model.CategoryId;

            // ================= VIDEO UPDATE =================
            if (VideoFile != null && VideoFile.Length > 0)
            {
                string videoFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/videos");
                if (!Directory.Exists(videoFolder)) Directory.CreateDirectory(videoFolder);
                string videoName = Guid.NewGuid().ToString() + Path.GetExtension(VideoFile.FileName);
                string videoPath = Path.Combine(videoFolder, videoName);
                using (var stream = new FileStream(videoPath, FileMode.Create))
                {
                    VideoFile.CopyTo(stream);
                }
                property.VideoUrl = "/videos/" + videoName;
            }

            // ================= IMAGE UPDATE =================
            if (ImageFiles != null && ImageFiles.Count > 0)
            {
                string folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                // ❗ delete old images (optional but recommended)
                _context.ProperyImages.RemoveRange(property.ProperyImages);
                property.ProperyImages = new List<ProperyImage>();

                foreach (var imageFile in ImageFiles)
                {
                    if (imageFile.Length > 0)
                    {
                        string fileName = Guid.NewGuid() + Path.GetExtension(imageFile.FileName);
                        string filePath = Path.Combine(folder, fileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            imageFile.CopyTo(stream);
                        }

                        property.ProperyImages.Add(new ProperyImage { ImagePath = "/images/" + fileName });
                    }
                }
            }

            // send for approval
            property.Status = "EditPending";

            _context.SaveChanges();

            return RedirectToAction("MyProperties");
        }
        
        public IActionResult PayCommissionUser(int id)
        {
            var userId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
            var enquiry = _context.Enquiries
                .Include(e => e.Property)
                .Include(e => e.SenderUser)
                .Include(e => e.OwnerUser)
                .FirstOrDefault(e => e.EnquiryId == id);

            if (enquiry == null) return NotFound();

            if (enquiry.SenderUserId == userId)
            {
                enquiry.IsBuyerPaid = true;
                AddNotification(enquiry.OwnerUserId.Value, "Buyer has paid the commission.");
            }
            else if (enquiry.OwnerUserId == userId)
            {
                enquiry.IsOwnerPaid = true;
                AddNotification(enquiry.SenderUserId.Value, "Owner has paid the commission.");
            }

            if (enquiry.IsBuyerPaid && enquiry.IsOwnerPaid)
            {
                enquiry.IsCommissionPaid = true;
                enquiry.Stage = 6; // Complete
                enquiry.Status = "Completed";

                // Mark property as sold or rented
                if (enquiry.Property != null)
                    enquiry.Property.Status = string.Equals(enquiry.Property.PropertyType, "Rent", StringComparison.OrdinalIgnoreCase) ? "Rented" : "Sold";

                string dealType = string.Equals(enquiry.Property?.PropertyType, "Rent", StringComparison.OrdinalIgnoreCase) ? "Rented" : "Sold";
                AddNotification(enquiry.SenderUserId.Value, $"Deal Completed! Property has been marked as {dealType}.");
                AddNotification(enquiry.OwnerUserId.Value, $"Deal Completed! Property has been marked as {dealType}.");

                // SEND EMAIL
                string subject = $"Property Sold - #{enquiry.EnquiryId}";

                if (enquiry.SenderUser != null && !string.IsNullOrEmpty(enquiry.SenderUser.Email))
                {
                    string buyerBody = $@"
        <h2>Hello {enquiry.SenderUser.FullName},</h2>
        <p>Congratulations! The property has been successfully sold to you.</p>
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
        <p>Congratulations! Your property has been successfully sold.</p>
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
        <p>A property has been successfully sold.</p>
        <p><strong>Property:</strong> {enquiry.Property?.Title}</p>
        <p><strong>Seller:</strong> {enquiry.OwnerUser?.FullName}</p>
        <p><strong>Buyer:</strong> {enquiry.SenderUser?.FullName}</p>
        <br/>
        <p>Thank you,<br/>System</p>";
                    _emailService.SendEmail(admin.Email, subject, adminBody);
                }
            }

            _context.SaveChanges();
            return RedirectToAction("MyEnquiries");
        }
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
        public IActionResult MyEnquiries()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userIdStr))
                return RedirectToAction("Login", "Account");

            int userId = Convert.ToInt32(userIdStr);

            var enquiries = _context.Enquiries
                .Include(e => e.Property)
                .ThenInclude(p => p.ProperyImages)

                .Include(e => e.SenderUser)
                .Include(e => e.OwnerUser)

                .Where(e =>

                    // only this user's deals
                    (e.SenderUserId == userId ||
                     e.OwnerUserId == userId)

                    // admin approved permanent
                    && e.IsAdminApproved == true

                    // show approved + completed
                    && (e.Status == "Approved"
                        || e.Status == "Completed")

                    // process started
                    && e.Stage > 0
                )

                .OrderByDescending(e => e.EnquiryId)
                .ToList();

            return View(enquiries);
        }
        private byte[] GeneratePremiumInvoice(Enquiry enquiry)
        {
            using (var stream = new MemoryStream())
            {
                var writer = new PdfWriter(stream);
                var pdf = new PdfDocument(writer);
                var document = new Document(pdf);

                var bold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
                var normal = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

                bool isRent = string.Equals(enquiry.Property?.PropertyType, "Rent", StringComparison.OrdinalIgnoreCase);

                // ================= HEADER =================
                var headerTable = new Table(2).UseAllAvailableWidth();

                // Left: Company Name
                headerTable.AddCell(new Cell().Add(new Paragraph("REAL ESTATE Home Lengo CO.")
                    .SetFont(bold).SetFontSize(18))
                    .SetBorder(iText.Layout.Borders.Border.NO_BORDER));

                // Right: Invoice Info
                headerTable.AddCell(new Cell().Add(new Paragraph(
                    $"Invoice ID: #DEAL-{enquiry.EnquiryId}\nDate: {DateTime.Now:dd-MMM-yyyy}")
                    .SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT))
                    .SetBorder(iText.Layout.Borders.Border.NO_BORDER));

                document.Add(headerTable);
                document.Add(new Paragraph("\n"));

                // ================= TITLE =================
                document.Add(new Paragraph("INVOICE")
                    .SetFont(bold)
                    .SetFontSize(20)
                    .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));

                document.Add(new Paragraph("\n"));

                // ================= PROPERTY =================
                document.Add(new Paragraph("PROPERTY DETAILS").SetFont(bold));
                document.Add(new Paragraph($"Title: {enquiry.Property?.Title}").SetFont(normal));
                document.Add(new Paragraph($"Type: {enquiry.Property?.PropertyType}").SetFont(normal));
                document.Add(new Paragraph("--------------------------------------------------"));

                // ================= STAKEHOLDERS =================
                document.Add(new Paragraph("STAKEHOLDERS").SetFont(bold));
                document.Add(new Paragraph($"{(isRent ? "Tenant" : "Buyer")}: {enquiry.SenderUser?.FullName}"));
                document.Add(new Paragraph($"Owner/Seller: {enquiry.OwnerUser?.FullName}"));
                document.Add(new Paragraph("--------------------------------------------------"));

                // ================= TABLE =================
                document.Add(new Paragraph("PAYMENT SUMMARY").SetFont(bold));

                var table = new Table(2).UseAllAvailableWidth();
                table.AddHeaderCell("Description");
                table.AddHeaderCell("Amount (₹)");

                decimal total = 0;

                if (isRent)
                {
                    decimal deposit = enquiry.SecurityDepositAmount ?? 0;
                    decimal firstRent = enquiry.FirstMonthRentAmount ?? 0;
                    decimal agreement = enquiry.AgreementChargesAmount ?? 2500;
                    decimal brokerage = enquiry.BrokerageChargesAmount ?? 0;

                    table.AddCell("Security Deposit (3 Months)");
                    table.AddCell(deposit.ToString("N2"));

                    table.AddCell("First Month Rent");
                    table.AddCell(firstRent.ToString("N2"));

                    table.AddCell("Agreement Charges");
                    table.AddCell(agreement.ToString("N2"));

                    table.AddCell("Brokerage Fee");
                    table.AddCell(brokerage.ToString("N2"));

                    total = deposit + firstRent + agreement + brokerage;
                }
                else
                {
                    decimal price = enquiry.PropertyPrice ?? 0;
                    decimal token = 15000;
                    decimal commission = price * 0.02M; // Total 2% as per controller logic

                    table.AddCell("Property Selling Price");
                    table.AddCell(price.ToString("N2"));

                    table.AddCell("Token Amount (Fixed)");
                    table.AddCell(token.ToString("N2"));

                    table.AddCell("Total Commission (2%)");
                    table.AddCell(commission.ToString("N2"));

                    total = price + commission;
                }

                // Total Row
                table.AddCell(new Cell().Add(new Paragraph("TOTAL SETTLEMENT").SetFont(bold)));
                table.AddCell(new Cell().Add(new Paragraph(total.ToString("N2")).SetFont(bold)));

                document.Add(table);
                document.Add(new Paragraph("\n"));

                // ================= STATUS =================
                string statusText = isRent ? "STATUS: LEASE ACTIVE & RENTED" : "STATUS: COMPLETED & SOLD";
                document.Add(new Paragraph(statusText)
                    .SetFont(bold)
                    .SetFontColor(iText.Kernel.Colors.ColorConstants.GRAY)
                    .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));

                document.Add(new Paragraph("\n"));

                // ================= FOOTER =================
                document.Add(new Paragraph("Thank you for choosing Real Estate Co.")
                    .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));

                document.Close();
                return stream.ToArray();
            }
        }
        public IActionResult DownloadInvoice(int id)
        {
            var enquiry = _context.Enquiries
                .Include(e => e.Property)
                .Include(e => e.SenderUser)
                .Include(e => e.OwnerUser)
                .FirstOrDefault(e => e.EnquiryId == id);

            if (enquiry == null || (enquiry.Stage < 6)) // Allow 6 for Rent and 7 for Sale
                return NotFound();

            var pdfBytes = GeneratePremiumInvoice(enquiry);

            return File(pdfBytes, "application/pdf", $"Invoice_{id}.pdf");
        }

        public IActionResult InitiatePayment(int id, string paymentType)
        {
            var userId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));

            var enquiry = _context.Enquiries
                .Include(e => e.Property)
                .Include(e => e.SenderUser)
                .Include(e => e.OwnerUser)
                .FirstOrDefault(e => e.EnquiryId == id);

            if (enquiry == null)
                return NotFound();

            decimal amount = 0;

            // ================= COMMON =================
            if (paymentType == "Token")
                amount = 15000;

            else if (paymentType == "Commission")
                amount = (enquiry.PropertyPrice ?? 0) * 0.001M; // 1% commission from each side (Total 2%)

            // ================= RENT =================
            else if (paymentType == "RentDepositAndFirstMonth")
                amount = (enquiry.PropertyPrice ?? 0) * 0.001M + 15000; // 3 Months Deposit + 1 Month Rent + Agreement

            else if (paymentType == "RentBrokerage")
                amount = (enquiry.PropertyPrice ?? 0) *0.002M +5000; // 1 Month Brokerage

            if (amount <= 0)
                return BadRequest("Invalid payment amount");

            try
            {
                string keyId = _config["Razorpay:Key"];
                string keySecret = _config["Razorpay:Secret"];

                var client = new RazorpayClient(keyId, keySecret);

                var orderOptions = new Dictionary<string, object>
        {
            { "amount", (int)(amount * 100) },
            { "currency", "INR" },
            { "receipt", $"enq_{enquiry.EnquiryId}_{paymentType}" }
        };

                var order = client.Order.Create(orderOptions);
                string orderId = order["id"].ToString();

                ViewBag.OrderId = orderId;
                ViewBag.KeyId = keyId;
                ViewBag.Amount = amount;
                ViewBag.PaymentType = paymentType;

                return View("RazorpayCheckout", enquiry);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Payment gateway error: " + ex.Message;
                return RedirectToAction("MyEnquiries");
            }
        }

        //public IActionResult InitiatePayment(int id, string paymentType)
        //{
        //    var userId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
        //    var enquiry = _context.Enquiries
        //        .Include(e => e.Property)
        //        .Include(e => e.SenderUser)
        //        .Include(e => e.OwnerUser)
        //        .FirstOrDefault(e => e.EnquiryId == id);

        //    if (enquiry == null)
        //        return NotFound();

        //    // Calculate amount based on payment type
        //    decimal amount = 0;

        //    if (paymentType == "Token")
        //        amount = 15000;
        //    else if (paymentType == "Commission")
        //        amount = ((enquiry.PropertyPrice ?? 0) * 0.001M) / 2M;
        //    else if (paymentType == "SecurityDeposit")
        //        amount = (enquiry.PropertyPrice ?? 0) * 0.001M;
        //    else if (paymentType == "FirstRentAndAgreement")
        //        amount = (enquiry.PropertyPrice ?? 0) *001M;
        //    else if (paymentType == "FirstMonthRent")
        //        amount = enquiry.PropertyPrice ?? 0;
        //    else if (paymentType == "RentalAgreementCharges")
        //        amount = 2500;
        //    else if (paymentType == "BrokerageCharges")
        //        amount = enquiry.PropertyPrice ?? 0;
        //    else if (paymentType == "MonthlyRent")
        //        amount = enquiry.PropertyPrice ?? 0;

        //    if (amount <= 0)
        //        return BadRequest("Invalid payment amount");

        //    // ✅ CREATE RAZORPAY ORDER
        //    try
        //    {
        //        string keyId = _config["Razorpay:Key"];
        //        string keySecret = _config["Razorpay:Secret"];
        //        var client = new RazorpayClient(keyId, keySecret);

        //        var orderOptions = new Dictionary<string, object>
        //        {
        //            { "amount", (int)(amount * 100) },
        //            { "currency", "INR" },
        //            { "receipt", $"enq_{enquiry.EnquiryId}_{paymentType}" }
        //        };

        //        var order = client.Order.Create(orderOptions);
        //        string orderId = order["id"].ToString();

        //        ViewBag.OrderId = orderId;
        //        ViewBag.KeyId = keyId;
        //        ViewBag.Amount = amount;
        //        ViewBag.PaymentType = paymentType;

        //        return View("RazorpayCheckout", enquiry);
        //    }
        //    catch (Exception ex)
        //    {
        //        TempData["Error"] = "Payment gateway error: " + ex.Message;
        //        return RedirectToAction("MyEnquiries");
        //    }
        //}

        // ✅ RAZORPAY PAYMENT VERIFICATION (called after Razorpay checkout completes)
        
        [HttpPost]
        public IActionResult VerifyPayment(string razorpay_payment_id,
    string razorpay_order_id,
    string razorpay_signature,
    int enquiryId,
    string paymentType)
        {
            var userId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));

            var enquiry = _context.Enquiries
                .Include(e => e.Property)
                .Include(e => e.SenderUser)
                .Include(e => e.OwnerUser)
                .FirstOrDefault(e => e.EnquiryId == enquiryId);

            if (enquiry == null)
                return NotFound();

            enquiry.RazorpayPaymentId = razorpay_payment_id;
            enquiry.RazorpayOrderId = razorpay_order_id;

            // ================= TOKEN =================
            if (paymentType == "Token")
            {
                enquiry.IsTokenPaid = true;
                enquiry.IsTokenPaidByBuyer = true;

                if (enquiry.OwnerUserId.HasValue)
                    AddNotification(enquiry.OwnerUserId.Value, "Buyer paid token ₹15,000.");

                if (enquiry.SenderUserId.HasValue)
                    AddNotification(enquiry.SenderUserId.Value, "Token payment successful.");
            }

            // ================= COMMISSION =================
            if (paymentType == "Commission")
            {
                if (enquiry.SenderUserId == userId)
                {
                    enquiry.IsCommissionPaidByBuyer = true;
                    if (enquiry.OwnerUserId.HasValue)
                        AddNotification(enquiry.OwnerUserId.Value, "Buyer has paid their share of commission.");
                }

                if (enquiry.OwnerUserId == userId)
                {
                    enquiry.IsCommissionPaidByOwner = true;
                    if (enquiry.SenderUserId.HasValue)
                        AddNotification(enquiry.SenderUserId.Value, "Owner has paid their share of commission.");
                }

                if (enquiry.IsCommissionPaidByBuyer && enquiry.IsCommissionPaidByOwner)
                {
                    enquiry.IsCommissionPaid = true;
                    bool isRent = string.Equals(enquiry.Property?.PropertyType, "Rent", StringComparison.OrdinalIgnoreCase);
                    enquiry.Stage = isRent ? 6 : 7;
                    enquiry.Status = "Completed";

                    if (enquiry.Property != null)
                        enquiry.Property.Status = isRent ? "Rented" : "Sold";

                    if (enquiry.SenderUserId.HasValue)
                        AddNotification(enquiry.SenderUserId.Value, "Deal Completed! Both parties have paid commission.");

                    if (enquiry.OwnerUserId.HasValue)
                        AddNotification(enquiry.OwnerUserId.Value, "Deal Completed! Both parties have paid commission.");
                }
            }

            // ================= RENT - STEP 1 =================
            if (paymentType == "RentDepositAndFirstMonth")
            {
                enquiry.IsSecurityDepositPaid = true;
                enquiry.IsFirstMonthRentPaid = true;
                enquiry.Stage = 6; // Move to Stage 6: Pay Brokerage

                if (enquiry.OwnerUserId.HasValue)
                    AddNotification(enquiry.OwnerUserId.Value, "Tenant paid deposit + first month rent.");

                if (enquiry.SenderUserId.HasValue)
                    AddNotification(enquiry.SenderUserId.Value, "Payment successful (Deposit + Rent). Proceed to Brokerage payment.");
            }

            // ================= RENT - STEP 2 =================
            if (paymentType == "RentBrokerage")
            {
                enquiry.IsBrokeragePaid = true;
                enquiry.Stage = 6; // Final Stage reached
                enquiry.Status = "Completed";

                if (enquiry.Property != null)
                    enquiry.Property.Status = "Rented";

                if (enquiry.OwnerUserId.HasValue)
                    AddNotification(enquiry.OwnerUserId.Value, "Brokerage received. Rent completed.");

                if (enquiry.SenderUserId.HasValue)
                    AddNotification(enquiry.SenderUserId.Value, "Rental deal completed.");
            }

            _context.SaveChanges();

            return RedirectToAction("PaymentSuccess", new { id = enquiry.EnquiryId });
        }
        //public IActionResult VerifyPayment(string razorpay_payment_id, string razorpay_order_id, string razorpay_signature, int enquiryId, string paymentType)
        //{
        //    var userId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
        //    var enquiry = _context.Enquiries
        //        .Include(e => e.Property)
        //        .Include(e => e.SenderUser)
        //        .Include(e => e.OwnerUser)
        //        .FirstOrDefault(e => e.EnquiryId == enquiryId);

        //    if (enquiry == null)
        //        return NotFound();

        //    // Store Razorpay IDs
        //    enquiry.RazorpayPaymentId = razorpay_payment_id;
        //    enquiry.RazorpayOrderId = razorpay_order_id;

        //    bool isRent = string.Equals(enquiry.Property?.PropertyType, "Rent", StringComparison.OrdinalIgnoreCase);

        //    // =====================================================
        //    // =============== SELL PAYMENTS ========================
        //    // =====================================================

        //    if (paymentType == "Token")
        //    {
        //        enquiry.IsTokenPaidByBuyer = true;
        //        enquiry.IsTokenPaid = true;
        //        AddNotification(enquiry.OwnerUserId.Value, "Buyer paid token ₹15,000 via Razorpay.");
        //        AddNotification(enquiry.SenderUserId.Value, "Token payment of ₹15,000 completed successfully.");
        //    }

        //    if (paymentType == "Commission")
        //    {
        //        if (enquiry.SenderUserId == userId)
        //        {
        //            enquiry.IsCommissionPaidByBuyer = true;
        //            AddNotification(enquiry.OwnerUserId.Value, "Buyer paid commission");
        //        }
        //        if (enquiry.OwnerUserId == userId)
        //        {
        //            enquiry.IsCommissionPaidByOwner = true;
        //            AddNotification(enquiry.SenderUserId.Value, "Owner paid commission");
        //        }

        //        if (enquiry.IsCommissionPaidByBuyer && enquiry.IsCommissionPaidByOwner)
        //        {
        //            enquiry.IsCommissionPaid = true;
        //            enquiry.Stage = 6;
        //            enquiry.Status = "Completed";
        //            if (enquiry.Property != null) enquiry.Property.Status = "Sold";

        //            AddNotification(enquiry.SenderUserId.Value, "Deal completed successfully");
        //            AddNotification(enquiry.OwnerUserId.Value, "Deal completed successfully");
        //        }
        //    }

        //    // =====================================================
        //    // =============== RENT PAYMENTS ========================
        //    // =====================================================

        //    if (paymentType == "SecurityDeposit")
        //    {
        //        enquiry.IsSecurityDepositPaid = true;
        //        enquiry.Stage = 5;
        //        AddNotification(enquiry.OwnerUserId.Value, "Tenant paid Security Deposit.");
        //        AddNotification(enquiry.SenderUserId.Value, "Deposit payment completed. Please proceed to pay First Month Rent & Agreement charges.");
        //    }

        //    if (paymentType == "FirstRentAndAgreement")
        //    {
        //        enquiry.IsFirstMonthRentPaid = true;
        //        enquiry.IsAgreementChargesPaid = true;
        //        enquiry.Stage = 6;
        //        enquiry.BrokerageChargesAmount = enquiry.PropertyPrice;
        //        enquiry.IsBrokeragePaid = false;
        //        AddNotification(enquiry.OwnerUserId.Value, "Tenant paid First Month Rent and Agreement Charges.");
        //        AddNotification(enquiry.SenderUserId.Value, "First Rent & Agreement payment completed. Please pay Brokerage charges to activate lease.");
        //    }

        //    if (paymentType == "FirstMonthRent")
        //    {
        //        enquiry.IsFirstMonthRentPaid = true;
        //        if (enquiry.IsSecurityDepositPaid && enquiry.IsAgreementChargesPaid)
        //        {
        //            enquiry.Stage = 6;
        //            enquiry.BrokerageChargesAmount = enquiry.PropertyPrice;
        //            enquiry.IsBrokeragePaid = false;
        //        }
        //        AddNotification(enquiry.OwnerUserId.Value, "Tenant paid first month rent.");
        //        AddNotification(enquiry.SenderUserId.Value, "First rent payment completed.");
        //    }

        //    if (paymentType == "RentalAgreementCharges")
        //    {
        //        enquiry.IsAgreementChargesPaid = true;
        //        if (enquiry.IsSecurityDepositPaid && enquiry.IsFirstMonthRentPaid)
        //        {
        //            enquiry.Stage = 6;
        //            enquiry.BrokerageChargesAmount = enquiry.PropertyPrice;
        //            enquiry.IsBrokeragePaid = false;
        //        }
        //        AddNotification(enquiry.OwnerUserId.Value, "Tenant paid agreement fees.");
        //        AddNotification(enquiry.SenderUserId.Value, "Agreement fees payment completed.");
        //    }

        //    if (paymentType == "BrokerageCharges")
        //    {
        //        enquiry.IsBrokeragePaid = true;
        //        enquiry.Stage = 7;
        //        enquiry.Status = "Completed";
        //        // ✅ FIX: Set "Rented" instead of "Sold" for rent properties
        //        if (enquiry.Property != null) enquiry.Property.Status = "Rented";

        //        AddNotification(enquiry.OwnerUserId.Value, "Brokerage paid. Lease is active!");
        //        AddNotification(enquiry.SenderUserId.Value, "Brokerage paid. Lease is active!");
        //    }

        //    if (paymentType == "MonthlyRent")
        //    {
        //        enquiry.IsMonthlyRentPaid = true;
        //        AddNotification(enquiry.OwnerUserId.Value, "Tenant paid monthly rent.");
        //        AddNotification(enquiry.SenderUserId.Value, "Rent paid successfully.");
        //    }

        //    _context.SaveChanges();

        //    return RedirectToAction("PaymentSuccess", new { id = enquiry.EnquiryId });
        //}


        public IActionResult PaymentSuccess(int id)
        {
            var enquiry = _context.Enquiries
                .Include(e => e.Property)
                .FirstOrDefault(e => e.EnquiryId == id);

            if (enquiry == null) return NotFound();

            return View(enquiry);
        }



        public IActionResult Profile()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userIdStr))
                return RedirectToAction("Login", "Account");

            int userId = Convert.ToInt32(userIdStr);

            var user = _context.Users.FirstOrDefault(u => u.UserId == userId);

            if (user == null)
                return NotFound();

            return View(user);
        }
        [HttpPost]
        public IActionResult UpdateProfile(User model, IFormFile ImageFile)
        {
            var user = _context.Users.Find(model.UserId);

            if (user == null)
                return NotFound();

            user.FullName = model.FullName;
            user.Email = model.Email;
            user.Phone = model.Phone;

            // ✅ PROFILE IMAGE UPLOAD
            if (ImageFile != null)
            {
                string folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/profile");

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string fileName = Guid.NewGuid() + Path.GetExtension(ImageFile.FileName);
                string filePath = Path.Combine(folder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    ImageFile.CopyTo(stream);
                }

                user.ProfileImage = "/images/profile/" + fileName;
            }

            _context.SaveChanges();

            TempData["Success"] = "Profile updated successfully!";
            return RedirectToAction("Profile");
        }
        public IActionResult Dashboard()
        {
            var userId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));

            var data = _context.Properties
                .Include(p => p.City)
                .Include(p => p.ProperyImages)
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .ToList();

            return View(data); // this will open Dashboard.cshtml
        }
        public IActionResult RequestDelete(int id)
        {
            var userId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));

            var property = _context.Properties
                .FirstOrDefault(x => x.ProperyId == id && x.UserId == userId);

            if (property == null)
                return NotFound();

            // send request to admin
            property.Status = "DeleteRequested";

            _context.SaveChanges();

            TempData["Success"] = "Delete request sent to admin.";

            return RedirectToAction("MyProperties");
        }
         public IActionResult Listing(
                    string location,
                    string propertyType,
                    int? bedrooms,
                    int? bathrooms,
                    decimal? minPrice,
                    decimal? maxPrice,
                    int? minSqft,
                    int? maxSqft,
                    List<int> amenities,
                    string categoryName,
                    string sortOrder
                )
                    {
                        var data = _context.Properties
                            .Include(p => p.Category)
                            .Include(p => p.City)
                            .Include(p => p.ProperyImages)
                            .Include(p => p.User)
                            .Include(p => p.PropertyAmenities)
                            .Where(p => p.Status == "Approved");

                        // 🔍 LOCATION
                        if (!string.IsNullOrEmpty(location))
                        {
                            data = data.Where(x => x.Address.Contains(location) || 
                                                   (x.City != null && x.City.CityName.Contains(location)) || 
                                                   x.Title.Contains(location));
                        }

                        // 🔍 PROPERTY TYPE (Buy/Rent mapped to Sell/Rent)
                        if (!string.IsNullOrEmpty(propertyType))
                        {
                            string dbPropertyType = propertyType.Equals("Buy", StringComparison.OrdinalIgnoreCase) ? "Sell" : propertyType;
                            data = data.Where(x => x.PropertyType == dbPropertyType);
                        }

            // 🔍 BEDROOM
            if (bedrooms.HasValue)
                data = data.Where(x => x.Bedroom == bedrooms);

            // 🔍 BATHROOM
            if (bathrooms.HasValue)
                data = data.Where(x => x.Bathrooms == bathrooms);

            // 🔍 PRICE
            if (minPrice.HasValue)
                data = data.Where(x => x.Price >= minPrice);

            if (maxPrice.HasValue)
                data = data.Where(x => x.Price <= maxPrice);

            // 🔍 SQFT
            if (minSqft.HasValue)
                data = data.Where(x => x.AreaSqft >= minSqft);

            if (maxSqft.HasValue)
                data = data.Where(x => x.AreaSqft <= maxSqft);

            // 🔍 CATEGORY NAME (Apartment/Villa/House)
            if (!string.IsNullOrEmpty(categoryName))
                data = data.Where(x => x.Category.CategoryName == categoryName);

            // 🔍 AMENITIES (MOST IMPORTANT)
            if (amenities != null && amenities.Any())
            {
                data = data.Where(p =>
                    p.PropertyAmenities.Any(pa => amenities.Contains(pa.AmenityId))
                );
            }

            // 🔍 SORTING
            switch (sortOrder)
            {
                case "price_asc":
                    data = data.OrderBy(x => x.Price);
                    break;
                case "price_desc":
                    data = data.OrderByDescending(x => x.Price);
                    break;
                case "oldest":
                    data = data.OrderBy(x => x.CreatedAt);
                    break;
                case "newest":
                default:
                    data = data.OrderByDescending(x => x.CreatedAt);
                    break;
            }

            // ✅ SEND AMENITIES TO VIEW
            ViewBag.Amenities = _context.Amenities.ToList();

            return View("Listing", data.ToList());
        }
        [HttpGet]
        public JsonResult GetCitiesByState(int stateId)
        {
            var cities = _context.Cities
                .Where(c => c.StateId == stateId)
                .Select(c => new
                {
                    cityId = c.CityId,
                    cityName = c.CityName
                })
                .ToList();

            return Json(cities);
        }

        // ===================================================
        // ============= RENTAL SITE VISIT SYSTEM ============
        // ===================================================

        [HttpPost]
        public IActionResult RequestVisit(int id, DateTime preferredDate, string preferredTime)
        {
            var enquiry = _context.Enquiries
                .Include(e => e.Property)
                .Include(e => e.SenderUser)
                .Include(e => e.OwnerUser)
                .FirstOrDefault(e => e.EnquiryId == id);

            if (enquiry == null) return NotFound();

            enquiry.PreferredVisitDate = preferredDate;
            enquiry.PreferredVisitTime = preferredTime;
            enquiry.VisitStatus = "Requested";
            enquiry.Stage = 2; // Stage 2: Property Visiting

            if (enquiry.OwnerUserId.HasValue)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = enquiry.OwnerUserId.Value,
                    Message = $"Site visit requested for {preferredDate:yyyy-MM-dd} at {preferredTime}.",
                    IsRead = false,
                    CreatedAt = DateTime.Now,
                    EnquiryId = enquiry.EnquiryId
                });

                // SEND EMAIL TO OWNER
                if (enquiry.OwnerUser != null && !string.IsNullOrEmpty(enquiry.OwnerUser.Email))
                {
                    string subject = $"New Site Visit Request - {enquiry.Property?.Title}";
                    string emailBody = $@"
                        <h2>Hello {enquiry.OwnerUser.FullName},</h2>
                        <p>A prospective tenant has requested a site visit on your rental property.</p>
                        <p><strong>Property:</strong> {enquiry.Property?.Title}</p>
                        <p><strong>Preferred Date:</strong> {preferredDate:yyyy-MM-dd}</p>
                        <p><strong>Preferred Time Slot:</strong> {preferredTime}</p>
                        <br/>
                        <p>Please log in to your dashboard to review, approve, or reject this visit request.</p>
                        <p>Thank you,<br/>RealEstate Team</p>";
                    _emailService.SendEmail(enquiry.OwnerUser.Email, subject, emailBody);
                }
            }

            _context.SaveChanges();
            TempData["Success"] = "Site visit request submitted successfully!";
            return RedirectToAction("MyEnquiries");
        }

        [HttpPost]
        public IActionResult RespondToVisit(int id, string action)
        {
            var enquiry = _context.Enquiries
                .Include(e => e.Property)
                .Include(e => e.SenderUser)
                .Include(e => e.OwnerUser)
                .FirstOrDefault(e => e.EnquiryId == id);

            if (enquiry == null) return NotFound();

            string statusText = action == "Approve" ? "Approved" : "Rejected";

            if (action == "Approve")
            {
                enquiry.VisitStatus = "Approved";
                enquiry.Stage = 2; // Site Visited & Approved (Stage 2)

                if (enquiry.SenderUserId.HasValue)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = enquiry.SenderUserId.Value,
                        Message = $"Owner approved your site visit request! Scheduled for {enquiry.PreferredVisitDate?.ToString("yyyy-MM-dd")} at {enquiry.PreferredVisitTime}.",
                        IsRead = false,
                        CreatedAt = DateTime.Now,
                        EnquiryId = enquiry.EnquiryId
                    });
                }
                TempData["Success"] = "Site visit request approved!";
            }
            else
            {
                enquiry.VisitStatus = "Rejected";

                if (enquiry.SenderUserId.HasValue)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = enquiry.SenderUserId.Value,
                        Message = "Owner rejected your site visit schedule request.",
                        IsRead = false,
                        CreatedAt = DateTime.Now,
                        EnquiryId = enquiry.EnquiryId
                    });
                }
                TempData["Error"] = "Site visit request rejected.";
            }

            // SEND EMAIL TO TENANT
            if (enquiry.SenderUser != null && !string.IsNullOrEmpty(enquiry.SenderUser.Email))
            {
                string subject = $"Site Visit Request {statusText} - {enquiry.Property?.Title}";
                string statusDetails = action == "Approve" 
                    ? $"The owner has approved your visit request. Your scheduled slot is on <strong>{enquiry.PreferredVisitDate?.ToString("yyyy-MM-dd")}</strong> during <strong>{enquiry.PreferredVisitTime}</strong>."
                    : "Unfortunately, the owner has rejected your requested visit slot. Please log in to request a different date or time.";

                string emailBody = $@"
                    <h2>Hello {enquiry.SenderUser.FullName},</h2>
                    <p>{statusDetails}</p>
                    <p><strong>Property:</strong> {enquiry.Property?.Title}</p>
                    <br/>
                    <p>Thank you,<br/>RealEstate Team</p>";
                _emailService.SendEmail(enquiry.SenderUser.Email, subject, emailBody);
            }

            _context.SaveChanges();
            return RedirectToAction("MyEnquiries");
        }

        [HttpPost]
        public IActionResult ProceedToStartDeal(int id)
        {
            var enquiry = _context.Enquiries
                .Include(e => e.Property)
                .Include(e => e.SenderUser)
                .Include(e => e.OwnerUser)
                .FirstOrDefault(e => e.EnquiryId == id);

            if (enquiry == null) return NotFound();

            enquiry.Stage = 3; // Stage 3: Start Deal
            enquiry.PropertyPrice = enquiry.Property?.Price;

            enquiry.IsDocumentUploaded = false;
            enquiry.IsDocumentApprovedByAdmin = false;

            bool isRent = string.Equals(enquiry.Property?.PropertyType, "Rent", StringComparison.OrdinalIgnoreCase);
            string roleName = isRent ? "tenant" : "buyer";

            if (enquiry.SenderUserId.HasValue)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = enquiry.SenderUserId.Value,
                    Message = $"Owner has started the official deal. Please upload your identity photo for Stage 4.",
                    IsRead = false,
                    CreatedAt = DateTime.Now,
                    EnquiryId = enquiry.EnquiryId
                });

                // SEND EMAIL
                if (enquiry.SenderUser != null && !string.IsNullOrEmpty(enquiry.SenderUser.Email))
                {
                    string subject = $"Official Deal Started - {enquiry.Property?.Title}";
                    string emailBody = $@"
                        <h2>Hello {enquiry.SenderUser.FullName},</h2>
                        <p>The owner has officially started the deal process. Please log in to upload your identity photo to proceed to the next stage.</p>
                        <p><strong>Property:</strong> {enquiry.Property?.Title}</p>
                        <br/>
                        <p>Thank you,<br/>RealEstate Team</p>";
                    _emailService.SendEmail(enquiry.SenderUser.Email, subject, emailBody);
                }
            }

            _context.SaveChanges();
            TempData["Success"] = "Advanced to Start Deal stage!";
            return RedirectToAction("MyEnquiries");
        }

        [HttpPost]
        public IActionResult AdvanceToDocuments(int id)
        {
            var enquiry = _context.Enquiries.Find(id);
            if (enquiry == null) return NotFound();
            enquiry.Stage = 4;
            _context.SaveChanges();
            return RedirectToAction("MyEnquiries");
        }

        // ===================================================
        // =========== TENANT DOCUMENT VERIFICATION ==========
        // ===================================================

        [HttpPost]
        public IActionResult UploadDocuments(int id, IFormFile documentFile)
        {
            var enquiry = _context.Enquiries
                .Include(e => e.Property)
                .Include(e => e.SenderUser)
                .Include(e => e.OwnerUser)
                .FirstOrDefault(e => e.EnquiryId == id);

            if (enquiry == null) return NotFound();

            if (documentFile != null && documentFile.Length > 0)
            {
                var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "verification");
                if (!Directory.Exists(uploadsDir))
                {
                    Directory.CreateDirectory(uploadsDir);
                }

                var fileName = "Enquiry_" + enquiry.EnquiryId + "_" + Path.GetFileName(documentFile.FileName);
                var filePath = Path.Combine(uploadsDir, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    documentFile.CopyTo(stream);
                }

                enquiry.DocumentPath = "/uploads/verification/" + fileName;
                enquiry.IsDocumentUploaded = true;
                enquiry.IsDocumentApprovedByAdmin = false;
                enquiry.Stage = 4; // Stage 4: Document

                bool isRent = string.Equals(enquiry.Property?.PropertyType, "Rent", StringComparison.OrdinalIgnoreCase);
                string partyRole = isRent ? "Tenant" : "Buyer";

                // Notify admin
                var admin = _context.Users.FirstOrDefault(u => u.Role == "Admin");
                if (admin != null)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = admin.UserId,
                        Message = $"{partyRole} {enquiry.SenderUser?.FullName} has uploaded documents for property {enquiry.Property?.Title}. Review now.",
                        IsRead = false,
                        CreatedAt = DateTime.Now,
                        EnquiryId = enquiry.EnquiryId,
                        Type = "Admin"
                    });
                }

                _context.SaveChanges();
                TempData["Success"] = "Documents uploaded! Waiting for verification.";
            }
            else
            {
                TempData["Error"] = "Please select a valid document file.";
            }

            return RedirectToAction("MyEnquiries");
        }

        [HttpPost]
        public IActionResult VerifyDocuments(int id, string action)
        {
            var enquiry = _context.Enquiries
                .Include(e => e.Property)
                .Include(e => e.SenderUser)
                .Include(e => e.OwnerUser)
                .FirstOrDefault(e => e.EnquiryId == id);

            if (enquiry == null) return NotFound();

            string statusText = action == "Approve" ? "Approved" : "Rejected";

            if (action == "Approve")
            {
                enquiry.IsDocumentApprovedByAdmin = true;
                bool isRent = string.Equals(enquiry.Property?.PropertyType, "Rent", StringComparison.OrdinalIgnoreCase);
                
                if (isRent)
                {
                    enquiry.Stage = 5; // Stage 5: Pay Deposit & 1 Month Rent
                    enquiry.SecurityDepositAmount = (enquiry.PropertyPrice ?? 0) * 3;
                    enquiry.FirstMonthRentAmount = enquiry.PropertyPrice;
                    enquiry.AgreementChargesAmount = 2500;

                    enquiry.IsSecurityDepositPaid = false;
                    enquiry.IsFirstMonthRentPaid = false;
                    enquiry.IsAgreementChargesPaid = false;
                }
                else
                {
                    enquiry.Stage = 5; // Stage 5: Pay Token for Sale
                    enquiry.TokenAmount = 15000;
                    enquiry.IsTokenPaid = false;
                }

                if (enquiry.SenderUserId.HasValue)
                {
                    string msg = isRent ? "Documents verified! Please proceed to pay Security Deposit & Advance payments." : "Documents verified! Please proceed to pay Token Amount ₹15,000.";
                    _context.Notifications.Add(new Notification
                    {
                        UserId = enquiry.SenderUserId.Value,
                        Message = msg,
                        IsRead = false,
                        CreatedAt = DateTime.Now,
                        EnquiryId = enquiry.EnquiryId
                    });
                }
                TempData["Success"] = "Documents verified and approved!";
            }
            else
            {
                enquiry.IsDocumentUploaded = false;
                enquiry.DocumentPath = null;

                if (enquiry.SenderUserId.HasValue)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = enquiry.SenderUserId.Value,
                        Message = "Verification documents rejected. Re-upload.",
                        IsRead = false,
                        CreatedAt = DateTime.Now,
                        EnquiryId = enquiry.EnquiryId
                    });
                }
                TempData["Error"] = "Documents rejected.";
            }

            // SEND EMAIL TO TENANT
            if (enquiry.SenderUser != null && !string.IsNullOrEmpty(enquiry.SenderUser.Email))
            {
                string subject = $"Identity Documents {statusText} - {enquiry.Property?.Title}";
                string details = action == "Approve"
                    ? "The owner has approved your identity documents! Your lease deal has progressed. Please log in to pay your Security Deposit and advance rents to finalize the lease agreement."
                    : "Unfortunately, your identity documents were rejected. Please log in and upload a valid, clear image of your Aadhaar Card, PAN Card, or Passport.";

                string emailBody = $@"
                    <h2>Hello {enquiry.SenderUser.FullName},</h2>
                    <p>{details}</p>
                    <p><strong>Property:</strong> {enquiry.Property?.Title}</p>
                    <br/>
                    <p>Thank you,<br/>RealEstate Team</p>";
                _emailService.SendEmail(enquiry.SenderUser.Email, subject, emailBody);
            }

            _context.SaveChanges();
            return RedirectToAction("MyEnquiries");
        }

        // ================= CLOSE RENT DEAL (TENANT REQUEST) =================
        [HttpPost]
        public IActionResult RequestCloseRentDeal(int id)
        {
            var enquiry = _context.Enquiries
                .Include(e => e.Property)
                .Include(e => e.SenderUser)
                .Include(e => e.OwnerUser)
                .FirstOrDefault(e => e.EnquiryId == id);

            if (enquiry == null) return NotFound();

            // Set the flag so admin can see the Close Deal button
            enquiry.IsCloseRentRequested = true;

            // Notify Admin
            var admin = _context.Users.FirstOrDefault(u => u.Role == "Admin");
            if (admin != null)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = admin.UserId,
                    Message = $"🔴 Tenant {enquiry.SenderUser?.FullName} has requested to close the rent deal for \"{enquiry.Property?.Title}\". Please review and close the deal.",
                    IsRead = false,
                    CreatedAt = DateTime.Now,
                    EnquiryId = enquiry.EnquiryId,
                    Type = "Admin",
                    Status = "Unread"
                });
            }

            // Notify Owner
            if (enquiry.OwnerUserId.HasValue)
            {
                AddNotification(enquiry.OwnerUserId.Value, $"Tenant {enquiry.SenderUser?.FullName} has requested to close the rent deal for \"{enquiry.Property?.Title}\".");
            }

            _context.SaveChanges();
            TempData["Success"] = "Close deal request sent to admin successfully.";
            return RedirectToAction("MyEnquiries");
        }



    }
}






