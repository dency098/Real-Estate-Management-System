using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateProject.Models;
using System.Security.Claims;

namespace RealEstateProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ApiEnquiryController : ControllerBase
    {
        private readonly RealEstateProjectContext _context;

        public ApiEnquiryController(RealEstateProjectContext context)
        {
            _context = context;
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendEnquiry([FromBody] EnquiryRequest request)
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var property = await _context.Properties.FindAsync(request.PropertyId);

            if (property == null) return NotFound("Property not found");
            if (property.UserId == userId) return BadRequest("You cannot enquire on your own property");

            var enquiry = new Enquiry
            {
                PropertyId = request.PropertyId,
                SenderUserId = userId,
                OwnerUserId = property.UserId,
                Message = request.Message,
                CreatedAt = DateTime.Now,
                Status = "Pending",
                Stage = 0,
                PropertyPrice = property.Price
            };

            _context.Enquiries.Add(enquiry);
            
            _context.Notifications.Add(new Notification
            {
                UserId = property.UserId,
                Message = $"New mobile enquiry for {property.Title}",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();
            return Ok(new { message = "Enquiry sent successfully", enquiryId = enquiry.EnquiryId });
        }

        [HttpGet("my")]
        public async Task<IActionResult> GetMyEnquiries()
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var enquiries = await _context.Enquiries
                .Include(e => e.Property)
                .Include(e => e.OwnerUser)
                .Where(e => e.SenderUserId == userId)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

            return Ok(enquiries);
        }

        [HttpGet("owner")]
        public async Task<IActionResult> GetOwnerEnquiries()
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var enquiries = await _context.Enquiries
                .Include(e => e.Property)
                .Include(e => e.SenderUser)
                .Where(e => e.OwnerUserId == userId)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

            return Ok(enquiries);
        }

        [HttpGet("{id}/status")]
        public async Task<IActionResult> GetEnquiryStatus(int id)
        {
            var enquiry = await _context.Enquiries
                .Include(e => e.Property)
                .FirstOrDefaultAsync(e => e.EnquiryId == id);

            if (enquiry == null) return NotFound();

            return Ok(enquiry);
        }
    }

    public class EnquiryRequest
    {
        public int PropertyId { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
