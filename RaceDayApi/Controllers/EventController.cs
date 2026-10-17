using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayApi.Data;
using RaceDayApi.Models;
using RaceDayApi.Services;

namespace RaceDayApi.Controllers
{
    /// <summary>View events (both roles). Create, update and delete events (Organisers only).</summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [Produces("application/json")]
    public class EventsController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public EventsController(RaceDayDbContext context)
        {
            _context = context;
        }

        /// <summary>List all events.</summary>
        /// <remarks>Both roles can use this. Add ?upcomingOnly=true to hide events that already happened.</remarks>
        /// <response code="200">The list of events, ordered by date.</response>
        /// <response code="401">Not logged in.</response>
        [HttpGet]
        [ProducesResponseType(typeof(List<EventSummaryResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetEvents([FromQuery] bool upcomingOnly = false)
        {
            var query = _context.Events.AsQueryable();

            if (upcomingOnly)
            {
                var now = DateTime.UtcNow;
                query = query.Where(e => e.EventDate >= now);
            }

            var events = await query
                .OrderBy(e => e.EventDate)
                .Select(e => new EventSummaryResponse(
                    e.EventID, e.Name, e.Description, e.EventDate, e.Location, e.Distance, e.EventType,
                    e.Organiser!.FullName))
                .ToListAsync();

            return Ok(events);
        }

        /// <summary>Get one event, including its categories.</summary>
        /// <response code="200">The event and its categories.</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="404">Event not found.</response>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(EventDetailResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEvent(int id)
        {
            var ev = await _context.Events
                .Where(e => e.EventID == id)
                .Select(e => new EventDetailResponse(
                    e.EventID, e.Name, e.Description, e.EventDate, e.Location, e.Distance, e.EventType,
                    e.Organiser!.FullName,
                    e.Categories
                        .Select(c => new CategoryResponse(c.CategoryID, c.Name, c.MinAge, c.MaxAge, c.Distance, c.EntryFee))
                        .ToList()))
                .FirstOrDefaultAsync();

            if (ev == null) return NotFound(new MessageResponse("Event not found."));

            return Ok(ev);
        }

        /// <summary>Create an event (Organiser only).</summary>
        /// <remarks>
        /// Send name, description, eventDate (must be in the future), location, distance (km) and
        /// eventType ("Run", "Walk" or "Cycle"). The event belongs to the logged-in Organiser.
        /// </remarks>
        /// <response code="201">Event created.</response>
        /// <response code="400">Invalid data.</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">You are not an Organiser.</response>
        [HttpPost]
        [Authorize(Roles = Roles.Organiser)]
        [ProducesResponseType(typeof(EventCreatedResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateEvent([FromBody] EventDto dto)
        {
            var organiserId = User.GetUserId();
            if (organiserId == null) return Unauthorized();

            if (dto.EventDate <= DateTime.UtcNow)
            {
                return BadRequest(new MessageResponse("The event date must be in the future."));
            }

            if (!await _context.Organisers.AnyAsync(o => o.OrganiserID == organiserId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new MessageResponse("Organiser account not found."));
            }

            var ev = new Event
            {
                OrganiserID = organiserId.Value,
                Name = dto.Name.Trim(),
                Description = dto.Description,
                EventDate = dto.EventDate,
                Location = dto.Location.Trim(),
                Distance = dto.Distance,
                EventType = dto.EventType.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Events.Add(ev);
            await _context.SaveChangesAsync();

            return StatusCode(StatusCodes.Status201Created, new EventCreatedResponse(ev.EventID, "Event created successfully."));
        }

        /// <summary>Update one of your own events (Organiser only).</summary>
        /// <response code="200">Event updated.</response>
        /// <response code="400">Invalid data.</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">You are not an Organiser, or the event belongs to another Organiser.</response>
        /// <response code="404">Event not found.</response>
        [HttpPut("{id:int}")]
        [Authorize(Roles = Roles.Organiser)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateEvent(int id, [FromBody] EventDto dto)
        {
            var ev = await _context.Events.FindAsync(id);
            if (ev == null) return NotFound(new MessageResponse("Event not found."));

            if (ev.OrganiserID != User.GetUserId())
            {
                return StatusCode(StatusCodes.Status403Forbidden, new MessageResponse("You can only change your own events."));
            }

            if (dto.EventDate == default)
            {
                return BadRequest(new MessageResponse("A valid event date is required."));
            }

            ev.Name = dto.Name.Trim();
            ev.Description = dto.Description;
            ev.EventDate = dto.EventDate;
            ev.Location = dto.Location.Trim();
            ev.Distance = dto.Distance;
            ev.EventType = dto.EventType.Trim();

            await _context.SaveChangesAsync();
            return Ok(new MessageResponse("Event updated successfully."));
        }

        /// <summary>Delete one of your own events (Organiser only).</summary>
        /// <remarks>An event that already has enrolments cannot be deleted.</remarks>
        /// <response code="200">Event deleted.</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">You are not an Organiser, or the event belongs to another Organiser.</response>
        /// <response code="404">Event not found.</response>
        /// <response code="409">The event has enrolments.</response>
        [HttpDelete("{id:int}")]
        [Authorize(Roles = Roles.Organiser)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var ev = await _context.Events.FindAsync(id);
            if (ev == null) return NotFound(new MessageResponse("Event not found."));

            if (ev.OrganiserID != User.GetUserId())
            {
                return StatusCode(StatusCodes.Status403Forbidden, new MessageResponse("You can only delete your own events."));
            }

            if (await _context.Enrollments.AnyAsync(e => e.EventID == id))
            {
                return Conflict(new MessageResponse("This event already has enrolments and cannot be deleted."));
            }

            _context.Events.Remove(ev);
            await _context.SaveChangesAsync();
            return Ok(new MessageResponse("Event deleted successfully."));
        }
    }

    public class EventDto
    {
        /// <summary>Name of the event.</summary>
        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Short description.</summary>
        [Required]
        public string Description { get; set; } = string.Empty;

        /// <summary>Date and time of the event.</summary>
        public DateTime EventDate { get; set; }

        /// <summary>Where the event takes place.</summary>
        [Required]
        [MaxLength(200)]
        public string Location { get; set; } = string.Empty;

        /// <summary>Distance in kilometres.</summary>
        [Range(0.1, 1000.0)]
        public decimal Distance { get; set; }

        /// <summary>"Run", "Walk" or "Cycle".</summary>
        [Required]
        [RegularExpression("(?i)^(run|walk|cycle)$", ErrorMessage = "EventType must be Run, Walk or Cycle.")]
        public string EventType { get; set; } = string.Empty;
    }
}
