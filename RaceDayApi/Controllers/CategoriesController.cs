using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayApi.Data;
using RaceDayApi.Models;
using RaceDayApi.Services;

namespace RaceDayApi.Controllers
{
    /// <summary>Age or distance categories for an event. Both roles can view; only the event's Organiser can change them.</summary>
    [Route("api")]
    [ApiController]
    [Authorize]
    [Produces("application/json")]
    public class CategoriesController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public CategoriesController(RaceDayDbContext context)
        {
            _context = context;
        }

        /// <summary>List the categories of an event.</summary>
        /// <response code="200">The categories of the event.</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="404">Event not found.</response>
        [HttpGet("events/{eventId:int}/categories")]
        [ProducesResponseType(typeof(List<CategoryResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCategories(int eventId)
        {
            if (!await _context.Events.AnyAsync(e => e.EventID == eventId))
            {
                return NotFound(new MessageResponse("Event not found."));
            }

            var categories = await _context.Categories
                .Where(c => c.EventID == eventId)
                .OrderBy(c => c.Name)
                .Select(c => new CategoryResponse(c.CategoryID, c.Name, c.MinAge, c.MaxAge, c.Distance, c.EntryFee))
                .ToListAsync();

            return Ok(categories);
        }

        /// <summary>Add a category to one of your events (Organiser only).</summary>
        /// <remarks>Send name (for example "Under 20" or "Senior 10km"), minAge, maxAge, distance and entryFee.</remarks>
        /// <response code="201">Category created.</response>
        /// <response code="400">Invalid data (for example maxAge is smaller than minAge).</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">You are not an Organiser, or the event belongs to another Organiser.</response>
        /// <response code="404">Event not found.</response>
        [HttpPost("events/{eventId:int}/categories")]
        [Authorize(Roles = Roles.Organiser)]
        [ProducesResponseType(typeof(CategoryCreatedResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateCategory(int eventId, [FromBody] CategoryDto dto)
        {
            var ev = await _context.Events.FindAsync(eventId);
            if (ev == null) return NotFound(new MessageResponse("Event not found."));

            if (ev.OrganiserID != User.GetUserId())
            {
                return StatusCode(StatusCodes.Status403Forbidden, new MessageResponse("You can only add categories to your own events."));
            }

            if (dto.MaxAge < dto.MinAge)
            {
                return BadRequest(new MessageResponse("MaxAge cannot be smaller than MinAge."));
            }

            var category = new Category
            {
                EventID = eventId,
                Name = dto.Name.Trim(),
                MinAge = dto.MinAge,
                MaxAge = dto.MaxAge,
                Distance = dto.Distance,
                EntryFee = dto.EntryFee
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            return StatusCode(StatusCodes.Status201Created, new CategoryCreatedResponse(category.CategoryID, "Category created successfully."));
        }

        /// <summary>Update a category of one of your events (Organiser only).</summary>
        /// <response code="200">Category updated.</response>
        /// <response code="400">Invalid data.</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">You are not an Organiser, or the event belongs to another Organiser.</response>
        /// <response code="404">Category not found.</response>
        [HttpPut("categories/{id:int}")]
        [Authorize(Roles = Roles.Organiser)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateCategory(int id, [FromBody] CategoryDto dto)
        {
            var category = await _context.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryID == id);
            if (category == null) return NotFound(new MessageResponse("Category not found."));

            if (category.Event.OrganiserID != User.GetUserId())
            {
                return StatusCode(StatusCodes.Status403Forbidden, new MessageResponse("You can only change categories of your own events."));
            }

            if (dto.MaxAge < dto.MinAge)
            {
                return BadRequest(new MessageResponse("MaxAge cannot be smaller than MinAge."));
            }

            category.Name = dto.Name.Trim();
            category.MinAge = dto.MinAge;
            category.MaxAge = dto.MaxAge;
            category.Distance = dto.Distance;
            category.EntryFee = dto.EntryFee;

            await _context.SaveChangesAsync();
            return Ok(new MessageResponse("Category updated successfully."));
        }

        /// <summary>Delete a category of one of your events (Organiser only).</summary>
        /// <remarks>A category that already has enrolments cannot be deleted.</remarks>
        /// <response code="200">Category deleted.</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">You are not an Organiser, or the event belongs to another Organiser.</response>
        /// <response code="404">Category not found.</response>
        /// <response code="409">The category has enrolments.</response>
        [HttpDelete("categories/{id:int}")]
        [Authorize(Roles = Roles.Organiser)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryID == id);
            if (category == null) return NotFound(new MessageResponse("Category not found."));

            if (category.Event.OrganiserID != User.GetUserId())
            {
                return StatusCode(StatusCodes.Status403Forbidden, new MessageResponse("You can only delete categories of your own events."));
            }

            if (await _context.Enrollments.AnyAsync(e => e.CategoryID == id))
            {
                return Conflict(new MessageResponse("This category already has enrolments and cannot be deleted."));
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            return Ok(new MessageResponse("Category deleted successfully."));
        }
    }

    public class CategoryDto
    {
        /// <summary>Category name, for example "Under 20" or "Senior 10km".</summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Youngest allowed age.</summary>
        [Range(0, 120)]
        public int MinAge { get; set; }

        /// <summary>Oldest allowed age.</summary>
        [Range(0, 120)]
        public int MaxAge { get; set; }

        /// <summary>Distance of this category in kilometres.</summary>
        [Range(0.0, 1000.0)]
        public decimal Distance { get; set; }

        /// <summary>Entry fee.</summary>
        [Range(0.0, 100000.0)]
        public decimal EntryFee { get; set; }
    }
}

