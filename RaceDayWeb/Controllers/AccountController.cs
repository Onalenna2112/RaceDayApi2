using Microsoft.AspNetCore.Mvc;
using RaceDayWeb.Models;
using RaceDayWeb.Services;

namespace RaceDayWeb.Controllers
{
    /// <summary>Register, log in and log out. All of it goes through the Part 2 API.</summary>
    public class AccountController : AppController
    {
        public AccountController(IRaceDayApiClient api) : base(api)
        {
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (CurrentUser != null) return RedirectToRoleHome();
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var result = await Api.LoginAsync(model.Email, model.Password);
            if (!result.Success || result.Data == null)
            {
                ModelState.AddModelError(string.Empty, result.Error ?? "We could not log you in. Please try again.");
                return View(model);
            }

            var login = result.Data;

            // Start a fresh session, then keep the user's id, role and token in it.
            UserSession.Clear(HttpContext);
            UserSession.Set(HttpContext, new SessionUser(login.Token, login.UserId, login.Role, login.FullName, login.Email));

            FlashSuccess($"Welcome back, {login.FullName}!");

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return LocalRedirect(model.ReturnUrl);
            }

            return RedirectToRoleHome();
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (CurrentUser != null) return RedirectToRoleHome();
            return View(new RegisterViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (model.Role == Roles.Organiser && string.IsNullOrWhiteSpace(model.OrganiserCode))
            {
                ModelState.AddModelError(nameof(model.OrganiserCode), "Organisers need the organiser code to register.");
            }

            if (!ModelState.IsValid) return View(model);

            var request = new RegisterRequest(
                model.FullName.Trim(),
                model.Email.Trim(),
                model.Password,
                model.Role,
                model.Role == Roles.Organiser ? model.OrganiserCode?.Trim() : null);

            var result = await Api.RegisterAsync(request);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Error ?? "We could not create your account. Please try again.");
                return View(model);
            }

            FlashSuccess("Your account has been created. Please log in.");
            return RedirectToAction(nameof(Login));
        }

        /// <summary>Ends the session (user id, role and token) and returns to the login page.</summary>
        [HttpPost]
        public IActionResult Logout()
        {
            UserSession.Clear(HttpContext);
            FlashSuccess("You have been logged out.");
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
