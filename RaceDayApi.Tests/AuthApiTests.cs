using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RaceDayApi.Data;
using RaceDayApi.Models;
using RaceDayApi.Tests.TestSupport;

namespace RaceDayApi.Tests;

/// <summary>
/// HTTP tests against the running API: registration, login, and the difference between
/// authenticated and unauthenticated requests.
/// </summary>
public class AuthApiTests : IClassFixture<RaceDayWebFactory>
{
    private readonly RaceDayWebFactory _factory;

    public AuthApiTests(RaceDayWebFactory factory)
    {
        _factory = factory;
    }

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    [Fact]
    public async Task Register_Participant_Returns201()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "New Runner",
            email = NewEmail(),
            password = RaceDayWebFactory.Password,
            role = Roles.Participant
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.Equal(Roles.Participant, body!.Role);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var client = _factory.CreateClient();
        var email = NewEmail();
        var payload = new { fullName = "Runner", email, password = RaceDayWebFactory.Password, role = Roles.Participant };

        var first = await client.PostAsJsonAsync("/api/auth/register", payload);
        var second = await client.PostAsJsonAsync("/api/auth/register", payload);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_ShortPassword_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Runner",
            email = NewEmail(),
            password = "short",
            role = Roles.Participant
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_UnknownRole_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Sneaky",
            email = NewEmail(),
            password = RaceDayWebFactory.Password,
            role = "Admin"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_Organiser_WithoutCode_Returns403()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Fake Organiser",
            email = NewEmail(),
            password = RaceDayWebFactory.Password,
            role = Roles.Organiser
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_Organiser_WithWrongCode_Returns403()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Fake Organiser",
            email = NewEmail(),
            password = RaceDayWebFactory.Password,
            role = Roles.Organiser,
            organiserCode = "NOT-THE-CODE"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_Organiser_WithCorrectCode_Returns201()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Real Organiser",
            email = NewEmail(),
            password = RaceDayWebFactory.Password,
            role = Roles.Organiser,
            organiserCode = _factory.OrganiserCode
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Login_CorrectCredentials_ReturnsTokenWithRole()
    {
        var user = await _factory.CreateUserAsync(Roles.Organiser);

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new
        {
            email = user.Email,
            password = RaceDayWebFactory.Password
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.Equal(Roles.Organiser, body.Role);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var user = await _factory.CreateUserAsync(Roles.Participant);

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new
        {
            email = user.Email,
            password = "WrongPassword!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new
        {
            email = NewEmail(),
            password = RaceDayWebFactory.Password
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Password_IsStoredHashed_NotAsPlainText()
    {
        var user = await _factory.CreateUserAsync(Roles.Participant);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var saved = db.Participants.Single(p => p.Email == user.Email);

        Assert.NotEqual(RaceDayWebFactory.Password, saved.PasswordHash);
        Assert.False(string.IsNullOrWhiteSpace(saved.PasswordHash));
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithInvalidToken_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "this.is.not-a-real-token");

        var response = await client.GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithValidToken_Returns200()
    {
        var user = await _factory.CreateUserAsync(Roles.Participant);

        var response = await user.Client.GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Profile_CanViewAndUpdateOwnProfile()
    {
        var user = await _factory.CreateUserAsync(Roles.Participant);

        var view = await user.Client.GetAsync($"/api/users/{user.UserId}");
        Assert.Equal(HttpStatusCode.OK, view.StatusCode);
        var profile = await view.Content.ReadFromJsonAsync<ProfileResponse>();
        Assert.Equal(user.Email, profile!.Email);
        Assert.Equal(Roles.Participant, profile.Role);

        var update = await user.Client.PutAsJsonAsync($"/api/users/{user.UserId}", new
        {
            fullName = "Updated Name",
            email = user.Email
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var again = await user.Client.GetAsync($"/api/users/{user.UserId}");
        var updated = await again.Content.ReadFromJsonAsync<ProfileResponse>();
        Assert.Equal("Updated Name", updated!.FullName);
    }

    [Fact]
    public async Task Profile_CannotViewOrUpdateSomeoneElsesProfile()
    {
        var alice = await _factory.CreateUserAsync(Roles.Participant);
        var bob = await _factory.CreateUserAsync(Roles.Participant);

        var view = await alice.Client.GetAsync($"/api/users/{bob.UserId}");
        var update = await alice.Client.PutAsJsonAsync($"/api/users/{bob.UserId}", new
        {
            fullName = "Hacked",
            email = bob.Email
        });

        Assert.Equal(HttpStatusCode.Forbidden, view.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
    }

    [Fact]
    public async Task Profile_EmailAlreadyUsed_Returns409()
    {
        var alice = await _factory.CreateUserAsync(Roles.Participant);
        var bob = await _factory.CreateUserAsync(Roles.Participant);

        var response = await alice.Client.PutAsJsonAsync($"/api/users/{alice.UserId}", new
        {
            fullName = "Alice",
            email = bob.Email
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}

