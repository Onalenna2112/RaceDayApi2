using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace RaceDayApi.Services
{
    public interface ITokenService
    {
        (string Token, DateTime ExpiresAt) CreateToken(int userId, string fullName, string email, string role);
    }

    /// <summary>Creates signed JWT tokens that carry the user's id and role.</summary>
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _config;

        public TokenService(IConfiguration config)
        {
            _config = config;
        }

        public (string Token, DateTime ExpiresAt) CreateToken(int userId, string fullName, string email, string role)
        {
            var key = _config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
            var minutes = int.TryParse(_config["Jwt:ExpiryMinutes"], out var m) ? m : 60;

            var claims = new[]
            {
                new Claim(AppClaims.UserId, userId.ToString()),
                new Claim(AppClaims.Name, fullName),
                new Claim(AppClaims.Email, email),
                new Claim(AppClaims.Role, role)
            };

            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256);

            var expires = DateTime.UtcNow.AddMinutes(minutes);

            var jwt = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: expires,
                signingCredentials: credentials);

            return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
        }
    }
}
