using System.Net;
using System.Net.Http.Json;
using RaceDayApi.Models;
using RaceDayApi.Tests.TestSupport;

namespace RaceDayApi.Tests;

/// <summary>HTTP tests for event filters and the Organiser dashboard data (GET /api/events/mine).</summary>
public class EventListingApiTests : IClassFixture<RaceDayWebFactory>
{
    private readonly RaceDayWebFactory _factory;

    public EventListingApiTests(RaceDayWebFactory factory)
    {
        _factory = factory;
    }

    private static async Task<int> CreateEventAsync(HttpClient organiser, string name, string eventType, string location = "Pretoria")
    {
        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            name,
            description = "Filter test",
            eventDate = DateTime.UtcNow.AddDays(15),
            location,
            distance = 5,
            eventType
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<EventCreatedResponse>();
        return body!.EventId;
    }

    [Fact]
    public async Task Filter_ByEventType_ReturnsOnlyThatType()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var participant = await _factory.CreateUserAsync(Roles.Participant);
        var tag = Guid.NewGuid().ToString("N");
        await CreateEventAsync(organiser.Client, $"Run {tag}", "Run");
        await CreateEventAsync(organiser.Client, $"Walk {tag}", "Walk");

        var walks = await participant.Client.GetFromJsonAsync<List<EventSummaryResponse>>($"/api/events?eventType=Walk&search={tag}");

        var only = Assert.Single(walks!);
        Assert.Equal("Walk", only.EventType);
    }

    [Fact]
    public async Task Filter_BySearchText_MatchesNameOrLocation()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var participant = await _factory.CreateUserAsync(Roles.Participant);
        var tag = Guid.NewGuid().ToString("N");
        await CreateEventAsync(organiser.Client, $"Sunrise {tag}", "Run", "Durban");
        await CreateEventAsync(organiser.Client, "Other event", "Run", $"Place {tag}");
        await CreateEventAsync(organiser.Client, "Unrelated", "Run", "Cape Town");

        var found = await participant.Client.GetFromJsonAsync<List<EventSummaryResponse>>($"/api/events?search={tag}");

        Assert.Equal(2, found!.Count);
    }

    [Fact]
    public async Task UpcomingOnly_HidesEventsInThePast()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var participant = await _factory.CreateUserAsync(Roles.Participant);
        var tag = Guid.NewGuid().ToString("N");
        await CreateEventAsync(organiser.Client, $"Future {tag}", "Run");

        var upcoming = await participant.Client.GetFromJsonAsync<List<EventSummaryResponse>>($"/api/events?upcomingOnly=true&search={tag}");

        Assert.Single(upcoming!);
    }

    [Fact]
    public async Task Mine_ReturnsOnlyOwnEvents_WithEnrolmentAndCategoryCounts()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var otherOrganiser = await _factory.CreateUserAsync(Roles.Organiser);
        var runnerOne = await _factory.CreateUserAsync(Roles.Participant);
        var runnerTwo = await _factory.CreateUserAsync(Roles.Participant);

        var eventId = await _factory.CreateEventAsync(organiser.Client);
        var categoryId = await _factory.CreateCategoryAsync(organiser.Client, eventId);
        await _factory.EnrolAsync(runnerOne.Client, eventId, categoryId);
        await _factory.EnrolAsync(runnerTwo.Client, eventId, categoryId);
        await _factory.CreateEventAsync(otherOrganiser.Client);

        var mine = await organiser.Client.GetFromJsonAsync<List<OrganiserEventResponse>>("/api/events/mine");

        var only = Assert.Single(mine!);
        Assert.Equal(eventId, only.EventId);
        Assert.Equal(2, only.EnrolmentCount);
        Assert.Equal(1, only.CategoryCount);
    }

    [Fact]
    public async Task Mine_AsParticipant_Returns403()
    {
        var participant = await _factory.CreateUserAsync(Roles.Participant);

        var response = await participant.Client.GetAsync("/api/events/mine");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Mine_WithoutToken_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/events/mine");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
