    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using RealEstateProject.Models;
    using System.IdentityModel.Tokens.Jwt;
    using System.Text;
    using System.Text.Json;

    namespace RealEstateProject.Controllers
    {
        public class AccountController : Controller
        {
        private readonly RealEstateProjectContext _context;
        private readonly IConfiguration _configuration;

        public AccountController(RealEstateProjectContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // ================= REGISTER =================

        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (_context.Users.Any(u => u.Email == model.Email))
            {
                ViewBag.Message = "Email already exists.";
                return View(model);
            }

            string fileName = "default.png";

            // ✅ UPLOAD IMAGE HERE
            if (model.ProfileImageFile != null)
            {
                string folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/profile");

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.ProfileImageFile.FileName);
                string path = Path.Combine(folder, fileName);

                using (var stream = new FileStream(path, FileMode.Create))
                {
                    await model.ProfileImageFile.CopyToAsync(stream);
                }
            }

            // default role
            string role = "User";

            // 🔐 Admin secret check
            var adminSecret = _configuration["AdminSettings:SecretKey"];
            if (!string.IsNullOrEmpty(model.AdminSecret) && model.AdminSecret == adminSecret)
            {
                role = "Admin";
            }

            // Create new user in DB directly for instant registration
            User user = new User
            {
                FullName = model.FullName,
                Email = model.Email,
                Phone = model.Phone,
                Password = BCrypt.Net.BCrypt.HashPassword(model.Password),
                Role = role,
                ProfileImage = "/images/profile/" + fileName,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                ViewBag.Message = ex.InnerException?.Message ?? ex.Message;
                return View(model);
            }

            TempData["Success"] = "Registration successful!";
            return RedirectToAction("Login");
        }

        // ================= LOGIN =================

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(LoginModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Directly query DB for ultra-fast, dynamic-port independent, zero-latency login
            var user = _context.Users.FirstOrDefault(u => u.Email == model.Email);

            if (user == null)
            {
                ViewBag.Message = "Invalid email or password";
                return View(model);
            }

            if (!BCrypt.Net.BCrypt.Verify(model.Password, user.Password))
            {
                ViewBag.Message = "Invalid email or password";
                return View(model);
            }


            // ✅ STORE IN SESSION
            HttpContext.Session.SetString("JWToken", ""); // Kept for compatibility
            HttpContext.Session.SetString("Role", user.Role ?? "User");
            HttpContext.Session.SetString("UserId", user.UserId.ToString());
            HttpContext.Session.SetString("FullName", user.FullName ?? "");
            HttpContext.Session.SetString("UserImage", user.ProfileImage ?? "/images/profile/default.png");

            // 🔥 REDIRECT
            if (user.Role == "Admin")
                return RedirectToAction("AdminDashboard");
            else
                return RedirectToAction("Index", "User");
        }

       
        // ================= USER DASHBOARD =================

        public IActionResult Index()
            {
                var role = HttpContext.Session.GetString("Role");

                if (string.IsNullOrEmpty(role) || role == "Admin")
                    return RedirectToAction("Login");
                var data = _context.Properties
                    .Include(p => p.City)
                    .Include(p => p.Category)
                    .Where(p => p.Status == "Approved") // 🔥 MUST ADD
                    .OrderByDescending(p => p.CreatedAt)
                    .ToList();

                return View("~/Views/User/Index.cshtml", data); // ✅ pass data
            }
        
            // ================= ADMIN DASHBOARD =================
            public IActionResult AdminDashboard()
            {
                var role = HttpContext.Session.GetString("Role");
                if (role != "Admin")
                    return RedirectToAction("Login");

                // Dashboard counts
                ViewBag.TotalProperties = _context.Properties.Count();
                ViewBag.PendingProperties = _context.Properties.Count(p => p.Status == "Pending");
                ViewBag.ApprovedProperties = _context.Properties.Count(p => p.Status == "Approved");
                ViewBag.RejectedProperties = _context.Properties.Count(p => p.Status == "Rejected");
                ViewBag.CountNotification = _context.Enquiries.Count();

                // Last 5 properties for quick view
                ViewBag.RecentProperties = _context.Properties
                    .Include(p => p.City)
                    .Include(p => p.Category)
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(5)
                    .ToList();
 
            return View();
            }

            // ================= LOGOUT =================

            public IActionResult Logout()
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login");
            }
        }
    }