using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateProject.Models;
using Microsoft.AspNetCore.SignalR;
using RealEstateProject.Hubs;
namespace RealEstateProject.Controllers
{
    public class PropertyController : Controller
    {
        private readonly RealEstateProjectContext _context;
       
        public PropertyController(RealEstateProjectContext context)
        {
            _context = context;
         
        }

        // ================= LIST (ADMIN VIEW) =================
        public IActionResult Index()
        {
            var data = _context.Properties
                .Include(p => p.City)
                .Include(p => p.Category)
                .Include(p => p.User) // 🔥 show user name
                .Include(p => p.ProperyImages) // 🔥 Include images
                .ToList() // Client-side evaluation for custom order logic
                .OrderBy(p => GetStatusOrder(p.Status))
                .ThenByDescending(p => p.CreatedAt)
                .ToList();

            return View(data);
        }

        private int GetStatusOrder(string status)
        {
            switch (status)
            {
                case "Pending": return 1;
                case "EditPending": return 2;
                case "DeleteRequested": return 3;
                case "Approved": return 4;
                case "Sold": return 5;
                case "Rejected": return 6;
                default: return 7;
            }
        }

        // ================= CREATE =================
        public IActionResult Create()
        {
            ViewBag.States = _context.States.ToList();
            ViewBag.Cities = _context.Cities.ToList();
            ViewBag.Categories = _context.PropertyCategories.ToList();
            return View();
        }

        [HttpPost]
        public IActionResult Create(Property model)
        {
            try
            {
                var userId = HttpContext.Session.GetString("UserId");
                var role = HttpContext.Session.GetString("Role");

                if (string.IsNullOrEmpty(userId))
                {
                    return RedirectToAction("Login", "Account");
                }

                model.UserId = Convert.ToInt32(userId);
                model.CreatedAt = DateTime.Now;

                // 🔥 ROLE BASED STATUS
                if (role == "Admin")
                {
                    model.Status = "Approved"; // ✅ direct approve
                }
                else
                {
                    model.Status = "Pending"; // ✅ needs approval
                }

                _context.Properties.Add(model);
                _context.SaveChanges();

                TempData["Success"] = "Property added successfully!";

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.States = _context.States.ToList();
                ViewBag.Cities = _context.Cities.ToList();
                ViewBag.Categories = _context.PropertyCategories.ToList();

                return Content(ex.InnerException?.Message ?? ex.Message);
            }
        }

        // ================= EDIT =================
        public IActionResult Edit(int id)
        {
            ViewBag.Cities = _context.Cities.ToList();
            ViewBag.Categories = _context.PropertyCategories.ToList();

            var property = _context.Properties.FirstOrDefault(x => x.ProperyId == id);

            if (property == null)
                return NotFound();

            return View(property);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Property model, IFormFile ImageFile)
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

            // ================= IMAGE UPDATE =================
            if (ImageFile != null && ImageFile.Length > 0)
            {
                string folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string fileName = Guid.NewGuid() + Path.GetExtension(ImageFile.FileName);
                string filePath = Path.Combine(folder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    ImageFile.CopyTo(stream);
                }

                // ❗ REMOVE OLD IMAGES
                if (property.ProperyImages != null)
                {
                    _context.ProperyImages.RemoveRange(property.ProperyImages);
                }

                // ❗ ADD NEW IMAGE
                property.ProperyImages = new List<ProperyImage>
        {
            new ProperyImage
            {
                ImagePath = "/images/" + fileName
            }
        };
            }

            property.Status = "Approved"; // or "EditPending" if you want approval flow

            _context.SaveChanges();

            return RedirectToAction("Index");
        }

        // ================= DELETE =================

        public IActionResult Delete(int id)
        {
            var property = _context.Properties
                .Include(p => p.ProperyImages) // include dependent images
                .FirstOrDefault(p => p.ProperyId == id);

            if (property != null)
            {
                // delete images first
                _context.ProperyImages.RemoveRange(property.ProperyImages);

                // then delete property
                _context.Properties.Remove(property);

                _context.SaveChanges();
            }

            return RedirectToAction("Index");
        }
        //public IActionResult Delete(int id)
        //{
        //    var data = _context.Properties.Find(id);

        //    if (data != null)
        //    {
        //        _context.Properties.Remove(data);
        //        _context.SaveChanges();
        //    }

