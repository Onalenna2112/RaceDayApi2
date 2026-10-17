using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayApi.Data;
using RaceDayApi.Models;
using RaceDayApi.Services;

namespace RaceDayApi.Controllers
{
    /// <summary>Race results. Organisers capture them; Participants view their own.</summary>
    [Route("api")]
    [ApiController]
    [Authorize]
    [Produces("application/json")]
    public class ResultsController : ControllerBase
    {
        private static readonly string[] TimeFormats = { @"hh\:mm\:ss", @"h\:mm\:ss" };

        private readonly RaceDayDbContext _context;

        public ResultsController(RaceDayDbContext context)
        {
            _context = context;
        }

        /// <summary>Capture (or correct) the finish time and position of an enrolment (Organiser only).</summary>
        /// <remarks>Send finishTime as hh:mm:ss (for example 01:25:30) and the finishing position (1 or more).</remarks>
        /// <response code="201">Result saved.</response>
        /// <response code="400">Invalid finish time or position.</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">You are not an Organiser, or the enrolment is for another Organiser's event.</response>
        /// <response code="404">Enrolment not found.</response>
        [HttpPost("enrolments/{enrolmentId:int}/results")]
        [Authorize(Roles = Roles.Organiser)]
        [ProducesResponseType(typeof(ResultSavedResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CaptureResult(int enrolmentId, [FromBody] ResultDto dto)
        {
            var enrolment = await _context.Enrollments
                .Include(e => e.Event)
                .FirstOrDefaultAsync(e => e.EnrollmentID == enrolmentId);
            if (enrolment == null) return NotFound(new MessageResponse("Enrolment not found."));

            if (enrolment.Event == null || enrolment.Event.OrganiserID != User.GetUserId())
            {
                return StatusCode(StatusCodes.Status403Forbidden, new MessageResponse("You can only capture results for your own events."));
            }

            if (!TimeSpan.TryParseExact(dto.FinishTime, TimeFormats, CultureInfo.InvariantCulture, out var finishTime)
                || finishTime <= TimeSpan.Zero)
            {
                return BadRequest(new MessageResponse("FinishTime must use the format hh:mm:ss, for example 01:25:30."));
            }

            var result = await _context.Results.FirstOrDefaultAsync(r => r.EnrollmentID == enrolmentId);
            if (result == null)
            {
                result = new Result { EnrollmentID = enrolmentId };
                _context.Results.Add(result);
            }

            result.FinishTime = finishTime;
            result.Position = dto.Position;
            result.RecordedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return StatusCode(StatusCodes.Status201Created, new ResultSavedResponse(result.ResultID, "Result saved successfully."));
        }

        /// <summary>View all results of one of your events, ordered by position (Organiser only).</summary>
        /// <response code="200">The results of the event.</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">You are not an Organiser, or the event belongs to another Organiser.</response>
        /// <response code="404">Event not found.</response>
        [HttpGet("events/{eventId:int}/results")]
        [Authorize(Roles = Roles.Organiser)]
        [ProducesResponseType(typeof(List<EventResultResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEventResults(int eventId)
        {
            var ev = await _context.Events.FindAsync(eventId);
            if (ev == null) return NotFound(new MessageResponse("Event not found."));

            if (ev.OrganiserID != User.GetUserId())
            {
                return StatusCode(StatusCodes.Status403Forbidden, new MessageResponse("You can only view results for your own events."));
            }

            var rows = await _context.Results
                .Where(r => r.Enrollment.EventID == eventId)
                .OrderBy(r => r.Position)
                .Select(r => new
                {
                    r.ResultID,
                    r.EnrollmentID,
                    r.Enrollment.ParticipantID,
                    ParticipantName = r.Enrollment.Participant!.FullName,
                    CategoryName = r.Enrollment.Category!.Name,
                    r.FinishTime,
                    r.Position
                })
                .ToListAsync();

            var response = rows
                .Select(r => new EventResultResponse(
                    r.ResultID, r.EnrollmentID, r.ParticipantID, r.ParticipantName, r.CategoryName,
                    r.FinishTime.ToString(@"hh\:mm\:ss"), r.Position))
                .ToList();

            return Ok(response);
        }

        /// <summary>View your own results (Participant only).</summary>
        /// <remarks>The id must be your own participant id.</remarks>
        /// <response code="200">Your results.</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">You are not a Participant, or the id is not yours.</response>
        [HttpGet("participants/{id:int}/results")]
        [Authorize(Roles = Roles.Participant)]
        [ProducesResponseType(typeof(List<MyResultResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetMyResults(int id)
        {
            if (User.GetUserId() != id)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new MessageResponse("You can only view your own results."));
            }

            var rows = await _context.Results
                .Where(r => r.Enrollment.ParticipantID == id)
                .OrderBy(r => r.Enrollment.Event!.EventDate)
                .Select(r => new
                {
                    r.ResultID,
                    r.EnrollmentID,
                    r.Enrollment.EventID,
                    EventName = r.Enrollment.Event!.Name,
                    EventDate = r.Enrollment.Event!.EventDate,
                    r.FinishTime,
                    r.Position
                })
                .ToListAsync();

            var response = rows
                .Select(r => new MyResultResponse(
                    r.ResultID, r.EnrollmentID, r.EventID, r.EventName, r.EventDate,
                    r.FinishTime.ToString(@"hh\:mm\:ss"), r.Position))
                .ToList();

            return Ok(response);
        }
    }

    public class ResultDto
    {
        /// <summary>Finish time as hh:mm:ss, for example 01:25:30.</summary>
        [Required]
        public string FinishTime { get; set; } = string.Empty;

        /// <summary>Finishing position (1 = winner).</summary>
        [Range(1, int.MaxValue)]
        public int Position { get; set; }
    }
}