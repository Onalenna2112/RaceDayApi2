using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RaceDayWeb.Models;

namespace RaceDayWeb.Services
{
    /// <summary>The outcome of one call to the API. Pages use it instead of handling raw HTTP errors.</summary>
    public class ApiResult<T>
    {
        public bool Success { get; init; }
        public T? Data { get; init; }
        public string? Error { get; init; }
        public int StatusCode { get; init; }

        /// <summary>True when the token was missing, invalid or expired.</summary>
        public bool Unauthorized => StatusCode == (int)HttpStatusCode.Unauthorized;
    }

    public interface IRaceDayApiClient
    {
        // Authentication
        Task<ApiResult<LoginResult>> LoginAsync(string email, string password);
        Task<ApiResult<object>> RegisterAsync(RegisterRequest request);

        // Events
        Task<ApiResult<List<EventSummary>>> GetEventsAsync(bool upcomingOnly, string? eventType, string? search);
        Task<ApiResult<EventDetail>> GetEventAsync(int id);
        Task<ApiResult<List<OrganiserEvent>>> GetMyEventsAsync();
        Task<ApiResult<EventCreated>> CreateEventAsync(EventRequest request);
        Task<ApiResult<object>> UpdateEventAsync(int id, EventRequest request);
        Task<ApiResult<object>> DeleteEventAsync(int id);
        Task<ApiResult<ImageUploadResult>> UploadBannerAsync(int eventId, IFormFile file);

        // Categories
        Task<ApiResult<object>> CreateCategoryAsync(int eventId, CategoryRequest request);
        Task<ApiResult<object>> UpdateCategoryAsync(int id, CategoryRequest request);
        Task<ApiResult<object>> DeleteCategoryAsync(int id);

        // Enrolments
        Task<ApiResult<object>> EnrolAsync(int eventId, int categoryId);
        Task<ApiResult<List<EventEnrolment>>> GetEventEnrolmentsAsync(int eventId);
        Task<ApiResult<List<MyEnrolment>>> GetMyEnrolmentsAsync(int participantId);
        Task<ApiResult<object>> CancelEnrolmentAsync(int enrolmentId);
        Task<ApiResult<object>> UpdateEnrolmentStatusAsync(int enrolmentId, string status);

        // Results
        Task<ApiResult<object>> CaptureResultAsync(int enrolmentId, string finishTime, int position);
        Task<ApiResult<List<EventResult>>> GetEventResultsAsync(int eventId);
        Task<ApiResult<List<MyResult>>> GetMyResultsAsync(int participantId);

        // Profile
        Task<ApiResult<UserProfile>> GetProfileAsync(int userId);
        Task<ApiResult<object>> UpdateProfileAsync(int userId, string fullName, string email);
        Task<ApiResult<ImageUploadResult>> UploadProfilePictureAsync(int userId, IFormFile file);
    }

    /// <summary>
    /// Talks to the RaceDay API over HTTP and adds the logged-in user's token to every request.
    /// This is the only place in the web site that knows how to reach the data.
    /// </summary>
    public class RaceDayApiClient : IRaceDayApiClient
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _client;
        private readonly IHttpContextAccessor _accessor;
        private readonly ILogger<RaceDayApiClient> _logger;

        public RaceDayApiClient(HttpClient client, IHttpContextAccessor accessor, ILogger<RaceDayApiClient> logger)
        {
            _client = client;
            _accessor = accessor;
            _logger = logger;
        }

        // ---------- Authentication ----------

        public Task<ApiResult<LoginResult>> LoginAsync(string email, string password) =>
            SendAsync<LoginResult>(HttpMethod.Post, "api/auth/login", Body(new { email, password }));

        public Task<ApiResult<object>> RegisterAsync(RegisterRequest request) =>
            SendAsync<object>(HttpMethod.Post, "api/auth/register", Body(request));

        // ---------- Events ----------

