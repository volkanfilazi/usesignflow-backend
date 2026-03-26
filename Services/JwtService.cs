using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DynamicFormBuilder.Services
{
    public class JwtService
    {
        private readonly IConfiguration _configuration;

        public JwtService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateAccessToken(AuthDefinition user)
        {
            var jwtKey = _configuration["Jwt:Key"]
                         ?? throw new InvalidOperationException("JWT key is missing.");

            var jwtIssuer = _configuration["Jwt:Issuer"]
                            ?? throw new InvalidOperationException("JWT issuer is missing.");

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (string.IsNullOrWhiteSpace(user.Id))
                throw new ArgumentException("User Id is required");

            if (string.IsNullOrWhiteSpace(user.Email))
                throw new ArgumentException("User Email is required");

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("emailVerified", user.EmailVerified.ToString()),
                new Claim("name", user.FullName ?? string.Empty),
                new Claim("twoFactorEnabled", user.TwoFactorEnabled.ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtIssuer,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateTwoFactorToken(AuthDefinition user)
        {
            var jwtKey = _configuration["Jwt:Key"]
                         ?? throw new InvalidOperationException("JWT key is missing.");

            var jwtIssuer = _configuration["Jwt:Issuer"]
                            ?? throw new InvalidOperationException("JWT issuer is missing.");

            if (string.IsNullOrWhiteSpace(user.Id))
                throw new ArgumentException("User Id is required");

            if (string.IsNullOrWhiteSpace(user.Email))
                throw new ArgumentException("User Email is required");

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("token_type", "2fa")
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtIssuer,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public ClaimsPrincipal? ValidateTwoFactorToken(string token)
        {
            var jwtKey = _configuration["Jwt:Key"]
                         ?? throw new InvalidOperationException("JWT key is missing");

            var jwtIssuer = _configuration["Jwt:Issuer"]
                            ?? throw new InvalidOperationException("JWT issuer is missing");

            var tokenHandler = new JwtSecurityTokenHandler();

            try
            {
                var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtIssuer,
                    ValidAudience = jwtIssuer,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    ClockSkew = TimeSpan.Zero
                }, out _);

                if (principal.FindFirst("token_type")?.Value != "2fa")
                    return null;

                return principal;
            }
            catch
            {
                return null;
            }
        }
    }
}