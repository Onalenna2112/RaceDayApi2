using System.Net;
using System.Net.Http.Json;
using RaceDayApi.Models;
using RaceDayApi.Tests.TestSupport;

namespace RaceDayApi.Tests;

/// <summary>HTTP tests: Organisers capture results; Participants only see their own.</summary>
public class ResultApiTests : IClassFixture<RaceDayWebFactory>
{
    private readonly RaceDayWebFactory _factory;

    public ResultApiTests(RaceDayWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Organiser_CanCaptureResult_AndParticipantCanViewIt()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var participant = await _factory.CreateUserAsync(Roles.Participant);
        var eventId = await _factory.CreateEventAsync(organiser.Client);
        var categoryId = await _factory.CreateCategoryAsync(organiser.Client, eventId);
        var enrolmentId = await _factory.EnrolAsync(participant.Client, eventId, categoryId);

        var capture = await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/results", new
        {
            finishTime = "01:25:30",
            position = 3
        });
        Assert.Equal(HttpStatusCode.Created, capture.StatusCode);

        var view = await participant.Client.GetAsync($"/api/participants/{participant.UserId}/results");
        Assert.Equal(HttpStatusCode.OK, view.StatusCode);
        var results = await view.Content.ReadFromJsonAsync<List<MyResultResponse>>();
        var mine = Assert.Single(results!);
        Assert.Equal(3, mine.Position);
        Assert.Equal("01:25:30", mine.FinishTime);
        Assert.Equal(eventId, mine.EventId);
    }

    [Fact]
    public async Task Organiser_CanViewAllResultsOfOwnEvent_OrderedByPosition()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var runnerOne = await _factory.CreateUserAsync(Roles.Participant);
        var runnerTwo = await _factory.CreateUserAsync(Roles.Participant);
        var eventId = await _factory.CreateEventAsync(organiser.Client);
        var categoryId = await _factory.CreateCategoryAsync(organiser.Client, eventId);
        var enrolOne = await _factory.EnrolAsync(runnerOne.Client, eventId, categoryId);
        var enrolTwo = await _factory.EnrolAsync(runnerTwo.Client, eventId, categoryId);

        await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolOne}/results", new { finishTime = "00:55:00", position = 2 });
        await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolTwo}/results", new { finishTime = "00:50:00", position = 1 });

        var response = await organiser.Client.GetAsync($"/api/events/{eventId}/results");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<EventResultResponse>>();
        Assert.Equal(2, list!.Count);
        Assert.Equal(1, list[0].Position);
        Assert.Equal(runnerTwo.UserId, list[0].ParticipantId);
    }

    [Fact]
    public async Task CapturingResultTwice_UpdatesInsteadOfDuplicating()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var participant = await _factory.CreateUserAsync(Roles.Participant);
        var eventId = await _factory.CreateEventAsync(organiser.Client);
        var categoryId = await _factory.CreateCategoryAsync(organiser.Client, eventId);
        var enrolmentId = await _factory.EnrolAsync(participant.Client, eventId, categoryId);

        await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/results", new { finishTime = "01:00:00", position = 5 });
        await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/results", new { finishTime = "00:59:00", position = 4 });

        var results = await participant.Client.GetFromJsonAsync<List<MyResultResponse>>($"/api/participants/{participant.UserId}/results");
        var only = Assert.Single(results!);
        Assert.Equal(4, only.Position);
        Assert.Equal("00:59:00", only.FinishTime);
    }

    [Fact]
    public async Task Participant_CannotCaptureResult_Returns403()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var participant = await _factory.CreateUserAsync(Roles.Participant);
        var eventId = await _factory.CreateEventAsync(organiser.Client);
        var categoryId = await _factory.CreateCategoryAsync(organiser.Client, eventId);
        var enrolmentId = await _factory.EnrolAsync(participant.Client, eventId, categoryId);

        var response = await participant.Client.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/results", new
        {
            finishTime = "00:10:00",
            position = 1
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotCaptureResult_Returns401()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/enrolments/1/results", new
        {
            finishTime = "00:10:00",
            position = 1
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotCaptureResultForAnotherOrganisersEvent_Returns403()
    {
        var owner = await _factory.CreateUserAsync(Roles.Organiser);
        var other = await _factory.CreateUserAsync(Roles.Organiser);
        var participant = await _factory.CreateUserAsync(Roles.Participant);
        var eventId = await _factory.CreateEventAsync(owner.Client);
        var categoryId = await _factory.CreateCategoryAsync(owner.Client, eventId);
        var enrolmentId = await _factory.EnrolAsync(participant.Client, eventId, categoryId);

        var response = await other.Client.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/results", new
        {
            finishTime = "00:10:00",
            position = 1
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("90")]
    [InlineData("00:00:00")]
    public async Task CaptureResult_WithInvalidFinishTime_Returns400(string badTime)
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var participant = await _factory.CreateUserAsync(Roles.Participant);
        var eventId = await _factory.CreateEventAsync(organiser.Client);
        var categoryId = await _factory.CreateCategoryAsync(organiser.Client, eventId);
        var enrolmentId = await _factory.EnrolAsync(participant.Client, eventId, categoryId);

        var response = await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/results", new
        {
            finishTime = badTime,
            position = 1
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotViewSomeoneElsesResults_Returns403()
    {
        var alice = await _factory.CreateUserAsync(Roles.Participant);
        var bob = await _factory.CreateUserAsync(Roles.Participant);

        var response = await alice.Client.GetAsync($"/api/participants/{bob.UserId}/results");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotViewEventResultsList_Returns403()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var participant = await _factory.CreateUserAsync(Roles.Participant);
        var eventId = await _factory.CreateEventAsync(organiser.Client);

        var response = await participant.Client.GetAsync($"/api/events/{eventId}/results");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