        public Task<ApiResult<List<EventSummary>>> GetEventsAsync(bool upcomingOnly, string? eventType, string? search)
        {
            var parts = new List<string> { "upcomingOnly=" + (upcomingOnly ? "true" : "false") };
            if (!string.IsNullOrWhiteSpace(eventType)) parts.Add("eventType=" + Uri.EscapeDataString(eventType.Trim()));
            if (!string.IsNullOrWhiteSpace(search)) parts.Add("search=" + Uri.EscapeDataString(search.Trim()));
            return SendAsync<List<EventSummary>>(HttpMethod.Get, "api/events?" + string.Join("&", parts));
        }

        public Task<ApiResult<EventDetail>> GetEventAsync(int id) =>
            SendAsync<EventDetail>(HttpMethod.Get, $"api/events/{id}");

        public Task<ApiResult<List<OrganiserEvent>>> GetMyEventsAsync() =>
            SendAsync<List<OrganiserEvent>>(HttpMethod.Get, "api/events/mine");

        public Task<ApiResult<EventCreated>> CreateEventAsync(EventRequest request) =>
            SendAsync<EventCreated>(HttpMethod.Post, "api/events", Body(request));

        public Task<ApiResult<object>> UpdateEventAsync(int id, EventRequest request) =>
            SendAsync<object>(HttpMethod.Put, $"api/events/{id}", Body(request));

        public Task<ApiResult<object>> DeleteEventAsync(int id) =>
            SendAsync<object>(HttpMethod.Delete, $"api/events/{id}");

        public Task<ApiResult<ImageUploadResult>> UploadBannerAsync(int eventId, IFormFile file) =>
            SendAsync<ImageUploadResult>(HttpMethod.Post, $"api/events/{eventId}/banner", FileBody(file));

        // ---------- Categories ----------

        public Task<ApiResult<object>> CreateCategoryAsync(int eventId, CategoryRequest request) =>
            SendAsync<object>(HttpMethod.Post, $"api/events/{eventId}/categories", Body(request));

        public Task<ApiResult<object>> UpdateCategoryAsync(int id, CategoryRequest request) =>
            SendAsync<object>(HttpMethod.Put, $"api/categories/{id}", Body(request));

        public Task<ApiResult<object>> DeleteCategoryAsync(int id) =>
            SendAsync<object>(HttpMethod.Delete, $"api/categories/{id}");

        // ---------- Enrolments ----------

        public Task<ApiResult<object>> EnrolAsync(int eventId, int categoryId) =>
            SendAsync<object>(HttpMethod.Post, $"api/events/{eventId}/enrolments", Body(new { categoryID = categoryId }));

        public Task<ApiResult<List<EventEnrolment>>> GetEventEnrolmentsAsync(int eventId) =>
            SendAsync<List<EventEnrolment>>(HttpMethod.Get, $"api/events/{eventId}/enrolments");

        public Task<ApiResult<List<MyEnrolment>>> GetMyEnrolmentsAsync(int participantId) =>
            SendAsync<List<MyEnrolment>>(HttpMethod.Get, $"api/participants/{participantId}/enrolments");

        public Task<ApiResult<object>> CancelEnrolmentAsync(int enrolmentId) =>
            SendAsync<object>(HttpMethod.Delete, $"api/enrolments/{enrolmentId}");

        public Task<ApiResult<object>> UpdateEnrolmentStatusAsync(int enrolmentId, string status) =>
            SendAsync<object>(HttpMethod.Put, $"api/enrolments/{enrolmentId}/status", Body(new { status }));

        // ---------- Results ----------

        public Task<ApiResult<object>> CaptureResultAsync(int enrolmentId, string finishTime, int position) =>
            SendAsync<object>(HttpMethod.Post, $"api/enrolments/{enrolmentId}/results", Body(new { finishTime, position }));

        public Task<ApiResult<List<EventResult>>> GetEventResultsAsync(int eventId) =>
            SendAsync<List<EventResult>>(HttpMethod.Get, $"api/events/{eventId}/results");

