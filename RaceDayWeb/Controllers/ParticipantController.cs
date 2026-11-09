using Microsoft.AspNetCore.Mvc;
using RaceDayWeb.Models;
using RaceDayWeb.Services;

namespace RaceDayWeb.Controllers
{
    /// <summary>Participant pages: my events, my results and my profile.</summary>
    [SessionAuthorize(Role = Roles.Participant)]
    public class ParticipantController : AppController
    {
        public ParticipantController(IRaceDayApiClient api) : base(api)
        {
        }

        public async Task<IActionResult> MyEvents()
        {
            var result = await Api.GetMyEnrolmentsAsync(CurrentUser!.UserId);
            if (result.Unauthorized) return SessionExpired();

            if (!result.Success || result.Data == null)
            {
                FlashError(result.Error ?? "We could not load your events.");
                return View(new List<MyEnrolment>());
            }

            return View(result.Data.OrderBy(e => e.EventDate).ToList());
        }

        [HttpPost]
        public async Task<IActionResult> CancelEnrolment(int enrolmentId)
        {
            var result = await Api.CancelEnrolmentAsync(enrolmentId);
            if (result.Unauthorized) return SessionExpired();

            if (result.Success) FlashSuccess("Your enrolment has been cancelled.");
            else FlashError(result.Error ?? "We could not cancel that enrolment.");

            return RedirectToAction(nameof(MyEvents));
        }

        /// <summary>Personal race history: event, date, category, finish time and position (for example 47th of 312).</summary>
        public async Task<IActionResult> MyResults()
        {
            var result = await Api.GetMyResultsAsync(CurrentUser!.UserId);
            if (result.Unauthorized) return SessionExpired();

            if (!result.Success || result.Data == null)
            {
                FlashError(result.Error ?? "We could not load your results.");
                return View(new List<MyResult>());
            }

            return View(result.Data.OrderByDescending(r => r.EventDate).ToList());
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var result = await Api.GetProfileAsync(CurrentUser!.UserId);
            if (result.Unauthorized) return SessionExpired();

            if (!result.Success || result.Data == null)
            {
                FlashError(result.Error ?? "We could not load your profile.");
                return RedirectToRoleHome();
            }

            return View(new ProfileViewModel
            {
                FullName = result.Data.FullName,
                Email = result.Data.Email,
                ProfilePictureUrl = result.Data.ProfilePictureUrl
            });
        }

        [HttpPost]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            var user = CurrentUser!;

            if (!ModelState.IsValid) return View(model);

            var result = await Api.UpdateProfileAsync(user.UserId, model.FullName.Trim(), model.Email.Trim());
            if (result.Unauthorized) return SessionExpired();

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Error ?? "We could not update your profile.");
                return View(model);
            }

            // Keep the name in the menu up to date.
            UserSession.Set(HttpContext, user with { FullName = model.FullName.Trim(), Email = model.Email.Trim().ToLowerInvariant() });

            FlashSuccess("Your profile has been updated.");
            return RedirectToAction(nameof(Profile));
        }

        [HttpPost]
        public async Task<IActionResult> ProfilePicture(IFormFile? picture)
        {
            if (picture == null || picture.Length == 0)
            {
                FlashError("Please choose a picture to upload.");
                return RedirectToAction(nameof(Profile));
            }

            var result = await Api.UploadProfilePictureAsync(CurrentUser!.UserId, picture);
            if (result.Unauthorized) return SessionExpired();

            if (result.Success) FlashSuccess("Your profile picture has been updated.");
            else FlashError(result.Error ?? "We could not upload your picture.");

            return RedirectToAction(nameof(Profile));
        }
    }
}
