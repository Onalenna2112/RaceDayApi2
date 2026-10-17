using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayApi.Data;
using RaceDayApi.Models;
using RaceDayApi.Services;

namespace RaceDayApi.Controllers
{
    /// <summary>View and update your own profile (both roles).</summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [Produces("application/json")]
    public class UsersController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public UsersController(RaceDayDbContext context)
        {
            _context = context;
        }

        /// <summary>View your own profile.</summary>
        /// <remarks>The id must be your own id (from your login response). The password hash is never returned.</remarks>
        /// <response code="200">Your profile.</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">You tried to view someone else's profile.</response>
        /// <response code="404">Profile not found.</response>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetUserProfile(int id)
        {
            if (User.GetUserId() != id)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new MessageResponse("You can only view your own profile."));
            }

            if (User.GetRole() == Roles.Organiser)
            {
                var organiser = await _context.Organisers.FindAsync(id);
                if (organiser == null) return NotFound(new MessageResponse("Profile not found."));
                return Ok(new ProfileResponse(organiser.OrganiserID, organiser.FullName, organiser.Email, Roles.Organiser));
            }

            var participant = await _context.Participants.FindAsync(id);
            if (participant == null) return NotFound(new MessageResponse("Profile not found."));
            return Ok(new ProfileResponse(participant.ParticipantID, participant.FullName, participant.Email, Roles.Participant));
        }

        /// <summary>Update your own profile (full name and email).</summary>
        /// <response code="200">Profile updated.</response>
        /// <response code="400">Invalid data.</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">You tried to update someone else's profile.</response>
        /// <response code="404">Profile not found.</response>
        /// <response code="409">The email is already used by another account.</response>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateUserProfile(int id, [FromBody] UpdateProfileDto dto)
        {
            if (User.GetUserId() != id)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new MessageResponse("You can only update your own profile."));
            }

            var email = dto.Email.Trim().ToLowerInvariant();

            if (User.GetRole() == Roles.Organiser)
            {
                var organiser = await _context.Organisers.FindAsync(id);
                if (organiser == null) return NotFound(new MessageResponse("Profile not found."));

                var taken = await _context.Organisers.AnyAsync(o => o.Email == email && o.OrganiserID != id)
                         || await _context.Participants.AnyAsync(p => p.Email == email);
                if (taken) return Conflict(new MessageResponse("This email is already used by another account."));

                organiser.FullName = dto.FullName.Trim();
                organiser.Email = email;
            }
            else
            {
                var participant = await _context.Participants.FindAsync(id);
                if (participant == null) return NotFound(new MessageResponse("Profile not found."));

                var taken = await _context.Participants.AnyAsync(p => p.Email == email && p.ParticipantID != id)
                         || await _context.Organisers.AnyAsync(o => o.Email == email);
                if (taken) return Conflict(new MessageResponse("This email is already used by another account."));

                participant.FullName = dto.FullName.Trim();
                participant.Email = email;
            }

            await _context.SaveChangesAsync();
            return Ok(new MessageResponse("Profile updated successfully."));
        }
    }

    public class UpdateProfileDto
    {
        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;
    }
}
