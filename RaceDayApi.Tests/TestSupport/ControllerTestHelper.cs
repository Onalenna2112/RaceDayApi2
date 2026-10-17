using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RaceDayApi.Services;

namespace RaceDayApi.Tests.TestSupport;

public static class ControllerTestHelper
{
    /// <summary>Makes the controller behave as if this user (id + role) is logged in.</summary>
    public static T WithUser<T>(this T controller, int userId, string role) where T : ControllerBase
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(AppClaims.UserId, userId.ToString()),
                new Claim(AppClaims.Role, role)
            },
            authenticationType: "TestAuth",
            nameType: AppClaims.Name,
            roleType: AppClaims.Role);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        return controller;
    }
}