        //    return RedirectToAction("Index");
        //}

        // ================= PENDING LIST =================
        public IActionResult Pending()
        {
            var data = _context.Properties
                .Include(p => p.City)
                .Include(p => p.Category)
                .Include(p => p.User) // 🔥 show who added
                .Where(p => p.Status == "Pending")
                .OrderByDescending(p => p.CreatedAt)
                .ToList();

            return View(data);
        }

        // ================= APPROVE =================
        public IActionResult Approve(int id)
        {
            var property = _context.Properties.Find(id);

            if (property != null)
            {
                property.Status = "Approved";
                _context.SaveChanges();

                TempData["Success"] = "Property Approved!";
            }

            return RedirectToAction("Pending");
        }

        // ================= REJECT =================
        public IActionResult Reject(int id)
        {
            var property = _context.Properties.Find(id);

            if (property != null)
            {
                property.Status = "Rejected";
                _context.SaveChanges();

                TempData["Error"] = "Property Rejected!";
            }

            return RedirectToAction("Pending");
        }
        public IActionResult ApproveEdit(int id)
        {
            var property = _context.Properties.FirstOrDefault(x => x.ProperyId == id);

            if (property == null)
                return NotFound();

            // 🔥 THIS IS KEY FIX
            property.Status = "Approved";

            _context.SaveChanges();

            return RedirectToAction("Pending");
        }
        public IActionResult RejectEdit(int id)
        {
            var property = _context.Properties.FirstOrDefault(x => x.ProperyId == id);

            if (property == null)
                return NotFound();

            property.Status = "Rejected";

            _context.SaveChanges();

            return RedirectToAction("Pending");
        }
        public IActionResult DeleteRequests()
        {
            var data = _context.Properties
                .Include(p => p.User)
                .Include(p => p.City)
                .Where(p => p.Status == "DeleteRequested")
                .OrderByDescending(p => p.CreatedAt)
                .ToList();

            return View(data);
        }
        public IActionResult ApproveDelete(int id)
        {
            var property = _context.Properties
                .Include(p => p.ProperyImages)
                .Include(p => p.PropertyAmenities) // 🔥 IMPORTANT FIX
                .FirstOrDefault(p => p.ProperyId == id);

            if (property == null)
                return NotFound();

            try
            {
                // 1. DELETE AMENITIES FIRST (FIX ERROR)
                if (property.PropertyAmenities != null && property.PropertyAmenities.Any())
                {
                    _context.PropertyAmenities.RemoveRange(property.PropertyAmenities);
                }

                // 2. DELETE IMAGES
                if (property.ProperyImages != null && property.ProperyImages.Any())
                {
                    _context.ProperyImages.RemoveRange(property.ProperyImages);
                }

                // 3. DELETE PROPERTY
                _context.Properties.Remove(property);

                _context.SaveChanges();

                TempData["Success"] = "Property deleted successfully.";
            }
            catch (Exception ex)
            {
                return Content(ex.InnerException?.Message ?? ex.Message);
            }

            return RedirectToAction("Index");
        }
        public IActionResult RejectDelete(int id)
        {
            var property = _context.Properties.FirstOrDefault(x => x.ProperyId == id);

            if (property == null)
                return NotFound();

            // restore back to approved
            property.Status = "Approved";

            _context.SaveChanges();

            TempData["Error"] = "Delete request rejected.";

            return RedirectToAction("Index");
        }
        public IActionResult RequestDelete(int id)
        {
            var property = _context.Properties
                .Include(p => p.User)
                .FirstOrDefault(p => p.ProperyId == id);

            if (property == null)
                return NotFound();

            property.Status = "DeleteRequested";
            _context.SaveChanges();

            TempData["ToastTitle"] = "Delete Request";
            TempData["ToastMessage"] = $"{property.User.FullName} wants to delete {property.Title}";

            return RedirectToAction("Index");
        }
        public IActionResult RequestEdit(int id)
        {
            var property = _context.Properties
                .Include(p => p.User)
                .FirstOrDefault(p => p.ProperyId == id);

            if (property == null)
                return NotFound();

            property.Status = "EditPending";
            _context.SaveChanges();

            TempData["ToastTitle"] = "Edit Request";
            TempData["ToastMessage"] = $"{property.User.FullName} wants to edit {property.Title}";

            return RedirectToAction("Index");
        }
    }
}