using Microsoft.EntityFrameworkCore;
using RaceDayApi.Data;
using RaceDayApi.Models;

namespace RaceDayApi.Tests.TestSupport;

/// <summary>Small helpers that create an in-memory database and seed test rows.</summary>
public static class TestData
{
    public static RaceDayDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<RaceDayDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new RaceDayDbContext(options);
    }

    public static Organiser AddOrganiser(RaceDayDbContext context, string email = "organiser@example.com")
    {
        var organiser = new Organiser { FullName = "Test Organiser", Email = email, PasswordHash = "hash" };
        context.Organisers.Add(organiser);
        context.SaveChanges();
        return organiser;
    }

    public static Participant AddParticipant(RaceDayDbContext context, string email = "runner@example.com")
    {
        var participant = new Participant { FullName = "Test Runner", Email = email, PasswordHash = "hash" };
        context.Participants.Add(participant);
        context.SaveChanges();
        return participant;
    }

    public static Event AddEvent(RaceDayDbContext context, int organiserId, int daysFromNow = 30)
    {
        var ev = new Event
        {
            OrganiserID = organiserId,
            Name = "City Run",
            Description = "10km road race",
            EventDate = DateTime.UtcNow.AddDays(daysFromNow),
            Location = "Pretoria",
            Distance = 10,
            EventType = "Run"
        };
        context.Events.Add(ev);
        context.SaveChanges();
        return ev;
    }

    public static Category AddCategory(RaceDayDbContext context, int eventId, string name = "Senior 10km")
    {
        var category = new Category
        {
            EventID = eventId,
            Name = name,
            MinAge = 18,
            MaxAge = 99,
            Distance = 10,
            EntryFee = 150
        };
        context.Categories.Add(category);
        context.SaveChanges();
        return category;
    }

    public static Enrollment AddEnrollment(RaceDayDbContext context, int participantId, int eventId, int categoryId)
    {
        var enrollment = new Enrollment
        {
            ParticipantID = participantId,
            EventID = eventId,
            CategoryID = categoryId,
            EnrolmentDate = DateTime.UtcNow,
            Status = "Enrolled"
        };
        context.Enrollments.Add(enrollment);
        context.SaveChanges();
        return enrollment;
    }
}

