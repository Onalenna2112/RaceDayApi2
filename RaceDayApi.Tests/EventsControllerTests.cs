using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayApi.Controllers;
using RaceDayApi.Data;
using RaceDayApi.Models;
using RaceDayApi.Tests.TestSupport;

namespace RaceDayApi.Tests;

/// <summary>Unit tests for EventsController, calling the controller directly as a chosen logged-in user.</summary>
public class EventsControllerTests
{
    private static RaceDayDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<RaceDayDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new RaceDayDbContext(options);
    }

    private static Organiser AddOrganiser(RaceDayDbContext context, string email = "boss@example.com")
    {
        var organiser = new Organiser { FullName = "Boss", Email = email, PasswordHash = "x" };
        context.Organisers.Add(organiser);
        context.SaveChanges();
        return organiser;
    }

    private static Event AddEvent(RaceDayDbContext context, int organiserId, int daysFromNow = 30)
    {
        var ev = new Event
        {
            OrganiserID = organiserId,
            Name = "City Run",
            Description = "10km",
            EventDate = DateTime.UtcNow.AddDays(daysFromNow),
            Location = "Pretoria",
            Distance = 10,
            EventType = "Run"
        };
        context.Events.Add(ev);
        context.SaveChanges();
        return ev;
    }

    private static EventDto ValidDto() => new()
    {
        Name = "Night Ride",
        Description = "Evening cycle",
        EventDate = DateTime.UtcNow.AddDays(10),
        Location = "Centurion",
        Distance = 40,
        EventType = "Cycle"
    };

    [Fact]
    public async Task GetEvents_ReturnsAllEvents()
    {
        using var context = CreateContext();
        var organiser = AddOrganiser(context);
        AddEvent(context, organiser.OrganiserID);
        AddEvent(context, organiser.OrganiserID, 60);

        var controller = new EventsController(context).WithUser(1, Roles.Participant);
        var result = await controller.GetEvents();

        var ok = Assert.IsType<OkObjectResult>(result);
        var events = Assert.IsType<List<EventSummaryResponse>>(ok.Value);
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public async Task GetEvent_UnknownId_ReturnsNotFound()
    {
        using var context = CreateContext();
        var controller = new EventsController(context).WithUser(1, Roles.Participant);

        var result = await controller.GetEvent(999);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task CreateEvent_AsOrganiser_Returns201_AndOwnsTheEvent()
    {
        using var context = CreateContext();
        var organiser = AddOrganiser(context);
        var controller = new EventsController(context).WithUser(organiser.OrganiserID, Roles.Organiser);

        var result = await controller.CreateEvent(ValidDto());

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, created.StatusCode);
        var saved = Assert.Single(context.Events);
        Assert.Equal(organiser.OrganiserID, saved.OrganiserID); // taken from the token, not from the request
    }

    [Fact]
    public async Task CreateEvent_WithPastDate_ReturnsBadRequest()
    {
        using var context = CreateContext();
        var organiser = AddOrganiser(context);
        var controller = new EventsController(context).WithUser(organiser.OrganiserID, Roles.Organiser);

        var dto = ValidDto();
        dto.EventDate = DateTime.UtcNow.AddDays(-1);

        var result = await controller.CreateEvent(dto);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(context.Events);
    }

    [Fact]
    public async Task UpdateEvent_ByOwner_UpdatesTheEvent()
    {
        using var context = CreateContext();
        var organiser = AddOrganiser(context);
        var ev = AddEvent(context, organiser.OrganiserID);
        var controller = new EventsController(context).WithUser(organiser.OrganiserID, Roles.Organiser);

        var dto = ValidDto();
        dto.Name = "Renamed Event";
        var result = await controller.UpdateEvent(ev.EventID, dto);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Renamed Event", context.Events.Single().Name);
    }

    [Fact]
    public async Task UpdateEvent_ByAnotherOrganiser_Returns403()
    {
        using var context = CreateContext();
        var owner = AddOrganiser(context, "owner@example.com");
        var other = AddOrganiser(context, "other@example.com");
        var ev = AddEvent(context, owner.OrganiserID);
        var controller = new EventsController(context).WithUser(other.OrganiserID, Roles.Organiser);

        var result = await controller.UpdateEvent(ev.EventID, ValidDto());

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, forbidden.StatusCode);
        Assert.Equal("City Run", context.Events.Single().Name); // unchanged
    }

    [Fact]
    public async Task DeleteEvent_WithEnrolments_ReturnsConflict()
    {
        using var context = CreateContext();
        var organiser = AddOrganiser(context);
        var ev = AddEvent(context, organiser.OrganiserID);
        context.Enrollments.Add(new Enrollment { EventID = ev.EventID, ParticipantID = 1, CategoryID = 1 });
        context.SaveChanges();
        var controller = new EventsController(context).WithUser(organiser.OrganiserID, Roles.Organiser);

        var result = await controller.DeleteEvent(ev.EventID);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Single(context.Events);
    }

    [Fact]
    public async Task DeleteEvent_ByOwner_RemovesTheEvent()
    {
        using var context = CreateContext();
        var organiser = AddOrganiser(context);
        var ev = AddEvent(context, organiser.OrganiserID);
        var controller = new EventsController(context).WithUser(organiser.OrganiserID, Roles.Organiser);

        var result = await controller.DeleteEvent(ev.EventID);

        Assert.IsType<OkObjectResult>(result);
        Assert.Empty(context.Events);
    }
}
