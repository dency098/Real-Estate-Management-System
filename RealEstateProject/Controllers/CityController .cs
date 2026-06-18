using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateProject.Models;

namespace RealEstateProject.Controllers
{
    public class CityController : Controller
    {
        private readonly RealEstateProjectContext _context;

        public CityController(RealEstateProjectContext context)
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

            var data = _context.Cities
      .Include(c => c.State)
      .ToList();
            return View(data);
        }

        // CREATE
        public IActionResult Create()
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            ViewBag.States = _context.States.ToList();
            return View();
        }

        [HttpPost]
        public IActionResult Create(City model)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            if (model.StateId <= 0)
            {
                ModelState.AddModelError("StateId", "Please select a valid State.");
            }

            if (string.IsNullOrWhiteSpace(model.CityName))
            {
                ModelState.AddModelError("CityName", "City name is required.");
            }
            else
            {
                model.CityName = model.CityName.Trim();
                if (model.CityName.Length < 3)
                {
                    ModelState.AddModelError("CityName", "City name must be at least 3 characters long.");
                }
                else if (model.CityName.Length > 100)
                {
                    ModelState.AddModelError("CityName", "City name cannot exceed 100 characters.");
                }
                else if (model.StateId > 0 && _context.Cities.Any(c => c.CityName.ToLower() == model.CityName.ToLower() && c.StateId == model.StateId))
                {
                    ModelState.AddModelError("CityName", $"City '{model.CityName}' already exists in the selected state.");
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Cities.Add(model);
                    _context.SaveChanges();
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, ex.InnerException?.Message ?? ex.Message);
                }
            }

            ViewBag.States = _context.States.ToList();
            return View(model);
        }

        // EDIT
        public IActionResult Edit(int id)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            ViewBag.States = _context.States.ToList();
            var data = _context.Cities.Find(id);
            return View(data);
        }

        [HttpPost]
        public IActionResult Edit(City model)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            if (model.StateId <= 0)
            {
                ModelState.AddModelError("StateId", "Please select a valid State.");
            }

            if (string.IsNullOrWhiteSpace(model.CityName))
            {
                ModelState.AddModelError("CityName", "City name is required.");
            }
            else
            {
                model.CityName = model.CityName.Trim();
                if (model.CityName.Length < 3)
                {
                    ModelState.AddModelError("CityName", "City name must be at least 3 characters long.");
                }
                else if (model.CityName.Length > 100)
                {
                    ModelState.AddModelError("CityName", "City name cannot exceed 100 characters.");
                }
                else if (model.StateId > 0 && _context.Cities.Any(c => c.CityName.ToLower() == model.CityName.ToLower() && c.StateId == model.StateId && c.CityId != model.CityId))
                {
                    ModelState.AddModelError("CityName", $"City '{model.CityName}' already exists in the selected state.");
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Cities.Update(model);
                    _context.SaveChanges();
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, ex.InnerException?.Message ?? ex.Message);
                }
            }

            ViewBag.States = _context.States.ToList();
            return View(model);
        }

        // DELETE
        public IActionResult Delete(int id)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            var data = _context.Cities.Find(id);
            _context.Cities.Remove(data);
            _context.SaveChanges();
            return RedirectToAction("Index");
        }

         //🔥 AJAX METHOD
        public JsonResult GetCitiesByState(int stateId)
        {
            var cities = _context.Cities
                .Where(c => c.StateId == stateId)
                .ToList();

            return Json(cities);
        }
    }
}
