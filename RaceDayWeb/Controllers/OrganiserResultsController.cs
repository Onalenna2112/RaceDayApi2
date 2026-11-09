using Microsoft.AspNetCore.Mvc;
using RaceDayWeb.Models;
using RaceDayWeb.Services;

namespace RaceDayWeb.Controllers
{
    /// <summary>Results entry for one event: capture and publish finish times and finishing positions.</summary>
    [SessionAuthorize(Role = Roles.Organiser)]
    [Route("Organiser/Events/{eventId:int}/Results")]
    public class OrganiserResultsController : AppController
    {
        public OrganiserResultsController(IRaceDayApiClient api) : base(api)
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

            var results = await Api.GetEventResultsAsync(eventId);
            if (results.Unauthorized) return SessionExpired();

            var model = new ResultsPageViewModel { Event = ev.Data };

            if (!enrolments.Success || enrolments.Data == null)
            {
                FlashError(enrolments.Error ?? "We could not load the enrolments.");
                return View(model);
            }

            var resultsByEnrolment = (results.Success && results.Data != null)
                ? results.Data.ToDictionary(r => r.EnrolmentId)
                : new Dictionary<int, EventResult>();

            model.Rows = enrolments.Data
                .Where(e => !string.Equals(e.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                .Select(e => new ResultRow
                {
                    Enrolment = e,
                    Result = resultsByEnrolment.TryGetValue(e.EnrolmentId, out var r) ? r : null
                })
                .OrderBy(row => row.Result == null ? int.MaxValue : row.Result.Position)
                .ThenBy(row => row.Enrolment.ParticipantName)
                .ToList();

            return View(model);
        }

        [HttpPost("Save/{enrolmentId:int}")]
        public async Task<IActionResult> Save(int eventId, int enrolmentId, string? finishTime, int position)
        {
            if (string.IsNullOrWhiteSpace(finishTime) || position <= 0)
            {
                FlashError("Please enter a finish time (hh:mm:ss) and a finishing position of 1 or more.");
                return RedirectToAction(nameof(Index), new { eventId });
            }

            var result = await Api.CaptureResultAsync(enrolmentId, finishTime.Trim(), position);
            if (result.Unauthorized) return SessionExpired();

            if (result.Success) FlashSuccess("The result has been saved and published.");
            else FlashError(result.Error ?? "We could not save that result.");

            return RedirectToAction(nameof(Index), new { eventId });
        }
    }
}
