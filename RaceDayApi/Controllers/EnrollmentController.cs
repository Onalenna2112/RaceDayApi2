using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayApi.Data;
using RaceDayApi.Models;
using RaceDayApi.Services;

namespace RaceDayApi.Controllers
{
    /// <summary>Event enrolments. Participants enrol; Organisers see who entered their events.</summary>
    [Route("api")]
    [ApiController]
    [Authorize]
    [Produces("application/json")]
    public class EnrollmentController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public EnrollmentController(RaceDayDbContext context)
        {
            _context = context;
        }

        /// <summary>Enrol yourself in an event by choosing a category (Participant only).</summary>
        /// <remarks>
        /// Send the categoryID. You are taken from your login token, so you can only enrol yourself.
        /// The system records the link between you, the event and the category.
        /// </remarks>
        /// <response code="201">Enrolment created.</response>
        /// <response code="400">Event already happened, or the category does not belong to this event.</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">You are not a Participant.</response>
        /// <response code="404">Event not found.</response>
        /// <response code="409">You are already enrolled in this event.</response>
        [HttpPost("events/{id:int}/enrolments")]
        [Authorize(Roles = Roles.Participant)]
        [ProducesResponseType(typeof(EnrolmentCreatedResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> EnrolInEvent(int id, [FromBody] EnrolmentDto dto)
        {
            var participantId = User.GetUserId();
            if (participantId == null) return Unauthorized();

            var ev = await _context.Events.FindAsync(id);
            if (ev == null) return NotFound(new MessageResponse("Event not found."));

            if (ev.EventDate < DateTime.UtcNow)
            {
                return BadRequest(new MessageResponse("Enrolment is closed because the event date has passed."));
            }

            var categoryBelongsToEvent = await _context.Categories
                .AnyAsync(c => c.CategoryID == dto.CategoryID && c.EventID == id);
            if (!categoryBelongsToEvent)
            {
                return BadRequest(new MessageResponse("That category does not exist for this event."));
            }

            if (await _context.Enrollments.AnyAsync(e => e.EventID == id && e.ParticipantID == participantId))
            {
                return Conflict(new MessageResponse("You are already enrolled in this event."));
            }

            var enrolment = new Enrollment
            {
                ParticipantID = participantId.Value,
                EventID = id,
                CategoryID = dto.CategoryID,
                EnrolmentDate = DateTime.UtcNow,
                Status = "Enrolled"
            };

            _context.Enrollments.Add(enrolment);
            await _context.SaveChangesAsync();

            return StatusCode(StatusCodes.Status201Created,
                new EnrolmentCreatedResponse(enrolment.EnrollmentID, "Successfully enrolled in the event."));
        }

        /// <summary>View all enrolments for one of your events (Organiser only).</summary>
        /// <response code="200">The enrolments of the event.</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">You are not an Organiser, or the event belongs to another Organiser.</response>
        /// <response code="404">Event not found.</response>
        [HttpGet("events/{id:int}/enrolments")]
        [Authorize(Roles = Roles.Organiser)]
        [ProducesResponseType(typeof(List<EventEnrolmentResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEventEnrolments(int id)
        {
            var ev = await _context.Events.FindAsync(id);
            if (ev == null) return NotFound(new MessageResponse("Event not found."));

            if (ev.OrganiserID != User.GetUserId())
            {
                return StatusCode(StatusCodes.Status403Forbidden, new MessageResponse("You can only view enrolments for your own events."));
            }

            var enrolments = await _context.Enrollments
                .Where(e => e.EventID == id)
                .OrderBy(e => e.EnrolmentDate)
                .Select(e => new EventEnrolmentResponse(
                    e.EnrollmentID,
                    e.ParticipantID,
                    e.Participant!.FullName,
                    e.Participant!.Email,
                    e.CategoryID,
                    e.Category!.Name,
                    e.Status,
                    e.EnrolmentDate))
                .ToListAsync();

            return Ok(enrolments);
        }

        /// <summary>View your own enrolments (Participant only).</summary>
        /// <remarks>The id must be your own participant id.</remarks>
        /// <response code="200">Your enrolments.</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">You are not a Participant, or the id is not yours.</response>
        [HttpGet("participants/{id:int}/enrolments")]
        [Authorize(Roles = Roles.Participant)]
        [ProducesResponseType(typeof(List<MyEnrolmentResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetMyEnrolments(int id)
        {
            if (User.GetUserId() != id)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new MessageResponse("You can only view your own enrolments."));
            }

            var enrolments = await _context.Enrollments
                .Where(e => e.ParticipantID == id)
                .OrderBy(e => e.Event!.EventDate)
                .Select(e => new MyEnrolmentResponse(
                    e.EnrollmentID,
                    e.EventID,
                    e.Event!.Name,
                    e.Event!.EventDate,
                    e.Event!.Location,
                    e.CategoryID,
                    e.Category!.Name,
                    e.Status,
                    e.EnrolmentDate))
                .ToListAsync();

            return Ok(enrolments);
        }

        /// <summary>Cancel an enrolment.</summary>
        /// <remarks>A Participant can cancel their own enrolment. An Organiser can remove an enrolment from their own event.</remarks>
        /// <response code="200">Enrolment cancelled.</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">This enrolment is not yours, and it is not for one of your events.</response>
        /// <response code="404">Enrolment not found.</response>
        /// <response code="409">A result was already recorded for this enrolment.</response>
        [HttpDelete("enrolments/{id:int}")]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CancelEnrolment(int id)
        {
            var enrolment = await _context.Enrollments
                .Include(e => e.Event)
                .FirstOrDefaultAsync(e => e.EnrollmentID == id);
            if (enrolment == null) return NotFound(new MessageResponse("Enrolment not found."));

            var userId = User.GetUserId();
            var role = User.GetRole();

            var allowed = (role == Roles.Participant && enrolment.ParticipantID == userId)
                       || (role == Roles.Organiser && enrolment.Event != null && enrolment.Event.OrganiserID == userId);
            if (!allowed)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new MessageResponse("You are not allowed to cancel this enrolment."));
            }

            if (await _context.Results.AnyAsync(r => r.EnrollmentID == id))
            {
                return Conflict(new MessageResponse("A result was already recorded, so this enrolment cannot be cancelled."));
            }

            _context.Enrollments.Remove(enrolment);
            await _context.SaveChangesAsync();
            return Ok(new MessageResponse("Enrolment cancelled."));
        }
    }
}
