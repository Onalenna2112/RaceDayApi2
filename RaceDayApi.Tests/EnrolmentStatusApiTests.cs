using System.Net;
using System.Net.Http.Json;
using RaceDayApi.Models;
using RaceDayApi.Tests.TestSupport;

namespace RaceDayApi.Tests;

/// <summary>HTTP tests for changing the status of an enrolment (Pending, Confirmed, Cancelled).</summary>
public class EnrolmentStatusApiTests : IClassFixture<RaceDayWebFactory>
{
    private readonly RaceDayWebFactory _factory;

    public EnrolmentStatusApiTests(RaceDayWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task NewEnrolment_StartsAsConfirmed()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var participant = await _factory.CreateUserAsync(Roles.Participant);
        var eventId = await _factory.CreateEventAsync(organiser.Client);
        var categoryId = await _factory.CreateCategoryAsync(organiser.Client, eventId);
        await _factory.EnrolAsync(participant.Client, eventId, categoryId);

        var mine = await participant.Client.GetFromJsonAsync<List<MyEnrolmentResponse>>($"/api/participants/{participant.UserId}/enrolments");

        Assert.Equal("Confirmed", Assert.Single(mine!).Status);
    }

    [Fact]
    public async Task Organiser_CanSetStatusToPending_AndParticipantSeesIt()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var participant = await _factory.CreateUserAsync(Roles.Participant);
        var eventId = await _factory.CreateEventAsync(organiser.Client);
        var categoryId = await _factory.CreateCategoryAsync(organiser.Client, eventId);
        var enrolmentId = await _factory.EnrolAsync(participant.Client, eventId, categoryId);

        var response = await organiser.Client.PutAsJsonAsync($"/api/enrolments/{enrolmentId}/status", new { status = "pending" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var mine = await participant.Client.GetFromJsonAsync<List<MyEnrolmentResponse>>($"/api/participants/{participant.UserId}/enrolments");
        Assert.Equal("Pending", Assert.Single(mine!).Status);
    }

    [Fact]
    public async Task Participant_CannotChangeStatus_Returns403()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var participant = await _factory.CreateUserAsync(Roles.Participant);
        var eventId = await _factory.CreateEventAsync(organiser.Client);
        var categoryId = await _factory.CreateCategoryAsync(organiser.Client, eventId);
        var enrolmentId = await _factory.EnrolAsync(participant.Client, eventId, categoryId);

        var response = await participant.Client.PutAsJsonAsync($"/api/enrolments/{enrolmentId}/status", new { status = "Cancelled" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotChangeStatusForAnotherOrganisersEvent_Returns403()
    {
        var owner = await _factory.CreateUserAsync(Roles.Organiser);
        var other = await _factory.CreateUserAsync(Roles.Organiser);
        var participant = await _factory.CreateUserAsync(Roles.Participant);
        var eventId = await _factory.CreateEventAsync(owner.Client);
        var categoryId = await _factory.CreateCategoryAsync(owner.Client, eventId);
        var enrolmentId = await _factory.EnrolAsync(participant.Client, eventId, categoryId);

        var response = await other.Client.PutAsJsonAsync($"/api/enrolments/{enrolmentId}/status", new { status = "Cancelled" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnknownStatus_Returns400()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var participant = await _factory.CreateUserAsync(Roles.Participant);
        var eventId = await _factory.CreateEventAsync(organiser.Client);
        var categoryId = await _factory.CreateCategoryAsync(organiser.Client, eventId);
        var enrolmentId = await _factory.EnrolAsync(participant.Client, eventId, categoryId);

        var response = await organiser.Client.PutAsJsonAsync($"/api/enrolments/{enrolmentId}/status", new { status = "Maybe" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnknownEnrolment_Returns404()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);

        var response = await organiser.Client.PutAsJsonAsync("/api/enrolments/999999/status", new { status = "Pending" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
