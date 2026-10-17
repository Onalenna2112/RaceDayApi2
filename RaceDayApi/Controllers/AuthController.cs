using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayApi.Data;
using RaceDayApi.Models;
using RaceDayApi.Services;

namespace RaceDayApi.Controllers
{
    /// <summary>Register and log in. These are the only public endpoints in the API.</summary>
    [Route("api/auth")]
    [ApiController]
    [AllowAnonymous]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly RaceDayDbContext _context;
        private readonly ITokenService _tokenService;
        private readonly IConfiguration _config;
        private readonly PasswordHasher<object> _passwordHasher = new();

        public AuthController(RaceDayDbContext context, ITokenService tokenService, IConfiguration config)
        {
            _context = context;
            _tokenService = tokenService;
            _config = config;
        }

        /// <summary>Register a new account as a Participant or an Organiser.</summary>
        /// <remarks>
        /// Send fullName, email, password (8+ characters) and role ("Participant" or "Organiser").
        /// Registering as an Organiser also needs the secret organiserCode, so nobody can
        /// create an Organiser account without permission. The password is stored hashed.
        /// </remarks>
        /// <response code="201">Account created.</response>
        /// <response code="400">Invalid data or unknown role.</response>
        /// <response code="403">Organiser code missing or wrong.</response>
        /// <response code="409">The email address is already registered.</response>
        [HttpPost("register")]
        [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var role = NormaliseRole(dto.Role);
            if (role == null)
            {
                return BadRequest(new MessageResponse("Role must be either 'Organiser' or 'Participant'."));
            }

            var email = dto.Email.Trim().ToLowerInvariant();

            if (await EmailInUseAsync(email))
            {
                return Conflict(new MessageResponse("This email is already registered."));
            }

            if (role == Roles.Organiser)
            {
                var expectedCode = _config["Auth:OrganiserRegistrationCode"];
                if (string.IsNullOrWhiteSpace(expectedCode) || dto.OrganiserCode != expectedCode)
                {
                    return StatusCode(StatusCodes.Status403Forbidden,
                        new MessageResponse("A valid organiser registration code is required to register as an Organiser."));
                }

                var organiser = new Organiser
                {
                    FullName = dto.FullName.Trim(),
                    Email = email,
                    CreatedAt = DateTime.UtcNow
                };
                organiser.PasswordHash = _passwordHasher.HashPassword(organiser, dto.Password);

                _context.Organisers.Add(organiser);
                await _context.SaveChangesAsync();

                return StatusCode(StatusCodes.Status201Created,
                    new RegisterResponse(organiser.OrganiserID, Roles.Organiser, "Organiser registered successfully."));
            }

            var participant = new Participant
            {
                FullName = dto.FullName.Trim(),
                Email = email,
                CreatedAt = DateTime.UtcNow
            };
            participant.PasswordHash = _passwordHasher.HashPassword(participant, dto.Password);

            _context.Participants.Add(participant);
            await _context.SaveChangesAsync();

            return StatusCode(StatusCodes.Status201Created,
                new RegisterResponse(participant.ParticipantID, Roles.Participant, "Participant registered successfully."));
        }

        /// <summary>Log in with email and password.</summary>
        /// <remarks>
        /// On success you get a JWT token. The token holds your id and your role, so the API knows
        /// who you are on every later request. Click Authorize in Swagger and paste the token.
        /// </remarks>
        /// <response code="200">Login successful. The token is in the response.</response>
        /// <response code="401">Email or password is wrong.</response>
        [HttpPost("login")]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var email = dto.Email.Trim().ToLowerInvariant();

            int userId;
            string fullName, hash, role;
            object user;

            var participant = await _context.Participants.FirstOrDefaultAsync(p => p.Email == email);
            if (participant != null)
            {
                user = participant;
                userId = participant.ParticipantID;
                fullName = participant.FullName;
                hash = participant.PasswordHash;
                role = Roles.Participant;
            }
            else
            {
                var organiser = await _context.Organisers.FirstOrDefaultAsync(o => o.Email == email);
                if (organiser == null)
                {
                    // Same message for "unknown email" and "wrong password" so attackers learn nothing.
                    return Unauthorized(new MessageResponse("Invalid email or password."));
                }

                user = organiser;
                userId = organiser.OrganiserID;
                fullName = organiser.FullName;
                hash = organiser.PasswordHash;
                role = Roles.Organiser;
            }

            var verification = _passwordHasher.VerifyHashedPassword(user, hash, dto.Password);
            if (verification == PasswordVerificationResult.Failed)
            {
                return Unauthorized(new MessageResponse("Invalid email or password."));
            }

            var (token, expiresAt) = _tokenService.CreateToken(userId, fullName, email, role);

            return Ok(new LoginResponse(token, expiresAt, userId, fullName, email, role, "Login successful."));
        }

        private static string? NormaliseRole(string? role)
        {
            if (string.Equals(role, Roles.Organiser, StringComparison.OrdinalIgnoreCase)) return Roles.Organiser;
            if (string.Equals(role, Roles.Participant, StringComparison.OrdinalIgnoreCase)) return Roles.Participant;
            return null;
        }

        private async Task<bool> EmailInUseAsync(string email)
        {
            return await _context.Participants.AnyAsync(p => p.Email == email)
                || await _context.Organisers.AnyAsync(o => o.Email == email);
        }
    }
}

