using DynamicFormBuilder.Models;
using DynamicFormBuilder.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthRepository _repo;
    private readonly IConfiguration _configuration;

    public AuthController(AuthRepository repo, IConfiguration configuration)
    {
        _repo = repo;
        _configuration = configuration;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest("Email is required.");

        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Password is required.");

        var existingUser = await _repo.GetByEmailAsync(request.Email.Trim().ToLowerInvariant());
        if (existingUser != null)
            return BadRequest("Email is already registered.");

        var user = new AuthDefinition
        {
            Email = request.Email.Trim().ToLowerInvariant(),
            FullName = request.FullName?.Trim() ?? string.Empty,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        await _repo.CreateAsync(user);

        var token = GenerateJwtToken(user);

        return Ok(new AuthResponse
        {
            Token = token,
            Email = user.Email,
            FullName = user.FullName
        });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Email and password are required.");

        var user = await _repo.GetByEmailAsync(request.Email.Trim().ToLowerInvariant());

        if (user == null)
            return Unauthorized("Invalid credentials.");

        var validPassword = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        if (!validPassword)
            return Unauthorized("Invalid credentials.");

        var token = GenerateJwtToken(user);

        return Ok(new AuthResponse
        {
            Token = token,
            Email = user.Email,
            FullName = user.FullName
        });
    }

    private string GenerateJwtToken(AuthDefinition user)
    {
        var jwtKey = _configuration["Jwt:Key"]
                     ?? throw new InvalidOperationException("JWT key is missing.");

        var jwtIssuer = _configuration["Jwt:Issuer"]
                        ?? throw new InvalidOperationException("JWT issuer is missing.");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("fullName", user.FullName)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtIssuer,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}