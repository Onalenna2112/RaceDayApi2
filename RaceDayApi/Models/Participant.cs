namespace RaceDayApi.Models
{
    public class Participant
    {
        public int ParticipantID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    }
}