using System.Security.Claims;

namespace RaceDayApi.Services
{
    /// <summary>Claim names written into the JWT and read back on every request.</summary>
    public static class AppClaims
    {
        public const string UserId = "sub";
        public const string Name = "name";
        public const string Email = "email";
        public const string Role = "role";
    }

    public static class ClaimsPrincipalExtensions
    {
        /// <summary>The logged-in user's id (OrganiserID or ParticipantID, depending on the role).</summary>
        public static int? GetUserId(this ClaimsPrincipal user)
        {
            var value = user.FindFirst(AppClaims.UserId)?.Value;
            return int.TryParse(value, out var id) ? id : null;
        }

        public static string? GetRole(this ClaimsPrincipal user) => user.FindFirst(AppClaims.Role)?.Value;
    }
}

