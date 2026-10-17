using System.ComponentModel.DataAnnotations;

namespace RaceDayApi.Models
{
    public class RegisterDto
    {
        /// <summary>Full name of the user.</summary>
        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        /// <summary>Email address. Must be unique across Organisers and Participants.</summary>
        [Required]
        [EmailAddress]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        /// <summary>Password (minimum 8 characters). It is stored hashed, never as plain text.</summary>
        [Required]
        [MinLength(8)]
        public string Password { get; set; } = string.Empty;

        /// <summary>"Participant" (default) or "Organiser".</summary>
        public string Role { get; set; } = Roles.Participant;

        /// <summary>
        /// Secret code that is required to register as an Organiser. Participants leave this empty.
        /// This stops anyone from creating an Organiser account on their own.
        /// </summary>
        public string? OrganiserCode { get; set; }
    }

    public class LoginDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}

