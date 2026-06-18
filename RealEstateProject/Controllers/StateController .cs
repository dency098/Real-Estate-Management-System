using Microsoft.AspNetCore.Mvc;
using RealEstateProject.Models;

namespace RealEstateProject.Controllers
{
    public class StateController : Controller
    {
        private readonly RealEstateProjectContext _context;

        public StateController(RealEstateProjectContext context)
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

            var data = _context.States.ToList();
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
        public IActionResult Create(State model)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            if (string.IsNullOrWhiteSpace(model.StateName))
            {
                ModelState.AddModelError("StateName", "State name is required.");
            }
            else
            {
                model.StateName = model.StateName.Trim();
                if (model.StateName.Length < 3)
                {
                    ModelState.AddModelError("StateName", "State name must be at least 3 characters long.");
                }
                else if (model.StateName.Length > 100)
                {
                    ModelState.AddModelError("StateName", "State name cannot exceed 100 characters.");
                }
                else if (_context.States.Any(s => s.StateName.ToLower() == model.StateName.ToLower()))
                {
                    ModelState.AddModelError("StateName", "State name already exists.");
                }
            }

            if (ModelState.IsValid)
            {
                _context.States.Add(model);
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

            var data = _context.States.Find(id);
            return View(data);
        }

        [HttpPost]
        public IActionResult Edit(State model)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            if (string.IsNullOrWhiteSpace(model.StateName))
            {
                ModelState.AddModelError("StateName", "State name is required.");
            }
            else
            {
                model.StateName = model.StateName.Trim();
                if (model.StateName.Length < 3)
                {
                    ModelState.AddModelError("StateName", "State name must be at least 3 characters long.");
                }
                else if (model.StateName.Length > 100)
                {
                    ModelState.AddModelError("StateName", "State name cannot exceed 100 characters.");
                }
                else if (_context.States.Any(s => s.StateName.ToLower() == model.StateName.ToLower() && s.StateId != model.StateId))
                {
                    ModelState.AddModelError("StateName", "State name already exists.");
                }
            }

            if (ModelState.IsValid)
            {
                _context.States.Update(model);
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

            var data = _context.States.Find(id);
            _context.States.Remove(data);
            _context.SaveChanges();
            return RedirectToAction("Index");
        }
    }
}
