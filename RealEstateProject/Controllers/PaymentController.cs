using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateProject.Models;
using RealEstateProject.Services;
using iText.IO.Font.Constants;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using System.IO;

namespace RealEstateProject.Controllers
{
    public class PaymentController : Controller
    {
        private readonly RealEstateProjectContext _context;
        private readonly EmailService _emailService;

        public PaymentController(RealEstateProjectContext context, EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // 🔹 Open Commission Page
        public IActionResult Commission(int id)
        {
            var enquiry = _context.Enquiries
                .Include(e => e.Property)
                .Include(e => e.SenderUser)
                .Include(e => e.OwnerUser)
                .FirstOrDefault(e => e.EnquiryId == id);

            return View(enquiry);
        }

        // 🔹 Payment Logic
        [HttpPost]
        public IActionResult Pay(int enquiryId, string role)
        {
            var enquiry = _context.Enquiries
                .Include(e => e.Property)
                .Include(e => e.SenderUser)
                .Include(e => e.OwnerUser)
                .FirstOrDefault(e => e.EnquiryId == enquiryId);

            if (enquiry == null)
                return NotFound();

            // ✔️ Mark Payment
            if (role == "Buyer")
                enquiry.IsBuyerPaid = true;

            if (role == "Owner")
                enquiry.IsOwnerPaid = true;

            // 🔥 If BOTH Paid → Complete Process
            if (enquiry.IsBuyerPaid && enquiry.IsOwnerPaid)
            {
                enquiry.Stage = 6;

                // 🔹 Create Transaction
                var transaction = new Transection
                {
                    PropertyId = enquiry.PropertyId ?? 0,
                    UserId = enquiry.SenderUserId ?? 0,
                    FinalPrice = (decimal)enquiry.PropertyPrice,
                    CommissionPercentage = 2,
                    CommissionAmount = (decimal)enquiry.AdminCommission,
                    TransactionDate = DateTime.Now,
                    Status = "Completed"
                };

                _context.Transections.Add(transaction);
                _context.SaveChanges(); // needed to get ID

                // 🔹 Create Admin Commission
                var commission = new AdminCommission
                {
                    TransectionId = transaction.TransectionId,
                    Amount = transaction.CommissionAmount,
                    Status = "Paid",
                    PaidDate = DateTime.Now
                };

                _context.AdminCommissions.Add(commission);
                _context.SaveChanges();

                // 📧 SEND EMAIL WITH ATTACHED INVOICE TO BUYER, SELLER, AND ADMIN
                try
                {
                    var pdfBytes = GeneratePremiumInvoice(enquiry);
                    string subject = $"Deal Completed & Invoice Generated - #DEAL-{enquiry.EnquiryId}";

                    // Send to Buyer (SenderUser)
                    if (enquiry.SenderUser != null && !string.IsNullOrEmpty(enquiry.SenderUser.Email))
                    {
                        string buyerBody = $@"
                            <h2>Hello {enquiry.SenderUser.FullName},</h2>
                            <p>Congratulations! Your property deal has been completed successfully.</p>
                            <p><strong>Property:</strong> {enquiry.Property?.Title}</p>
                            <p>Both parties have successfully paid the required commission. Please find your official payment invoice attached to this email.</p>
                            <br/>
                            <p>Thank you,<br/>RealEstate Team</p>";
                        _emailService.SendEmailWithAttachment(enquiry.SenderUser.Email, subject, buyerBody, pdfBytes, $"Invoice_{enquiry.EnquiryId}.pdf");
                    }

                    // Send to Seller/Owner (OwnerUser)
                    if (enquiry.OwnerUser != null && !string.IsNullOrEmpty(enquiry.OwnerUser.Email))
                    {
                        string sellerBody = $@"
                            <h2>Hello {enquiry.OwnerUser.FullName},</h2>
                            <p>Congratulations! Your property deal has been completed successfully.</p>
                            <p><strong>Property:</strong> {enquiry.Property?.Title}</p>
                            <p>Both parties have successfully paid the required commission. Please find your official payment invoice attached to this email.</p>
                            <br/>
                            <p>Thank you,<br/>RealEstate Team</p>";
                        _emailService.SendEmailWithAttachment(enquiry.OwnerUser.Email, subject, sellerBody, pdfBytes, $"Invoice_{enquiry.EnquiryId}.pdf");
                    }

                    // Send to Admin
                    var admin = _context.Users.AsNoTracking().FirstOrDefault(u => u.Role == "Admin");
                    if (admin != null && !string.IsNullOrEmpty(admin.Email))
                    {
                        string adminBody = $@"
                            <h2>Hello Admin,</h2>
                            <p>A new property deal has been completed successfully and commissions have been fully paid!</p>
                            <p><strong>Deal ID:</strong> #{enquiry.EnquiryId}</p>
                            <p><strong>Property:</strong> {enquiry.Property?.Title}</p>
                            <p><strong>Total Price:</strong> ₹{enquiry.PropertyPrice:N2}</p>
                            <p><strong>Commission Earned:</strong> ₹{enquiry.AdminCommission:N2}</p>
                            <br/>
                            <p>Please find the transaction invoice attached.</p>
                            <br/>
                            <p>Thank you,<br/>System Automated Service</p>";
                        _emailService.SendEmailWithAttachment(admin.Email, subject, adminBody, pdfBytes, $"Invoice_{enquiry.EnquiryId}.pdf");
                    }
                }
                catch (System.Exception)
                {
                    // Fail-safe catch block
                }
            }

            _context.SaveChanges();

            return RedirectToAction("Commission", new { id = enquiryId });
        }

        // 🔹 Generates premium invoice via iText7
        private byte[] GeneratePremiumInvoice(Enquiry enquiry)
        {
            using (var stream = new MemoryStream())
            {
                var writer = new PdfWriter(stream);
                var pdf = new PdfDocument(writer);
                var document = new Document(pdf);

                var bold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
                var normal = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

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
                document.Add(new Paragraph("--------------------------------------------------"));

                // ================= STAKEHOLDERS =================
                document.Add(new Paragraph("STAKEHOLDERS").SetFont(bold));
                document.Add(new Paragraph($"Buyer: {enquiry.SenderUser?.FullName}"));
                document.Add(new Paragraph($"Seller: {enquiry.OwnerUser?.FullName}"));
                document.Add(new Paragraph("--------------------------------------------------"));

                // ================= TABLE =================
                document.Add(new Paragraph("PAYMENT SUMMARY").SetFont(bold));

                var table = new Table(2).UseAllAvailableWidth();

                table.AddHeaderCell("Description");
                table.AddHeaderCell("Amount (₹)");

                decimal price = enquiry.PropertyPrice ?? 0;
                decimal token = 15000;
                decimal commission = price * 0.001M/2M;
                decimal total = price + commission;

                table.AddCell("Property Price");
                table.AddCell(price.ToString("N2"));

                table.AddCell("Token Amount (15000)");
                table.AddCell(token.ToString("N2"));

                table.AddCell("Commission (2%)");
                table.AddCell(commission.ToString("N2"));

                // Total Row
                table.AddCell(new Cell().Add(new Paragraph("TOTAL").SetFont(bold)));
                table.AddCell(new Cell().Add(new Paragraph(total.ToString("N2")).SetFont(bold)));

                document.Add(table);

                document.Add(new Paragraph("\n"));

                // ================= STATUS =================
                document.Add(new Paragraph("STATUS: COMPLETED & SOLD")
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
    }
}