using Microsoft.AspNetCore.Mvc;
using RealEstateProject.Models;

namespace RealEstateProject.Controllers
{
    public class CategoryController : Controller
    {
        private readonly RealEstateProjectContext _context;

        public CategoryController(RealEstateProjectContext context)
        {
            _context = context;
        }

        // ✅ CHECK ADMIN SESSION
        private bool IsAdmin()
        {
            return HttpContext.Session.GetString("Role") == "Admin";
        }

        // ================= LIST =================
        public IActionResult Index()
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            var data = _context.PropertyCategories.ToList();
            return View(data);
        }

        // ================= CREATE =================
        public IActionResult Create()
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            return View();
        }

        [HttpPost]
        public IActionResult Create(PropertyCategory model)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            if (string.IsNullOrWhiteSpace(model.CategoryName))
            {
                ModelState.AddModelError("CategoryName", "Category name is required.");
            }
            else
            {
                model.CategoryName = model.CategoryName.Trim();
                if (model.CategoryName.Length < 3)
                {
                    ModelState.AddModelError("CategoryName", "Category name must be at least 3 characters long.");
                }
                else if (model.CategoryName.Length > 100)
                {
                    ModelState.AddModelError("CategoryName", "Category name cannot exceed 100 characters.");
                }
                else if (_context.PropertyCategories.Any(c => c.CategoryName.ToLower() == model.CategoryName.ToLower()))
                {
                    ModelState.AddModelError("CategoryName", "Category name already exists.");
                }
            }

            if (ModelState.IsValid)
            {
                _context.PropertyCategories.Add(model);
                _context.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(model);
        }

        // ================= EDIT =================
        public IActionResult Edit(int id)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            var data = _context.PropertyCategories.Find(id);

            if (data == null)
                return NotFound();

            return View(data);
        }

        [HttpPost]
        public IActionResult Edit(PropertyCategory model)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            if (string.IsNullOrWhiteSpace(model.CategoryName))
            {
                ModelState.AddModelError("CategoryName", "Category name is required.");
            }
            else
            {
                model.CategoryName = model.CategoryName.Trim();
                if (model.CategoryName.Length < 3)
                {
                    ModelState.AddModelError("CategoryName", "Category name must be at least 3 characters long.");
                }
                else if (model.CategoryName.Length > 100)
                {
                    ModelState.AddModelError("CategoryName", "Category name cannot exceed 100 characters.");
                }
                else if (_context.PropertyCategories.Any(c => c.CategoryName.ToLower() == model.CategoryName.ToLower() && c.CategoryId != model.CategoryId))
                {
                    ModelState.AddModelError("CategoryName", "Category name already exists.");
                }
            }

            if (ModelState.IsValid)
            {
                _context.PropertyCategories.Update(model);
                _context.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(model);
        }

        // ================= DELETE =================
        public IActionResult Delete(int id)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            var data = _context.PropertyCategories.Find(id);

            if (data == null)
                return NotFound();

            _context.PropertyCategories.Remove(data);
            _context.SaveChanges();

            return RedirectToAction("Index");
        }
    }
}