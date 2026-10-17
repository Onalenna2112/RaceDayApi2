using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RaceDayApi.Data;
using RaceDayApi.Models;
using RaceDayApi.Tests.TestSupport;

namespace RaceDayApi.Tests;

/// <summary>HTTP tests: Participants enrol, and the enrolment is recorded correctly.</summary>
public class EnrolmentApiTests : IClassFixture<RaceDayWebFactory>
{
    private readonly RaceDayWebFactory _factory;

    public EnrolmentApiTests(RaceDayWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Participant_CanEnrol_AndEnrolmentIsRecordedCorrectly()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var participant = await _factory.CreateUserAsync(Roles.Participant);
        var eventId = await _factory.CreateEventAsync(organiser.Client);
        var categoryId = await _factory.CreateCategoryAsync(organiser.Client, eventId);

        var response = await participant.Client.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryID = categoryId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var enrolmentId = (await response.Content.ReadFromJsonAsync<EnrolmentCreatedResponse>())!.EnrolmentId;

        // Check the database: the link between participant, event and category is saved.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var saved = db.Enrollments.Single(e => e.EnrollmentID == enrolmentId);
        Assert.Equal(participant.UserId, saved.ParticipantID);
        Assert.Equal(eventId, saved.EventID);
        Assert.Equal(categoryId, saved.CategoryID);
        Assert.Equal("Enrolled", saved.Status);
    }

    [Fact]
    public async Task Participant_EnrollingTwiceInSameEvent_Returns409()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var participant = await _factory.CreateUserAsync(Roles.Participant);
        var eventId = await _factory.CreateEventAsync(organiser.Client);
        var categoryId = await _factory.CreateCategoryAsync(organiser.Client, eventId);
        await _factory.EnrolAsync(participant.Client, eventId, categoryId);

        var second = await participant.Client.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryID = categoryId });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Enrol_WithCategoryFromAnotherEvent_Returns400()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var participant = await _factory.CreateUserAsync(Roles.Participant);
        var eventA = await _factory.CreateEventAsync(organiser.Client);
        var eventB = await _factory.CreateEventAsync(organiser.Client);
        var categoryOfB = await _factory.CreateCategoryAsync(organiser.Client, eventB);

        var response = await participant.Client.PostAsJsonAsync($"/api/events/{eventA}/enrolments", new { categoryID = categoryOfB });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_InUnknownEvent_Returns404()
    {
        var participant = await _factory.CreateUserAsync(Roles.Participant);

        var response = await participant.Client.PostAsJsonAsync("/api/events/999999/enrolments", new { categoryID = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotEnrol_Returns403()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var eventId = await _factory.CreateEventAsync(organiser.Client);
        var categoryId = await _factory.CreateCategoryAsync(organiser.Client, eventId);

        var response = await organiser.Client.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryID = categoryId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotEnrol_Returns401()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var eventId = await _factory.CreateEventAsync(organiser.Client);
        var categoryId = await _factory.CreateCategoryAsync(organiser.Client, eventId);

        var response = await _factory.CreateClient().PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryID = categoryId });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanViewAllEnrolmentsForOwnEvent()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var runnerOne = await _factory.CreateUserAsync(Roles.Participant);
        var runnerTwo = await _factory.CreateUserAsync(Roles.Participant);
        var eventId = await _factory.CreateEventAsync(organiser.Client);
        var categoryId = await _factory.CreateCategoryAsync(organiser.Client, eventId);
        await _factory.EnrolAsync(runnerOne.Client, eventId, categoryId);
        await _factory.EnrolAsync(runnerTwo.Client, eventId, categoryId);

        var response = await organiser.Client.GetAsync($"/api/events/{eventId}/enrolments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<EventEnrolmentResponse>>();
        Assert.Equal(2, list!.Count);
        Assert.Contains(list, e => e.ParticipantId == runnerOne.UserId);
        Assert.Contains(list, e => e.ParticipantId == runnerTwo.UserId);
    }

    [Fact]
    public async Task Participant_CannotViewEventEnrolments_Returns403()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var participant = await _factory.CreateUserAsync(Roles.Participant);
        var eventId = await _factory.CreateEventAsync(organiser.Client);

        var response = await participant.Client.GetAsync($"/api/events/{eventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotViewEnrolmentsOfAnotherOrganisersEvent_Returns403()
    {
        var owner = await _factory.CreateUserAsync(Roles.Organiser);
        var other = await _factory.CreateUserAsync(Roles.Organiser);
        var eventId = await _factory.CreateEventAsync(owner.Client);

        var response = await other.Client.GetAsync($"/api/events/{eventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CanViewOwnEnrolments_ButNotSomeoneElses()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var alice = await _factory.CreateUserAsync(Roles.Participant);
        var bob = await _factory.CreateUserAsync(Roles.Participant);
        var eventId = await _factory.CreateEventAsync(organiser.Client);
        var categoryId = await _factory.CreateCategoryAsync(organiser.Client, eventId);
        await _factory.EnrolAsync(alice.Client, eventId, categoryId);

        var own = await alice.Client.GetAsync($"/api/participants/{alice.UserId}/enrolments");
        var others = await alice.Client.GetAsync($"/api/participants/{bob.UserId}/enrolments");

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        var list = await own.Content.ReadFromJsonAsync<List<MyEnrolmentResponse>>();
        Assert.Single(list!);
        Assert.Equal(eventId, list![0].EventId);
        Assert.Equal(HttpStatusCode.Forbidden, others.StatusCode);
    }

    [Fact]
    public async Task Participant_CanCancelOwnEnrolment_ButNotSomeoneElses()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var alice = await _factory.CreateUserAsync(Roles.Participant);
        var bob = await _factory.CreateUserAsync(Roles.Participant);
        var eventId = await _factory.CreateEventAsync(organiser.Client);
        var categoryId = await _factory.CreateCategoryAsync(organiser.Client, eventId);
        var aliceEnrolment = await _factory.EnrolAsync(alice.Client, eventId, categoryId);

        var byBob = await bob.Client.DeleteAsync($"/api/enrolments/{aliceEnrolment}");
        var byAlice = await alice.Client.DeleteAsync($"/api/enrolments/{aliceEnrolment}");

        Assert.Equal(HttpStatusCode.Forbidden, byBob.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byAlice.StatusCode);
    }
}
