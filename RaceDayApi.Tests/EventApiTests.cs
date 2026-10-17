using System.Net;
using System.Net.Http.Json;
using RaceDayApi.Models;
using RaceDayApi.Tests.TestSupport;

namespace RaceDayApi.Tests;

/// <summary>HTTP tests: Organisers manage events and categories; other roles are rejected.</summary>
public class EventApiTests : IClassFixture<RaceDayWebFactory>
{
    private readonly RaceDayWebFactory _factory;

    public EventApiTests(RaceDayWebFactory factory)
    {
        _factory = factory;
    }

    private static object ValidEvent(string name = "Park Run") => new
    {
        name,
        description = "Weekend race",
        eventDate = DateTime.UtcNow.AddDays(20),
        location = "Pretoria",
        distance = 5,
        eventType = "Run"
    };

    [Fact]
    public async Task Organiser_CanCreateEvent_AndBothRolesCanViewIt()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var participant = await _factory.CreateUserAsync(Roles.Participant);

        var create = await organiser.Client.PostAsJsonAsync("/api/events", ValidEvent());
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var eventId = (await create.Content.ReadFromJsonAsync<EventCreatedResponse>())!.EventId;

        var byOrganiser = await organiser.Client.GetAsync($"/api/events/{eventId}");
        var byParticipant = await participant.Client.GetAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.OK, byOrganiser.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byParticipant.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCreateEvent_Returns403()
    {
        var participant = await _factory.CreateUserAsync(Roles.Participant);

        var response = await participant.Client.PostAsJsonAsync("/api/events", ValidEvent());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotCreateEvent_Returns401()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/events", ValidEvent());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithInvalidEventType_Returns400()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);

        var response = await organiser.Client.PostAsJsonAsync("/api/events", new
        {
            name = "Swim",
            description = "Not allowed",
            eventDate = DateTime.UtcNow.AddDays(5),
            location = "Pool",
            distance = 1,
            eventType = "Swim"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetEvent_UnknownId_Returns404()
    {
        var participant = await _factory.CreateUserAsync(Roles.Participant);

        var response = await participant.Client.GetAsync("/api/events/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateOwnEvent()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var eventId = await _factory.CreateEventAsync(organiser.Client);

        var response = await organiser.Client.PutAsJsonAsync($"/api/events/{eventId}", ValidEvent("Renamed Race"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var check = await organiser.Client.GetFromJsonAsync<EventDetailResponse>($"/api/events/{eventId}");
        Assert.Equal("Renamed Race", check!.Name);
    }

    [Fact]
    public async Task Organiser_CannotUpdateAnotherOrganisersEvent_Returns403()
    {
        var owner = await _factory.CreateUserAsync(Roles.Organiser);
        var other = await _factory.CreateUserAsync(Roles.Organiser);
        var eventId = await _factory.CreateEventAsync(owner.Client);

        var response = await other.Client.PutAsJsonAsync($"/api/events/{eventId}", ValidEvent("Stolen"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotUpdateOrDeleteEvent_Returns403()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var participant = await _factory.CreateUserAsync(Roles.Participant);
        var eventId = await _factory.CreateEventAsync(organiser.Client);

        var update = await participant.Client.PutAsJsonAsync($"/api/events/{eventId}", ValidEvent());
        var delete = await participant.Client.DeleteAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanDeleteOwnEvent()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var eventId = await _factory.CreateEventAsync(organiser.Client);

        var delete = await organiser.Client.DeleteAsync($"/api/events/{eventId}");
        var get = await organiser.Client.GetAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanAddCategory_AndBothRolesCanViewCategories()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var participant = await _factory.CreateUserAsync(Roles.Participant);
        var eventId = await _factory.CreateEventAsync(organiser.Client);
        await _factory.CreateCategoryAsync(organiser.Client, eventId);

        var byOrganiser = await organiser.Client.GetFromJsonAsync<List<CategoryResponse>>($"/api/events/{eventId}/categories");
        var byParticipant = await participant.Client.GetFromJsonAsync<List<CategoryResponse>>($"/api/events/{eventId}/categories");

        Assert.Single(byOrganiser!);
        Assert.Single(byParticipant!);
        Assert.Equal("Senior 10km", byParticipant![0].Name);
    }

    [Fact]
    public async Task Participant_CannotCreateCategory_Returns403()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var participant = await _factory.CreateUserAsync(Roles.Participant);
        var eventId = await _factory.CreateEventAsync(organiser.Client);

        var response = await participant.Client.PostAsJsonAsync($"/api/events/{eventId}/categories", new
        {
            name = "Under 20",
            minAge = 0,
            maxAge = 19,
            distance = 5,
            entryFee = 50
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_WithMaxAgeBelowMinAge_Returns400()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var eventId = await _factory.CreateEventAsync(organiser.Client);

        var response = await organiser.Client.PostAsJsonAsync($"/api/events/{eventId}/categories", new
        {
            name = "Broken",
            minAge = 40,
            maxAge = 20,
            distance = 5,
            entryFee = 50
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_WithEnrolments_Returns409()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var participant = await _factory.CreateUserAsync(Roles.Participant);
        var eventId = await _factory.CreateEventAsync(organiser.Client);
        var categoryId = await _factory.CreateCategoryAsync(organiser.Client, eventId);
        await _factory.EnrolAsync(participant.Client, eventId, categoryId);

        var response = await organiser.Client.DeleteAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}

