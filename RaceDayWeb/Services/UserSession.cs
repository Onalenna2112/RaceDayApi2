namespace RaceDayWeb.Services
{
    /// <summary>The logged-in user, as stored in the session.</summary>
    public record SessionUser(string Token, int UserId, string Role, string FullName, string Email);

    /// <summary>
    /// Reads and writes the logged-in user in the ASP.NET Core session.
    /// The session holds the user id, the role and the API token for every page.
    /// </summary>
    public static class UserSession
    {
        private const string TokenKey = "Token";
        private const string UserIdKey = "UserId";
        private const string RoleKey = "Role";
        private const string NameKey = "FullName";
        private const string EmailKey = "Email";

        public static SessionUser? Get(HttpContext context)
        {
            var session = context.Session;
            var token = session.GetString(TokenKey);
            var role = session.GetString(RoleKey);
            var userId = session.GetInt32(UserIdKey);

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(role) || userId == null)
            {
                return null;
            }

            return new SessionUser(token, userId.Value, role, session.GetString(NameKey) ?? string.Empty, session.GetString(EmailKey) ?? string.Empty);
        }

        public static void Set(HttpContext context, SessionUser user)
        {
            var session = context.Session;
            session.SetString(TokenKey, user.Token);
            session.SetInt32(UserIdKey, user.UserId);
            session.SetString(RoleKey, user.Role);
            session.SetString(NameKey, user.FullName);
            session.SetString(EmailKey, user.Email);
        }

        public static void Clear(HttpContext context)
        {
            context.Session.Clear();
        }
    }
}
