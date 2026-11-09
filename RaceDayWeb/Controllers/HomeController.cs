using Microsoft.AspNetCore.Mvc;
using RaceDayWeb.Services;

namespace RaceDayWeb.Controllers
{
    public class HomeController : AppController
    {
        public HomeController(IRaceDayApiClient api) : base(api)
        {
        }

        /// <summary>Public landing page. Logged-in users go straight to their own home page.</summary>
        public IActionResult Index()
        {
            if (CurrentUser != null) return RedirectToRoleHome();
            return View();
        }

        /// <summary>Friendly page shown when something unexpected goes wrong. No technical details are shown.</summary>
        [Route("Home/Error")]
        [IgnoreAntiforgeryToken]
        public IActionResult Error()
        {
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return View();
        }

        /// <summary>Friendly page for 404 and other status codes.</summary>
        [Route("Home/HttpStatus")]
        [IgnoreAntiforgeryToken]
        public IActionResult HttpStatus(int code = 404)
        {
            ViewData["Code"] = code;
            return View();
        }
    }
}
