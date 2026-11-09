using System.ComponentModel.DataAnnotations;

namespace RaceDayWeb.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Please enter your email address.")]
        [EmailAddress(ErrorMessage = "That does not look like a valid email address.")]
        [Display(Name = "Email address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter your password.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public string? ReturnUrl { get; set; }
    }

    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Please enter your full name.")]
        [StringLength(100)]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter your email address.")]
        [EmailAddress(ErrorMessage = "That does not look like a valid email address.")]
        [Display(Name = "Email address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please choose a password.")]
        [MinLength(8, ErrorMessage = "Your password must be at least 8 characters long.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please repeat your password.")]
        [Compare(nameof(Password), ErrorMessage = "The two passwords do not match.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = Roles.Participant;

        [Display(Name = "Organiser code")]
        public string? OrganiserCode { get; set; }
    }

    public class EventFilterViewModel
    {
        public string? Search { get; set; }
        public string? EventType { get; set; }
        public string SortBy { get; set; } = "date";
        public bool IncludePast { get; set; }
        public List<EventSummary> Events { get; set; } = new();
    }

    public class EventDetailsViewModel
    {
        public EventDetail Event { get; set; } = null!;
        public MyEnrolment? MyEnrolment { get; set; }
        public bool IsParticipant { get; set; }
        public bool EventIsPast { get; set; }
    }

    public class EventFormViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Please enter the event name.")]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter a description.")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please choose the event date and time.")]
        [Display(Name = "Date and time")]
        public DateTime? EventDate { get; set; }

        [Required(ErrorMessage = "Please enter the location.")]
        [StringLength(200)]
        public string Location { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter the distance in kilometres.")]
        [Range(0.1, 1000, ErrorMessage = "The distance must be between 0.1 and 1000 km.")]
        [Display(Name = "Distance (km)")]
        public decimal? Distance { get; set; }

        [Required(ErrorMessage = "Please choose an event type.")]
        [Display(Name = "Event type")]
        public string EventType { get; set; } = "Run";

        [Display(Name = "Banner image")]
        public IFormFile? BannerFile { get; set; }

        public string? ExistingBannerUrl { get; set; }
    }

    public class CategoryFormViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Please enter a category name.")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Range(0, 120, ErrorMessage = "Minimum age must be between 0 and 120.")]
        [Display(Name = "Minimum age")]
        public int MinAge { get; set; }

        [Range(0, 120, ErrorMessage = "Maximum age must be between 0 and 120.")]
        [Display(Name = "Maximum age")]
        public int MaxAge { get; set; } = 99;

        [Range(0, 1000, ErrorMessage = "The distance must be between 0 and 1000 km.")]
        [Display(Name = "Distance (km)")]
        public decimal Distance { get; set; }

        [Range(0, 100000, ErrorMessage = "The entry fee must be between 0 and 100000.")]
        [Display(Name = "Entry fee (R)")]
        public decimal EntryFee { get; set; }
    }

    public class CategoriesPageViewModel
    {
        public EventDetail Event { get; set; } = null!;
        public CategoryFormViewModel NewCategory { get; set; } = new();
    }

    public class EnrolmentsPageViewModel
    {
        public EventDetail Event { get; set; } = null!;
        public List<EventEnrolment> Enrolments { get; set; } = new();
    }

    public class ResultRow
    {
        public EventEnrolment Enrolment { get; set; } = null!;
        public EventResult? Result { get; set; }
    }

    public class ResultsPageViewModel
    {
        public EventDetail Event { get; set; } = null!;
        public List<ResultRow> Rows { get; set; } = new();
    }

    public class DashboardViewModel
    {
        public List<OrganiserEvent> Events { get; set; } = new();
        public List<OrganiserEvent> Upcoming { get; set; } = new();
        public int TotalEnrolments { get; set; }
        public OrganiserEvent? NextEvent { get; set; }
    }

    public class ProfileViewModel
    {
        [Required(ErrorMessage = "Please enter your full name.")]
        [StringLength(100)]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter your email address.")]
        [EmailAddress(ErrorMessage = "That does not look like a valid email address.")]
        [Display(Name = "Email address")]
        public string Email { get; set; } = string.Empty;

        public string? ProfilePictureUrl { get; set; }
    }
}
