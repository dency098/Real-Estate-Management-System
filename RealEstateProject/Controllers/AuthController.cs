using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using RealEstateProject.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace RealEstateProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly RealEstateProjectContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(RealEstateProjectContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // ================= REGISTER =================
        [HttpPost("register")]

        public IActionResult Register([FromBody] RegisterModel model)
        {
            if (model == null)
                return BadRequest("Model is null");

            if (_context.Users.Any(u => u.Email == model.Email))
                return BadRequest("Email already exists.");

            // default role
            string role = "User";

            // 🔐 Admin secret check
            var adminSecret = _configuration["AdminSettings:SecretKey"];
            if (!string.IsNullOrEmpty(model.AdminSecret) && model.AdminSecret == adminSecret)
            {
                role = "Admin";
            }
            // ✅ IMAGE UPLOAD
            string fileName = model.ProfileImage ?? "default.png";
            // create new user
            User user = new User
            {
                FullName = model.FullName,
                Email = model.Email,
                Phone = model.Phone,
                Password = BCrypt.Net.BCrypt.HashPassword(model.Password),
                Role = role,
                ProfileImage = "/images/profile/" + fileName,
                CreatedAt = DateTime.UtcNow  // ✅ important to prevent NULL insert
            };

            try
            {
                _context.Users.Add(user);
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.InnerException?.Message ?? ex.Message);
            }

            return Ok(new
            {
                message = "User registered successfully",
                role = role
            });
        }

        // ================= LOGIN =================
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginModel model)
        {
            if (model == null)
                return BadRequest("Invalid data.");

            var user = _context.Users.FirstOrDefault(u => u.Email == model.Email);

            if (user == null)
                return Unauthorized("User not found");

            if (!BCrypt.Net.BCrypt.Verify(model.Password, user.Password))
                return Unauthorized("Wrong password");

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("FullName", user.FullName),
                new Claim("UserId", user.UserId.ToString())
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(
                    Convert.ToDouble(_configuration["Jwt:DurationInMinutes"])
                ),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature
                ),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"]
            };

            SecurityToken token;

            try
            {
                token = tokenHandler.CreateToken(tokenDescriptor);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Token creation failed: {ex.Message}");
            }

            return Ok(new
            {
                token = tokenHandler.WriteToken(token),
                expiration = token.ValidTo,
                role = user.Role
            });
        }

        // ================= ROLE-BASED APIs =================
        [Authorize(Roles = "Admin")]
        [HttpGet("admin-data")]
        public IActionResult AdminData()
        {
            return Ok("Admin only data");
        }

        [Authorize(Roles = "User")]
        [HttpGet("user-data")]
        public IActionResult UserData()
        {
            return Ok("User data (Buyer/Seller combined)");
        }
    }
}