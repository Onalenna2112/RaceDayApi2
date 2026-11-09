namespace RaceDayWeb.Models
{
    // These records match the JSON that the RaceDay API sends and receives.
    // The web site has no database access: everything comes from the API.

    public record LoginResult(string Token, DateTime ExpiresAt, int UserId, string FullName, string Email, string Role, string Message);

    public record UserProfile(int UserId, string FullName, string Email, string Role, string? ProfilePictureUrl);

    public record CategoryItem(int CategoryId, string Name, int MinAge, int MaxAge, decimal Distance, decimal EntryFee);

    public record EventSummary(int EventId, string Name, string Description, DateTime EventDate, string Location, decimal Distance, string EventType, string OrganiserName, string? BannerImageUrl);

    public record EventDetail(int EventId, string Name, string Description, DateTime EventDate, string Location, decimal Distance, string EventType, string OrganiserName, string? BannerImageUrl, List<CategoryItem> Categories);

    public record OrganiserEvent(int EventId, string Name, DateTime EventDate, string Location, string EventType, string? BannerImageUrl, int EnrolmentCount, int CategoryCount);

    public record EventCreated(int EventId, string Message);

    public record EventEnrolment(int EnrolmentId, int ParticipantId, string ParticipantName, string ParticipantEmail, int CategoryId, string CategoryName, string Status, DateTime EnrolmentDate);

    public record MyEnrolment(int EnrolmentId, int EventId, string EventName, DateTime EventDate, string Location, int CategoryId, string CategoryName, string Status, DateTime EnrolmentDate);

    public record EventResult(int ResultId, int EnrolmentId, int ParticipantId, string ParticipantName, string CategoryName, string FinishTime, int Position);

    public record MyResult(int ResultId, int EnrolmentId, int EventId, string EventName, DateTime EventDate, string CategoryName, string FinishTime, int Position, int TotalFinishers);

    public record ImageUploadResult(string ImageUrl, string Message);

    // Requests sent to the API
    public record RegisterRequest(string FullName, string Email, string Password, string Role, string? OrganiserCode);

    public record EventRequest(string Name, string Description, DateTime EventDate, string Location, decimal Distance, string EventType);

    public record CategoryRequest(string Name, int MinAge, int MaxAge, decimal Distance, decimal EntryFee);
}
