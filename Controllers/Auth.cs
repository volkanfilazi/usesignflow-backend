using DynamicFormBuilder.Models;
using DynamicFormBuilder.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthRepository _repo;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _emailService;

    public AuthController(
        AuthRepository repo,
        IConfiguration configuration,
        IEmailService emailService)
    {
        _repo = repo;
        _configuration = configuration;
        _emailService = emailService;
    }

    [HttpPost("register")]
    public async Task<ActionResult> Register([FromBody] RegisterUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest("Email is required.");

        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Password is required.");

        var email = request.Email.Trim().ToLowerInvariant();

        var existingUser = await _repo.GetByEmailAsync(email);
        if (existingUser != null)
            return BadRequest("Email is already registered.");

        var verifyToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        var user = new AuthDefinition
        {
            Email = email,
            FullName = request.FullName?.Trim() ?? string.Empty,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            EmailVerified = false,
            EmailVerificationToken = verifyToken,
            EmailVerificationTokenExpiresAtUtc = DateTime.UtcNow.AddHours(24)
        };

        await _repo.CreateAsync(user);
        var apiBaseUrl = _configuration["App:FrontendBaseUrl"]?.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(apiBaseUrl))
            throw new InvalidOperationException("App:FrontendBaseUrl is missing.");
        var verifyUrl =
    $"{_configuration["App:FrontendBaseUrl"]}/verification-process?token={Uri.EscapeDataString(verifyToken)}&email={Uri.EscapeDataString(user.Email)}";

        try
        {
            await _emailService.SendVerificationEmailAsync(user.Email, verifyUrl, user.FullName);
        }
        catch (Exception ex)
        {
            // logla
            Console.WriteLine(ex.Message);

            return StatusCode(500, new
            {
                message = "User created, but verification email could not be sent."
            });
        }

        return Ok(new
        {
            message = "Registration successful. Please verify your email address."
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

        if (!user.EmailVerified)
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Please verify your email before logging in."
            });

        var token = GenerateJwtToken(user);

        return Ok(new AuthResponse
        {
            Token = token,
            Email = user.Email,
            FullName = user.FullName
        });
    }

    [HttpGet("verify-email")]
    public async Task<ActionResult> VerifyEmail([FromQuery] string email, [FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            return BadRequest("Invalid verification request.");

        var user = await _repo.GetByEmailAsync(email.Trim().ToLowerInvariant());
        if (user == null)
            return BadRequest("User not found.");

        if (user.EmailVerified)
            return Ok(new { message = "Email is already verified." });

        if (user.EmailVerificationToken != token)
            return BadRequest("Invalid verification token.");

        if (user.EmailVerificationTokenExpiresAtUtc == null ||
            user.EmailVerificationTokenExpiresAtUtc < DateTime.UtcNow)
            return BadRequest("Verification token has expired.");

        user.EmailVerified = true;
        user.EmailVerificationToken = null;
        user.EmailVerificationTokenExpiresAtUtc = null;

        await _repo.UpdateAsync(user);

        return Ok(new { message = "Email verified successfully." });
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