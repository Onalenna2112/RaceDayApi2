using Microsoft.AspNetCore.Mvc;
using RaceDayWeb.Models;
using RaceDayWeb.Services;

namespace RaceDayWeb.Controllers
{
    /// <summary>Category management for one event: add, edit and delete age or distance categories.</summary>
    [SessionAuthorize(Role = Roles.Organiser)]
    [Route("Organiser/Events/{eventId:int}/Categories")]
    public class OrganiserCategoriesController : AppController
    {
        public OrganiserCategoriesController(IRaceDayApiClient api) : base(api)
        {
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(int eventId)
        {
            var ev = await Api.GetEventAsync(eventId);
            if (ev.Unauthorized) return SessionExpired();

            if (!ev.Success || ev.Data == null)
            {
                FlashError(ev.Error ?? "We could not load that event.");
                return RedirectToAction("Events", "Organiser");
            }

            return View(new CategoriesPageViewModel { Event = ev.Data });
        }

        [HttpPost("Add")]
        public async Task<IActionResult> Add(int eventId, CategoryFormViewModel newCategory)
        {
            if (newCategory.MaxAge < newCategory.MinAge)
            {
                ModelState.AddModelError("newCategory.MaxAge", "Maximum age cannot be smaller than minimum age.");
            }

            if (!ModelState.IsValid)
            {
                var ev = await Api.GetEventAsync(eventId);
                if (ev.Unauthorized) return SessionExpired();
                if (!ev.Success || ev.Data == null) return RedirectToAction("Events", "Organiser");
                return View("Index", new CategoriesPageViewModel { Event = ev.Data, NewCategory = newCategory });
            }

            var result = await Api.CreateCategoryAsync(eventId, ToRequest(newCategory));
            if (result.Unauthorized) return SessionExpired();

            if (result.Success) FlashSuccess("The category has been added.");
            else FlashError(result.Error ?? "We could not add that category.");

            return RedirectToAction(nameof(Index), new { eventId });
        }

        [HttpGet("Edit/{id:int}")]
        public async Task<IActionResult> Edit(int eventId, int id)
        {
            var ev = await Api.GetEventAsync(eventId);
            if (ev.Unauthorized) return SessionExpired();

            var category = ev.Data?.Categories.FirstOrDefault(c => c.CategoryId == id);
            if (!ev.Success || ev.Data == null || category == null)
            {
                FlashError(ev.Error ?? "We could not find that category.");
                return RedirectToAction(nameof(Index), new { eventId });
            }

            ViewData["EventName"] = ev.Data.Name;
            return View(new CategoryFormViewModel
            {
                Id = category.CategoryId,
                Name = category.Name,
                MinAge = category.MinAge,
                MaxAge = category.MaxAge,
                Distance = category.Distance,
                EntryFee = category.EntryFee
            });
        }

        [HttpPost("Edit/{id:int}")]
        public async Task<IActionResult> Edit(int eventId, int id, CategoryFormViewModel form)
        {
            form.Id = id;

            if (form.MaxAge < form.MinAge)
            {
                ModelState.AddModelError(nameof(form.MaxAge), "Maximum age cannot be smaller than minimum age.");
            }

            if (!ModelState.IsValid) return View(form);

            var result = await Api.UpdateCategoryAsync(id, ToRequest(form));
            if (result.Unauthorized) return SessionExpired();

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Error ?? "We could not save your changes.");
                return View(form);
            }

            FlashSuccess("The category has been updated.");
            return RedirectToAction(nameof(Index), new { eventId });
        }

        [HttpPost("Delete/{id:int}")]
        public async Task<IActionResult> Delete(int eventId, int id)
        {
            var result = await Api.DeleteCategoryAsync(id);
            if (result.Unauthorized) return SessionExpired();

            if (result.Success) FlashSuccess("The category has been deleted.");
            else FlashError(result.Error ?? "We could not delete that category.");

            return RedirectToAction(nameof(Index), new { eventId });
        }

        private static CategoryRequest ToRequest(CategoryFormViewModel form) =>
            new(form.Name.Trim(), form.MinAge, form.MaxAge, form.Distance, form.EntryFee);
    }
}
