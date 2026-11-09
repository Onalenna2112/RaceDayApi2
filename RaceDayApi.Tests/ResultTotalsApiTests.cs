using System.Net.Http.Json;
using RaceDayApi.Models;
using RaceDayApi.Tests.TestSupport;

namespace RaceDayApi.Tests;

/// <summary>HTTP test for the personal race history: category name and total finishers (for example 2nd of 3).</summary>
public class ResultTotalsApiTests : IClassFixture<RaceDayWebFactory>
{
    private readonly RaceDayWebFactory _factory;

    public ResultTotalsApiTests(RaceDayWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MyResults_IncludesCategoryNameAndTotalFinishers()
    {
        var organiser = await _factory.CreateUserAsync(Roles.Organiser);
        var runnerOne = await _factory.CreateUserAsync(Roles.Participant);
        var runnerTwo = await _factory.CreateUserAsync(Roles.Participant);
        var runnerThree = await _factory.CreateUserAsync(Roles.Participant);
        var eventId = await _factory.CreateEventAsync(organiser.Client);
        var categoryId = await _factory.CreateCategoryAsync(organiser.Client, eventId);
        var enrolOne = await _factory.EnrolAsync(runnerOne.Client, eventId, categoryId);
        var enrolTwo = await _factory.EnrolAsync(runnerTwo.Client, eventId, categoryId);
        var enrolThree = await _factory.EnrolAsync(runnerThree.Client, eventId, categoryId);

        await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolOne}/results", new { finishTime = "00:40:00", position = 1 });
        await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolTwo}/results", new { finishTime = "00:45:00", position = 2 });
        await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolThree}/results", new { finishTime = "00:50:00", position = 3 });

        var results = await runnerTwo.Client.GetFromJsonAsync<List<MyResultResponse>>($"/api/participants/{runnerTwo.UserId}/results");

        var mine = Assert.Single(results!);
        Assert.Equal(2, mine.Position);
        Assert.Equal(3, mine.TotalFinishers);
        Assert.Equal("Senior 10km", mine.CategoryName);
    }
}
