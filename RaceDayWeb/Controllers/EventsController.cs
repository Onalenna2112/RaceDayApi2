using Microsoft.AspNetCore.Mvc;
using RaceDayWeb.Models;
using RaceDayWeb.Services;

namespace RaceDayWeb.Controllers
{
    /// <summary>The Participant's view of events: browse, filter, see details and enrol.</summary>
    [SessionAuthorize]
    public class EventsController : AppController
    {
        public EventsController(IRaceDayApiClient api) : base(api)
        {
        }

        /// <summary>Participant home page: upcoming events with search, type and sorting options.</summary>
        [SessionAuthorize(Role = Roles.Participant)]
        public async Task<IActionResult> Index(EventFilterViewModel filter)
        {
            var result = await Api.GetEventsAsync(!filter.IncludePast, filter.EventType, filter.Search);
            if (result.Unauthorized) return SessionExpired();

            if (!result.Success || result.Data == null)
            {
                FlashError(result.Error ?? "We could not load the events.");
                filter.Events = new List<EventSummary>();
                return View(filter);
            }

            filter.Events = filter.SortBy == "distance"
                ? result.Data.OrderBy(e => e.Distance).ToList()
                : result.Data.OrderBy(e => e.EventDate).ToList();

            return View(filter);
        }

        /// <summary>Full event information, the banner and the available categories.</summary>
        public async Task<IActionResult> Details(int id)
        {
            var result = await Api.GetEventAsync(id);
            if (result.Unauthorized) return SessionExpired();

            if (!result.Success || result.Data == null)
            {
                FlashError(result.Error ?? "We could not load that event.");
                return RedirectToRoleHome();
            }

            var user = CurrentUser!;
            var model = new EventDetailsViewModel
            {
                Event = result.Data,
                IsParticipant = user.Role == Roles.Participant,
                EventIsPast = result.Data.EventDate < DateTime.UtcNow
            };

            if (model.IsParticipant)
            {
                var mine = await Api.GetMyEnrolmentsAsync(user.UserId);
                if (mine.Success && mine.Data != null)
                {
                    model.MyEnrolment = mine.Data.FirstOrDefault(e => e.EventId == id);
                }
            }

            return View(model);
        }

        /// <summary>Enrol the logged-in Participant in an event with the chosen category.</summary>
        [HttpPost]
        [SessionAuthorize(Role = Roles.Participant)]
        public async Task<IActionResult> Enrol(int eventId, int categoryId)
        {
            if (categoryId <= 0)
            {
                FlashError("Please choose a category before you enter the event.");
                return RedirectToAction(nameof(Details), new { id = eventId });
            }

            var result = await Api.EnrolAsync(eventId, categoryId);
            if (result.Unauthorized) return SessionExpired();

            if (!result.Success)
            {
                FlashError(result.Error ?? "We could not enrol you. Please try again.");
                return RedirectToAction(nameof(Details), new { id = eventId });
            }

            FlashSuccess("You are in! Your enrolment has been recorded.");
            return RedirectToAction("MyEvents", "Participant");
        }
    }
}
