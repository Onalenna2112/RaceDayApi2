using Microsoft.AspNetCore.Mvc;
using RaceDayWeb.Models;
using RaceDayWeb.Services;

namespace RaceDayWeb.Controllers
{
    /// <summary>All Participants enrolled in one event, with their category and enrolment status.</summary>
    [SessionAuthorize(Role = Roles.Organiser)]
    [Route("Organiser/Events/{eventId:int}/Enrolments")]
    public class OrganiserEnrolmentsController : AppController
    {
        public OrganiserEnrolmentsController(IRaceDayApiClient api) : base(api)
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

            var enrolments = await Api.GetEventEnrolmentsAsync(eventId);
            if (enrolments.Unauthorized) return SessionExpired();

            var model = new EnrolmentsPageViewModel { Event = ev.Data };
            if (enrolments.Success && enrolments.Data != null)
            {
                model.Enrolments = enrolments.Data;
            }
            else
            {
                FlashError(enrolments.Error ?? "We could not load the enrolments.");
            }

            return View(model);
        }

        [HttpPost("Status/{id:int}")]
        public async Task<IActionResult> Status(int eventId, int id, string status)
        {
            var result = await Api.UpdateEnrolmentStatusAsync(id, status);
            if (result.Unauthorized) return SessionExpired();

            if (result.Success) FlashSuccess("The enrolment status has been updated.");
            else FlashError(result.Error ?? "We could not update that status.");

            return RedirectToAction(nameof(Index), new { eventId });
        }
    }
}
