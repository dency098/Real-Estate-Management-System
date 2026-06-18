using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateProject.Models;

namespace RealEstateProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ApiPropertyController : ControllerBase
    {
        private readonly RealEstateProjectContext _context;

        public ApiPropertyController(RealEstateProjectContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetProperties(string? type, int? categoryId, string? search)
        {
            var query = _context.Properties
                .Include(p => p.City)
                .Include(p => p.Category)
                .Include(p => p.ProperyImages)
                .Where(p => p.Status == "Approved");

            if (!string.IsNullOrEmpty(type))
                query = query.Where(p => p.PropertyType == type);

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(p => p.Title.Contains(search) || p.Address.Contains(search));

            var data = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
            return Ok(data);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetProperty(int id)
        {
            var property = await _context.Properties
                .Include(p => p.City)
                .Include(p => p.Category)
                .Include(p => p.ProperyImages)
                .Include(p => p.User)
                .Include(p => p.PropertyAmenities).ThenInclude(pa => pa.Amenity)
                .FirstOrDefaultAsync(p => p.ProperyId == id);

            if (property == null)
                return NotFound();

            return Ok(property);
        }

        [Authorize]
        [HttpGet("my")]
        public async Task<IActionResult> GetMyProperties()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                            ?? User.FindFirst("UserId")?.Value;

            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                return Unauthorized();

            var properties = await _context.Properties
                .Include(p => p.City)
                .Include(p => p.Category)
                .Include(p => p.ProperyImages)
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return Ok(properties);
        }

        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            return Ok(await _context.PropertyCategories.ToListAsync());
        }

        [HttpGet("cities")]
        public async Task<IActionResult> GetCities()
        {
            return Ok(await _context.Cities.ToListAsync());
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateProperty([FromBody] PropertyApiModel model)
        {
            if (model == null)
                return BadRequest("Model is null");

            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                            ?? User.FindFirst("UserId")?.Value;

            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                return Unauthorized("User is not authorized.");
            }

            // Find or create State
            var state = await _context.States.FirstOrDefaultAsync(s => s.StateName.ToLower() == model.State.ToLower());
            if (state == null)
            {
                state = new State { StateName = model.State };
                _context.States.Add(state);
                await _context.SaveChangesAsync();
            }

            // Find or create City
            var city = await _context.Cities.FirstOrDefaultAsync(c => c.CityName.ToLower() == model.City.ToLower() && c.StateId == state.StateId);
            if (city == null)
            {
                city = new City { CityName = model.City, StateId = state.StateId };
                _context.Cities.Add(city);
                await _context.SaveChangesAsync();
            }

            // Find or create Category
            var category = await _context.PropertyCategories.FirstOrDefaultAsync(c => c.CategoryName.ToLower() == model.Category.ToLower());
            if (category == null)
            {
                category = new PropertyCategory { CategoryName = model.Category };
                _context.PropertyCategories.Add(category);
                await _context.SaveChangesAsync();
            }

            // Create property
            var property = new Property
            {
                UserId = userId,
                CategoryId = category.CategoryId,
                CityId = city.CityId,
                Title = model.Title,
                Description = model.Description ?? "",
                Price = (decimal)model.Price,
                PropertyType = model.PropertyType,
                AreaSqft = model.AreaSqft,
                Bedroom = model.Bedroom,
                Bathrooms = model.Bathrooms,
                Furnishing = model.Furnishing ?? "Unfurnished",
                Address = model.Address ?? "",
                Pincode = model.Pincode ?? "",
                Status = "Pending", // Requires admin approval before displaying
                CreatedAt = DateTime.Now
            };

            _context.Properties.Add(property);
            await _context.SaveChangesAsync();

            // Add standard default image
            var image = new ProperyImage
            {
                PropertyId = property.ProperyId,
                ImagePath = "/images/014db3dc-9e43-4328-8d3a-e85a228d1a01.jpg"
            };
            _context.ProperyImages.Add(image);

            // Add amenities
            if (model.Amenities != null && model.Amenities.Any())
            {
                foreach (var amenityName in model.Amenities)
                {
                    var amenity = await _context.Amenities.FirstOrDefaultAsync(a => a.AmenityName.ToLower() == amenityName.ToLower());
                    if (amenity == null)
                    {
                        amenity = new Amenity 
                        { 
                            AmenityName = amenityName,
                            Description = "" // Required field
                        };
                        _context.Amenities.Add(amenity);
                        await _context.SaveChangesAsync();
                    }

                    _context.PropertyAmenities.Add(new PropertyAmenity
                    {
                        PropertyId = property.ProperyId,
                        AmenityId = amenity.AmenityId
                    });
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Property published successfully!", propertyId = property.ProperyId });
        }

        [Authorize]
        [HttpPost("{id}/images")]
        public async Task<IActionResult> UploadImages(int id, List<IFormFile> images)
        {
            var property = await _context.Properties.FindAsync(id);
            if (property == null)
                return NotFound("Property not found");

            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                            ?? User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId) || property.UserId != userId)
                return Unauthorized();

            if (images == null || !images.Any())
                return BadRequest("No images provided");

            // Remove default placeholder image
            var defaultImages = await _context.ProperyImages
                .Where(pi => pi.PropertyId == id && pi.ImagePath == "/images/014db3dc-9e43-4328-8d3a-e85a228d1a01.jpg")
                .ToListAsync();
            _context.ProperyImages.RemoveRange(defaultImages);

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            foreach (var file in images)
            {
                if (file.Length > 0)
                {
                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                    var filePath = Path.Combine(uploadsFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    _context.ProperyImages.Add(new ProperyImage
                    {
                        PropertyId = id,
                        ImagePath = "/images/" + fileName
                    });
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Images uploaded successfully" });
        }
    }

    public class PropertyApiModel
    {
        public string Title { get; set; } = null!;
        public string Description { get; set; } = "";
        public double Price { get; set; }
        public string PropertyType { get; set; } = null!;
        public string Category { get; set; } = null!;
        public int AreaSqft { get; set; }
        public int Bedroom { get; set; }
        public int Bathrooms { get; set; }
        public string Furnishing { get; set; } = "Unfurnished";
        public string State { get; set; } = null!;
        public string City { get; set; } = null!;
        public string Pincode { get; set; } = null!;
        public string Address { get; set; } = null!;
        public List<string> Amenities { get; set; } = new();
    }
}
