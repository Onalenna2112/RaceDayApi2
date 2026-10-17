using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RaceDayApi.Data;
using RaceDayApi.Models;

namespace RaceDayApi.Tests.TestSupport;

/// <summary>A logged-in test user with an HttpClient that already sends their token.</summary>
public record AuthUser(HttpClient Client, int UserId, string Email, string Role);

/// <summary>
/// Starts the real API in memory (real JWT authentication and real role checks)
/// but swaps SQL Server for an in-memory database.
/// </summary>
public class RaceDayWebFactory : WebApplicationFactory<Program>
{
    public const string Password = "Password123!";

    private readonly string _databaseName = Guid.NewGuid().ToString();

    public string OrganiserCode =>
        Services.GetRequiredService<IConfiguration>()["Auth:OrganiserRegistrationCode"]!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<RaceDayDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<RaceDayDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }

    /// <summary>Registers a new user with the given role, logs in and returns a client with the token set.</summary>
    public async Task<AuthUser> CreateUserAsync(string role)
    {
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@example.com";
        var client = CreateClient();

        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = $"Test {role}",
            email,
            password = Password,
            role,
            organiserCode = role == Roles.Organiser ? OrganiserCode : null
        });
        register.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
        return new AuthUser(client, body.UserId, email, role);
    }

    public async Task<int> CreateEventAsync(HttpClient organiser, int daysFromNow = 30)
    {
        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            name = "Test Event",
            description = "A test event",
            eventDate = DateTime.UtcNow.AddDays(daysFromNow),
            location = "Pretoria",
            distance = 10,
            eventType = "Run"
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<EventCreatedResponse>();
        return body!.EventId;
    }

    public async Task<int> CreateCategoryAsync(HttpClient organiser, int eventId)
    {
        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", new
        {
            name = "Senior 10km",
            minAge = 18,
            maxAge = 99,
            distance = 10,
            entryFee = 150
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CategoryCreatedResponse>();
        return body!.CategoryId;
    }

    public async Task<int> EnrolAsync(HttpClient participant, int eventId, int categoryId)
    {
        var response = await participant.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryID = categoryId });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<EnrolmentCreatedResponse>();
        return body!.EnrolmentId;
    }
}
