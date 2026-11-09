using Microsoft.AspNetCore.Mvc;
using RaceDayWeb.Models;
using RaceDayWeb.Services;

namespace RaceDayWeb.Controllers
{
    /// <summary>Shared helpers for all controllers: current user, flash messages and role-based redirects.</summary>
    public abstract class AppController : Controller
    {
        protected AppController(IRaceDayApiClient api)
        {
            Api = api;
        }

        protected IRaceDayApiClient Api { get; }

        protected SessionUser? CurrentUser => UserSession.Get(HttpContext);

        protected void FlashSuccess(string message) => TempData["Success"] = message;

        protected void FlashWarning(string message) => TempData["Warning"] = message;

        protected void FlashError(string message) => TempData["Error"] = message;

        /// <summary>Ends the session and sends the user back to the login page with a clear message.</summary>
        protected IActionResult SessionExpired()
        {
            UserSession.Clear(HttpContext);
            FlashWarning("Your session has expired. Please log in again.");
            return RedirectToAction("Login", "Account");
        }

        /// <summary>Sends each role to its own home page.</summary>
        protected IActionResult RedirectToRoleHome()
        {
            var user = CurrentUser;

            if (user?.Role == Roles.Organiser) return RedirectToAction("Dashboard", "Organiser");
            if (user?.Role == Roles.Participant) return RedirectToAction("Index", "Events");

            return RedirectToAction("Index", "Home");
        }
    }
}
