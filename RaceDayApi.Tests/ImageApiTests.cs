using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RaceDayApi.Models;
using RaceDayApi.Tests.TestSupport;

namespace RaceDayApi.Tests;

/// <summary>HTTP tests for image uploads (event banners and profile pictures). Azure is replaced by a fake.</summary>
public class ImageApiTests : IClassFixture<RaceDayWebFactory>
{
    private readonly RaceDayWebFactory _factory;

    public ImageApiTests(RaceDayWebFactory factory)
    {
        _factory = factory;
    }

    private static MultipartFormDataContent Upload(byte[] bytes, string fileName, string contentType)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var form = new MultipartFormDataContent();
        form.Add(file, "file", fileName);
        return form;
    }

    private static MultipartFormDataContent ValidPng() => Upload(
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0 },
        "banner.png",
        "image/png");

    [Fact]
    public async Task Organiser_CanUploadBanner_AndTheUrlIsSavedOnTheEvent()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var participant = await _factory.CreateUserAsync(Roles.Participant);
        var eventId = await _factory.CreateEventAsync(organiser.Client);

        var response = await organiser.Client.PostAsync($"/api/events/{eventId}/banner", ValidPng());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ImageUploadResponse>();
        Assert.StartsWith(FakeBlobStorageService.BaseUrl, body!.ImageUrl);

        var detail = await participant.Client.GetFromJsonAsync<EventDetailResponse>($"/api/events/{eventId}");
        Assert.Equal(body.ImageUrl, detail!.BannerImageUrl);
    }

    [Fact]
    public async Task Participant_CannotUploadBanner_Returns403()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var participant = await _factory.CreateUserAsync(Roles.Participant);
        var eventId = await _factory.CreateEventAsync(organiser.Client);

        var response = await participant.Client.PostAsync($"/api/events/{eventId}/banner", ValidPng());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotUploadBannerForAnotherOrganisersEvent_Returns403()
    {
        var owner = await _factory.CreateUserAsync(Roles.Organiser);
        var other = await _factory.CreateUserAsync(Roles.Organiser);
        var eventId = await _factory.CreateEventAsync(owner.Client);

        var response = await other.Client.PostAsync($"/api/events/{eventId}/banner", ValidPng());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotUploadBanner_Returns401()
    {
        var response = await _factory.CreateClient().PostAsync("/api/events/1/banner", ValidPng());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_WithATextFile_Returns400()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var eventId = await _factory.CreateEventAsync(organiser.Client);

        var response = await organiser.Client.PostAsync(
            $"/api/events/{eventId}/banner",
            Upload(System.Text.Encoding.UTF8.GetBytes("hello world, not an image"), "notes.txt", "text/plain"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_WithAFakeImage_Returns400()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var eventId = await _factory.CreateEventAsync(organiser.Client);

        // The name says .png but the bytes are plain text.
        var response = await organiser.Client.PostAsync(
            $"/api/events/{eventId}/banner",
            Upload(System.Text.Encoding.UTF8.GetBytes("this is plain text pretending to be a png"), "fake.png", "image/png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_ForUnknownEvent_Returns404()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);

        var response = await organiser.Client.PostAsync("/api/events/999999/banner", ValidPng());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CanUploadOwnProfilePicture_AndItShowsOnTheProfile()
    {
        var participant = await _factory.CreateUserAsync(Roles.Participant);

        var response = await participant.Client.PostAsync($"/api/users/{participant.UserId}/profile-picture", ValidPng());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ImageUploadResponse>();

        var profile = await participant.Client.GetFromJsonAsync<ProfileResponse>($"/api/users/{participant.UserId}");
        Assert.Equal(body!.ImageUrl, profile!.ProfilePictureUrl);
    }

    [Fact]
    public async Task Participant_CannotUploadPictureForSomeoneElse_Returns403()
    {
        var alice = await _factory.CreateUserAsync(Roles.Participant);
        var bob = await _factory.CreateUserAsync(Roles.Participant);

        var response = await alice.Client.PostAsync($"/api/users/{bob.UserId}/profile-picture", ValidPng());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotUploadAProfilePicture_Returns403()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);

        var response = await organiser.Client.PostAsync($"/api/users/{organiser.UserId}/profile-picture", ValidPng());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
