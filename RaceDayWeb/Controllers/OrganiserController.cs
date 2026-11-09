using Microsoft.AspNetCore.Mvc;
using RaceDayWeb.Models;
using RaceDayWeb.Services;

namespace RaceDayWeb.Controllers
{
    /// <summary>Organiser pages: dashboard and event management (create, edit, delete, banner upload).</summary>
    [SessionAuthorize(Role = Roles.Organiser)]
    public class OrganiserController : AppController
    {
        private static readonly string[] EventTypes = { "Run", "Walk", "Cycle" };

        public OrganiserController(IRaceDayApiClient api) : base(api)
        {
        }

        /// <summary>Summary of all my events, total enrolments per event and upcoming dates.</summary>
        public async Task<IActionResult> Dashboard()
        {
            var result = await Api.GetMyEventsAsync();
            if (result.Unauthorized) return SessionExpired();

            var model = new DashboardViewModel();
            if (!result.Success || result.Data == null)
            {
                FlashError(result.Error ?? "We could not load your dashboard.");
                return View(model);
            }

            var now = DateTime.UtcNow;
            model.Events = result.Data;
            model.TotalEnrolments = result.Data.Sum(e => e.EnrolmentCount);
            model.Upcoming = result.Data.Where(e => e.EventDate >= now).OrderBy(e => e.EventDate).ToList();
            model.NextEvent = model.Upcoming.FirstOrDefault();
            return View(model);
        }

        public async Task<IActionResult> Events()
        {
            var result = await Api.GetMyEventsAsync();
            if (result.Unauthorized) return SessionExpired();

            if (!result.Success || result.Data == null)
            {
                FlashError(result.Error ?? "We could not load your events.");
                return View(new List<OrganiserEvent>());
            }

            return View(result.Data);
        }

        [HttpGet]
        public IActionResult CreateEvent()
        {
            ViewBag.EventTypes = EventTypes;
            ViewData["FormTitle"] = "Create an event";
            return View("EventForm", new EventFormViewModel { EventDate = DateTime.Now.AddDays(30).Date.AddHours(9) });
        }

        [HttpPost]
        public async Task<IActionResult> CreateEvent(EventFormViewModel model)
        {
            ViewBag.EventTypes = EventTypes;
            ViewData["FormTitle"] = "Create an event";

            if (!ModelState.IsValid) return View("EventForm", model);

            var created = await Api.CreateEventAsync(ToRequest(model));
            if (created.Unauthorized) return SessionExpired();

            if (!created.Success || created.Data == null)
            {
                ModelState.AddModelError(string.Empty, created.Error ?? "We could not create the event.");
                return View("EventForm", model);
            }

            if (model.BannerFile is { Length: > 0 })
            {
                var upload = await Api.UploadBannerAsync(created.Data.EventId, model.BannerFile);
                if (!upload.Success)
                {
                    FlashWarning($"The event was created, but the banner could not be uploaded: {upload.Error}");
                    return RedirectToAction(nameof(Events));
                }
            }

            FlashSuccess("Your event has been created.");
            return RedirectToAction(nameof(Events));
        }

        [HttpGet]
        public async Task<IActionResult> EditEvent(int id)
        {
            var result = await Api.GetEventAsync(id);
            if (result.Unauthorized) return SessionExpired();

            if (!result.Success || result.Data == null)
            {
                FlashError(result.Error ?? "We could not load that event.");
                return RedirectToAction(nameof(Events));
            }

            var ev = result.Data;
            ViewBag.EventTypes = EventTypes;
            ViewData["FormTitle"] = "Edit event";
            return View("EventForm", new EventFormViewModel
            {
                Id = ev.EventId,
                Name = ev.Name,
                Description = ev.Description,
                EventDate = ev.EventDate,
                Location = ev.Location,
                Distance = ev.Distance,
                EventType = ev.EventType,
                ExistingBannerUrl = ev.BannerImageUrl
            });
        }

        [HttpPost]
        public async Task<IActionResult> EditEvent(int id, EventFormViewModel model)
        {
            ViewBag.EventTypes = EventTypes;
            ViewData["FormTitle"] = "Edit event";
            model.Id = id;

            if (!ModelState.IsValid) return View("EventForm", model);

            var updated = await Api.UpdateEventAsync(id, ToRequest(model));
            if (updated.Unauthorized) return SessionExpired();

            if (!updated.Success)
            {
                ModelState.AddModelError(string.Empty, updated.Error ?? "We could not save your changes.");
                return View("EventForm", model);
            }

            if (model.BannerFile is { Length: > 0 })
            {
                var upload = await Api.UploadBannerAsync(id, model.BannerFile);
                if (!upload.Success)
                {
                    FlashWarning($"Your changes were saved, but the banner could not be uploaded: {upload.Error}");
                    return RedirectToAction(nameof(Events));
                }
            }

            FlashSuccess("Your event has been updated.");
            return RedirectToAction(nameof(Events));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var result = await Api.DeleteEventAsync(id);
            if (result.Unauthorized) return SessionExpired();

            if (result.Success) FlashSuccess("The event has been deleted.");
            else FlashError(result.Error ?? "We could not delete that event.");

            return RedirectToAction(nameof(Events));
        }

        private static EventRequest ToRequest(EventFormViewModel model) => new(
            model.Name.Trim(),
            model.Description.Trim(),
            model.EventDate!.Value,
            model.Location.Trim(),
            model.Distance!.Value,
            model.EventType);
    }
}
