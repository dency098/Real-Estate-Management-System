using Microsoft.AspNetCore.Mvc;
using RealEstateProject.Models;

namespace RealEstateProject.Controllers
{
    public class AmenityController : Controller
    {
        private readonly RealEstateProjectContext _context;

        public AmenityController(RealEstateProjectContext context)
        {
            _context = context;
        }

        private bool IsAdmin()
        {
            return HttpContext.Session.GetString("Role") == "Admin";
        }

        // LIST
        public IActionResult Index()
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            var data = _context.Amenities.ToList();
            return View(data);
        }

        // CREATE
        public IActionResult Create()
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            return View();
        }

        [HttpPost]
        public IActionResult Create(Amenity model)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            if (string.IsNullOrWhiteSpace(model.AmenityName))
            {
                ModelState.AddModelError("AmenityName", "Amenity name is required.");
            }
            else
            {
                model.AmenityName = model.AmenityName.Trim();
                if (model.AmenityName.Length < 3)
                {
                    ModelState.AddModelError("AmenityName", "Amenity name must be at least 3 characters long.");
                }
                else if (model.AmenityName.Length > 100)
                {
                    ModelState.AddModelError("AmenityName", "Amenity name cannot exceed 100 characters.");
                }
                else if (_context.Amenities.Any(a => a.AmenityName.ToLower() == model.AmenityName.ToLower()))
                {
                    ModelState.AddModelError("AmenityName", "Amenity name already exists.");
                }
            }

            if (string.IsNullOrWhiteSpace(model.Description))
            {
                ModelState.AddModelError("Description", "Description is required.");
            }
            else
            {
                model.Description = model.Description.Trim();
                if (model.Description.Length < 5)
                {
                    ModelState.AddModelError("Description", "Description must be at least 5 characters long.");
                }
                else if (model.Description.Length > 500)
                {
                    ModelState.AddModelError("Description", "Description cannot exceed 500 characters.");
                }
            }

            if (ModelState.IsValid)
            {
                _context.Amenities.Add(model);
                _context.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(model);
        }

        // EDIT
        public IActionResult Edit(int id)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            var data = _context.Amenities.Find(id);
            return View(data);
        }

        [HttpPost]
        public IActionResult Edit(Amenity model)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            if (string.IsNullOrWhiteSpace(model.AmenityName))
            {
                ModelState.AddModelError("AmenityName", "Amenity name is required.");
            }
            else
            {
                model.AmenityName = model.AmenityName.Trim();
                if (model.AmenityName.Length < 3)
                {
                    ModelState.AddModelError("AmenityName", "Amenity name must be at least 3 characters long.");
                }
                else if (model.AmenityName.Length > 100)
                {
                    ModelState.AddModelError("AmenityName", "Amenity name cannot exceed 100 characters.");
                }
                else if (_context.Amenities.Any(a => a.AmenityName.ToLower() == model.AmenityName.ToLower() && a.AmenityId != model.AmenityId))
                {
                    ModelState.AddModelError("AmenityName", "Amenity name already exists.");
                }
            }

            if (string.IsNullOrWhiteSpace(model.Description))
            {
                ModelState.AddModelError("Description", "Description is required.");
            }
            else
            {
                model.Description = model.Description.Trim();
                if (model.Description.Length < 5)
                {
                    ModelState.AddModelError("Description", "Description must be at least 5 characters long.");
                }
                else if (model.Description.Length > 500)
                {
                    ModelState.AddModelError("Description", "Description cannot exceed 500 characters.");
                }
            }

            if (ModelState.IsValid)
            {
                _context.Amenities.Update(model);
                _context.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(model);
        }

        // DELETE
        public IActionResult Delete(int id)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            var data = _context.Amenities.Find(id);

            if (data != null)
            {
                _context.Amenities.Remove(data);
                _context.SaveChanges();
            }

            return RedirectToAction("Index");
        }
    }
}