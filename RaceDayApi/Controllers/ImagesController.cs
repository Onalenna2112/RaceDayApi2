using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayApi.Data;
using RaceDayApi.Models;
using RaceDayApi.Services;

namespace RaceDayApi.Controllers
{
    /// <summary>Image uploads. Files go to Azure Blob Storage through the API; the URL is saved in the database.</summary>
    [Route("api")]
    [ApiController]
    [Authorize]
    [Produces("application/json")]
    public class ImagesController : ControllerBase
    {
        private readonly RaceDayDbContext _context;
        private readonly IBlobStorageService _blobs;

        public ImagesController(RaceDayDbContext context, IBlobStorageService blobs)
        {
            _context = context;
            _blobs = blobs;
        }

        /// <summary>Upload or replace the banner image of one of your events (Organiser only).</summary>
        /// <remarks>Send a multipart form with one field called file. JPG, PNG or WEBP, up to 5 MB.</remarks>
        /// <response code="200">Banner saved. The image URL is in the response.</response>
        /// <response code="400">Not a valid image, or too large.</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">You are not an Organiser, or the event belongs to another Organiser.</response>
        /// <response code="404">Event not found.</response>
        /// <response code="502">The image storage service failed.</response>
        /// <response code="503">Image storage is not configured.</response>
        [HttpPost("events/{id:int}/banner")]
        [Authorize(Roles = Roles.Organiser)]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        [ProducesResponseType(typeof(ImageUploadResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status502BadGateway)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> UploadEventBanner(int id, IFormFile file)
        {
            var ev = await _context.Events.FindAsync(id);
            if (ev == null) return NotFound(new MessageResponse("Event not found."));

            if (ev.OrganiserID != User.GetUserId())
            {
                return StatusCode(StatusCodes.Status403Forbidden, new MessageResponse("You can only change the banner of your own events."));
            }

            var validation = await ImageValidator.ValidateAsync(file);
            if (!validation.Ok) return BadRequest(new MessageResponse(validation.Error!));

            if (!_blobs.IsConfigured)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new MessageResponse("Image storage is not configured on the server."));
            }

            string url;
            try
            {
                await using var stream = file.OpenReadStream();
                url = await _blobs.UploadAsync(stream, $"events/{id}/{Guid.NewGuid():N}{validation.Extension}", validation.ContentType);
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new MessageResponse("The image could not be saved. Please try again later."));
            }

            var oldUrl = ev.BannerImageUrl;
            ev.BannerImageUrl = url;
            await _context.SaveChangesAsync();
            await _blobs.DeleteAsync(oldUrl);

            return Ok(new ImageUploadResponse(url, "Event banner saved."));
        }

        /// <summary>Upload or replace your own profile picture (Participant only).</summary>
        /// <remarks>Send a multipart form with one field called file. JPG, PNG or WEBP, up to 5 MB. The id must be your own id.</remarks>
        /// <response code="200">Picture saved. The image URL is in the response.</response>
        /// <response code="400">Not a valid image, or too large.</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">You are not a Participant, or the id is not yours.</response>
        /// <response code="404">Profile not found.</response>
        /// <response code="502">The image storage service failed.</response>
        /// <response code="503">Image storage is not configured.</response>
        [HttpPost("users/{id:int}/profile-picture")]
        [Authorize(Roles = Roles.Participant)]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        [ProducesResponseType(typeof(ImageUploadResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status502BadGateway)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> UploadProfilePicture(int id, IFormFile file)
        {
            if (User.GetUserId() != id)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new MessageResponse("You can only change your own profile picture."));
            }

            var participant = await _context.Participants.FindAsync(id);
            if (participant == null) return NotFound(new MessageResponse("Profile not found."));

            var validation = await ImageValidator.ValidateAsync(file);
            if (!validation.Ok) return BadRequest(new MessageResponse(validation.Error!));

            if (!_blobs.IsConfigured)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new MessageResponse("Image storage is not configured on the server."));
            }

            string url;
            try
            {
                await using var stream = file.OpenReadStream();
                url = await _blobs.UploadAsync(stream, $"profiles/{id}/{Guid.NewGuid():N}{validation.Extension}", validation.ContentType);
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new MessageResponse("The image could not be saved. Please try again later."));
            }

            var oldUrl = participant.ProfilePictureUrl;
            participant.ProfilePictureUrl = url;
            await _context.SaveChangesAsync();
            await _blobs.DeleteAsync(oldUrl);

            return Ok(new ImageUploadResponse(url, "Profile picture saved."));
        }
    }
}