        public Task<ApiResult<List<MyResult>>> GetMyResultsAsync(int participantId) =>
            SendAsync<List<MyResult>>(HttpMethod.Get, $"api/participants/{participantId}/results");

        // ---------- Profile ----------

        public Task<ApiResult<UserProfile>> GetProfileAsync(int userId) =>
            SendAsync<UserProfile>(HttpMethod.Get, $"api/users/{userId}");

        public Task<ApiResult<object>> UpdateProfileAsync(int userId, string fullName, string email) =>
            SendAsync<object>(HttpMethod.Put, $"api/users/{userId}", Body(new { fullName, email }));

        public Task<ApiResult<ImageUploadResult>> UploadProfilePictureAsync(int userId, IFormFile file) =>
            SendAsync<ImageUploadResult>(HttpMethod.Post, $"api/users/{userId}/profile-picture", FileBody(file));

        // ---------- Plumbing ----------

        private static HttpContent Body<TBody>(TBody body) => JsonContent.Create(body, options: Json);

        private static HttpContent FileBody(IFormFile file)
        {
            var stream = file.OpenReadStream();
            var fileContent = new StreamContent(stream);
            if (!string.IsNullOrWhiteSpace(file.ContentType))
            {
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            }

            var form = new MultipartFormDataContent();
            form.Add(fileContent, "file", file.FileName);
            return form;
        }

        private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, HttpContent? content = null)
        {
            try
            {
                using var request = new HttpRequestMessage(method, url) { Content = content };

                var context = _accessor.HttpContext;
                var user = context == null ? null : UserSession.Get(context);
                if (user != null)
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
                }

                using var response = await _client.SendAsync(request);
                var status = (int)response.StatusCode;

                if (response.IsSuccessStatusCode)
                {
                    if (typeof(T) == typeof(object))
                    {
                        return new ApiResult<T> { Success = true, StatusCode = status };
                    }

                    var data = await response.Content.ReadFromJsonAsync<T>(Json);
                    return new ApiResult<T> { Success = true, Data = data, StatusCode = status };
                }

                var apiMessage = await ReadMessageAsync(response);
                return Failure<T>(status, Describe(status, apiMessage));
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Could not reach the RaceDay API.");
                return Failure<T>(0, "We could not reach the RaceDay service. Please try again in a moment.");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "The RaceDay API took too long to answer.");
                return Failure<T>(0, "The RaceDay service took too long to answer. Please try again.");
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "The RaceDay API sent a reply this site could not read.");
                return Failure<T>(0, "We received an unexpected reply from the RaceDay service. Please try again.");
            }
        }

        private static ApiResult<T> Failure<T>(int status, string message) =>
            new() { Success = false, StatusCode = status, Error = message };

        private static string Describe(int status, string? apiMessage)
        {
            if (!string.IsNullOrWhiteSpace(apiMessage) && status < 500)
            {
                // Messages with technical text are replaced by a friendly one.
                if (apiMessage.Contains("JSON", StringComparison.OrdinalIgnoreCase) || apiMessage.Contains("$."))
                {
                    return "Some of the details you entered are not valid. Please check them and try again.";
                }

                return apiMessage;
            }

            return status switch
            {
                401 => "Your session has expired. Please log in again.",
                403 => "You do not have permission to do that.",
                404 => "We could not find what you were looking for.",
                >= 500 => "Something went wrong on our side. Please try again in a moment.",
                _ => "That request could not be completed. Please try again."
            };
        }

        private static async Task<string?> ReadMessageAsync(HttpResponseMessage response)
        {
            try
            {
                var text = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(text)) return null;

                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return null;

                if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                {
                    return message.GetString();
                }

                if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in errors.EnumerateObject())
                    {
                        if (property.Value.ValueKind == JsonValueKind.Array && property.Value.GetArrayLength() > 0)
                        {
                            return property.Value[0].GetString();
                        }
                    }
                }

                return null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
