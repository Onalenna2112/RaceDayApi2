namespace RaceDayApi.Models
{
    // Response models. They exist so that Swagger can show the shape of every response
    // and so that private data (such as password hashes) is never returned.

    public record MessageResponse(string Message);

    public record RegisterResponse(int UserId, string Role, string Message);

    public record LoginResponse(string Token, DateTime ExpiresAt, int UserId, string FullName, string Email, string Role, string Message);

    public record ProfileResponse(int UserId, string FullName, string Email, string Role);

    public record CategoryResponse(int CategoryId, string Name, int MinAge, int MaxAge, decimal Distance, decimal EntryFee);

    public record EventSummaryResponse(int EventId, string Name, string Description, DateTime EventDate, string Location, decimal Distance, string EventType, string OrganiserName);

    public record EventDetailResponse(int EventId, string Name, string Description, DateTime EventDate, string Location, decimal Distance, string EventType, string OrganiserName, List<CategoryResponse> Categories);

    public record EventCreatedResponse(int EventId, string Message);

    public record CategoryCreatedResponse(int CategoryId, string Message);

    public record EnrolmentCreatedResponse(int EnrolmentId, string Message);

    public record EventEnrolmentResponse(int EnrolmentId, int ParticipantId, string ParticipantName, string ParticipantEmail, int CategoryId, string CategoryName, string Status, DateTime EnrolmentDate);

    public record MyEnrolmentResponse(int EnrolmentId, int EventId, string EventName, DateTime EventDate, string Location, int CategoryId, string CategoryName, string Status, DateTime EnrolmentDate);

    public record ResultSavedResponse(int ResultId, string Message);

    public record EventResultResponse(int ResultId, int EnrolmentId, int ParticipantId, string ParticipantName, string CategoryName, string FinishTime, int Position);

    public record MyResultResponse(int ResultId, int EnrolmentId, int EventId, string EventName, DateTime EventDate, string FinishTime, int Position);
}

