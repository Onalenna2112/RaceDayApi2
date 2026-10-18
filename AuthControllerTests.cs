using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RaceDayApi.Controllers;
using RaceDayApi.Data;
using RaceDayApi.Models;
using RaceDayApi.Services;

namespace RaceDayApi.Tests;

/// <summary>Unit tests for AuthController (registration and login), calling the controller directly.</summary>
public class AuthControllerTests
{
    private const string OrganiserCode = "TEST-ORGANISER-CODE";

    private static RaceDayDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<RaceDayDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new RaceDayDbContext(options);
    }

    private static IConfiguration CreateConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "Unit-Test-Secret-Key-That-Is-Long-Enough-123456",
                ["Jwt:Issuer"] = "RaceDayApi",
                ["Jwt:Audience"] = "RaceDayApiUsers",
                ["Jwt:ExpiryMinutes"] = "60",
                ["Auth:OrganiserRegistrationCode"] = OrganiserCode
            })
            .Build();

    private static AuthController CreateController(RaceDayDbContext context)
    {
        var config = CreateConfig();
        return new AuthController(context, new TokenService(config), config);
    }

    private static RegisterDto NewParticipant(string email = "runner@example.com") => new()
    {
        FullName = "Test Runner",
        Email = email,
        Password = "Password123!",
        Role = Roles.Participant
    };

    [Fact]
    public async Task Register_NewParticipant_Returns201_AndStoresHashedPassword()
    {
        using var context = CreateContext();
        var controller = CreateController(context);

        var result = await controller.Register(NewParticipant());

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, created.StatusCode);

        var saved = Assert.Single(context.Participants);
        Assert.NotEqual("Password123!", saved.PasswordHash); // never stored as plain text
        Assert.False(string.IsNullOrWhiteSpace(saved.PasswordHash));
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        using var context = CreateContext();
        var controller = CreateController(context);

        await controller.Register(NewParticipant());
        var second = await controller.Register(NewParticipant());

        Assert.IsType<ConflictObjectResult>(second);
        Assert.Single(context.Participants);
    }

    [Fact]
    public async Task Register_UnknownRole_ReturnsBadRequest()
    {
        using var context = CreateContext();
        var controller = CreateController(context);

        var dto = NewParticipant();
        dto.Role = "Admin";

        var result = await controller.Register(dto);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(context.Participants);
        Assert.Empty(context.Organisers);
    }

    [Fact]
    public async Task Register_Organiser_WithoutCode_Returns403_AndCreatesNothing()
    {
        using var context = CreateContext();
        var controller = CreateController(context);

        var dto = NewParticipant("boss@example.com");
        dto.Role = Roles.Organiser;

        var result = await controller.Register(dto);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, forbidden.StatusCode);
        Assert.Empty(context.Organisers);
    }

    [Fact]
    public async Task Register_Organiser_WithCorrectCode_Returns201()
    {
        using var context = CreateContext();
        var controller = CreateController(context);

        var dto = NewParticipant("boss@example.com");
        dto.Role = Roles.Organiser;
        dto.OrganiserCode = OrganiserCode;

        var result = await controller.Register(dto);

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, created.StatusCode);
        Assert.Single(context.Organisers);
    }

    [Fact]
    public async Task Login_CorrectCredentials_ReturnsTokenAndRole()
    {
        using var context = CreateContext();
        var controller = CreateController(context);
        await controller.Register(NewParticipant());

        var result = await controller.Login(new LoginDto { Email = "runner@example.com", Password = "Password123!" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<LoginResponse>(ok.Value);
        Assert.False(string.IsNullOrWhiteSpace(body.Token));
        Assert.Equal(Roles.Participant, body.Role);
    }

    [Fact]
    public async Task Login_Organiser_ReturnsOrganiserRole()
    {
        using var context = CreateContext();
        var controller = CreateController(context);

        var dto = NewParticipant("boss@example.com");
        dto.Role = Roles.Organiser;
        dto.OrganiserCode = OrganiserCode;
        await controller.Register(dto);

        var result = await controller.Login(new LoginDto { Email = "boss@example.com", Password = "Password123!" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<LoginResponse>(ok.Value);
        Assert.Equal(Roles.Organiser, body.Role);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        using var context = CreateContext();
        var controller = CreateController(context);
        await controller.Register(NewParticipant());

        var result = await controller.Login(new LoginDto { Email = "runner@example.com", Password = "WrongPassword!" });

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsUnauthorized()
    {
        using var context = CreateContext();
        var controller = CreateController(context);

        var result = await controller.Login(new LoginDto { Email = "nobody@example.com", Password = "Password123!" });

        Assert.IsType<UnauthorizedObjectResult>(result);
    }
}