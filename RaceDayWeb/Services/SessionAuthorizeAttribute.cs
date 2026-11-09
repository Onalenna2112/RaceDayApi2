using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RaceDayWeb.Services
{
    /// <summary>
    /// Protects a controller or action. Visitors who are not logged in are sent to the login page,
    /// and users with the wrong role are sent to an "access denied" page.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class SessionAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        /// <summary>The role that is required. Leave empty to allow any logged-in user.</summary>
        public string? Role { get; set; }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = UserSession.Get(context.HttpContext);

            if (user == null)
            {
                var request = context.HttpContext.Request;
                string? returnUrl = HttpMethods.IsGet(request.Method) ? request.Path + request.QueryString : null;
                context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
                return;
            }

            if (!string.IsNullOrEmpty(Role) && !string.Equals(user.Role, Role, StringComparison.Ordinal))
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
            }
        }
    }
}
