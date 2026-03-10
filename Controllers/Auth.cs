using DynamicFormBuilder.Models;
using DynamicFormBuilder.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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

    [EnableRateLimiting("auth-strict")]
    [HttpPost("register")]
    public async Task<ActionResult> Register(
    [FromBody] RegisterUserRequest request,
    [FromServices] ILegalDocumentService legalDocumentService
)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest("Email is required.");

        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Password is required.");

        if (string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest("Full name is required.");

        if (!request.TermsAccepted || !request.PrivacyAccepted)
            return BadRequest("Terms and Privacy Policy must be accepted.");

        var email = request.Email.Trim().ToLowerInvariant();

        var existingUser = await _repo.GetByEmailAsync(email);
        if (existingUser != null)
            return BadRequest("Email is already registered.");

        var terms = legalDocumentService.GetCurrentTerms();
        var privacy = legalDocumentService.GetCurrentPrivacy();

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers["User-Agent"].ToString();

        var acceptances = new List<LegalAcceptance>
    {
        new LegalAcceptance
        {
            Type = terms.Type,
            Version = terms.Version,
            Hash = terms.Hash,
            AcceptedAtUtc = DateTime.UtcNow,
            IpAddress = ipAddress,
            UserAgent = userAgent
        },
        new LegalAcceptance
        {
            Type = privacy.Type,
            Version = privacy.Version,
            Hash = privacy.Hash,
            AcceptedAtUtc = DateTime.UtcNow,
            IpAddress = ipAddress,
            UserAgent = userAgent
        }
    };

        var rawVerifyToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var verifyTokenHash = TokenHelper.ComputeSha256(rawVerifyToken);

        var user = new AuthDefinition
        {
            Email = email,
            FullName = request.FullName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            EmailVerified = false,
            EmailVerificationTokenHash = verifyTokenHash,
            EmailVerificationTokenExpiresAtUtc = DateTime.UtcNow.AddHours(24),
            LegalAcceptances = acceptances
        };

        await _repo.CreateAsync(user);

        var frontendBaseUrl = _configuration["App:FrontendBaseUrl"]?.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(frontendBaseUrl))
            throw new InvalidOperationException("App:FrontendBaseUrl is missing.");

        var verifyUrl =
            $"{frontendBaseUrl}/verification-process?token={Uri.EscapeDataString(rawVerifyToken)}&email={Uri.EscapeDataString(user.Email)}";
        
        try
        {
            await _emailService.SendVerificationEmailAsync(user.Email, verifyUrl, user.FullName);
        }
        catch (Exception ex)
        {
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

    [EnableRateLimiting("auth-strict")]
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

        if (user.EmailVerificationTokenHash != token)
            return BadRequest("Invalid verification token.");

        if (user.EmailVerificationTokenExpiresAtUtc == null ||
            user.EmailVerificationTokenExpiresAtUtc < DateTime.UtcNow)
            return BadRequest("Verification token has expired.");

        user.EmailVerified = true;
        user.EmailVerificationTokenHash = null;
        user.EmailVerificationTokenExpiresAtUtc = null;

        await _repo.UpdateAsync(user);

        return Ok(new { message = "Email verified successfully." });
    }

    [EnableRateLimiting("auth-strict")]
    [HttpPost("resend-verification")]
    public async Task<ActionResult> ResendVerificationEmail([FromBody] ResendVerificationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest("Email is required.");

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _repo.GetByEmailAsync(email);

        if (user == null)
            return Ok(new { message = "If the account exists and is not verified, a new verification email has been sent." });

        if (user.EmailVerified)
            return Ok(new { message = "Email is already verified." });

        var rawVerifyToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var verifyTokenHash = TokenHelper.ComputeSha256(rawVerifyToken);

        user.EmailVerificationTokenHash = verifyTokenHash;
        user.EmailVerificationTokenExpiresAtUtc = DateTime.UtcNow.AddHours(24);

        await _repo.UpdateAsync(user);

        var frontendBaseUrl = _configuration["App:FrontendBaseUrl"]?.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(frontendBaseUrl))
            throw new InvalidOperationException("App:FrontendBaseUrl is missing.");

        var verifyUrl =
            $"{frontendBaseUrl}/verification-process?token={Uri.EscapeDataString(rawVerifyToken)}&email={Uri.EscapeDataString(user.Email)}";

        await _emailService.SendVerificationEmailAsync(user.Email, verifyUrl, user.FullName);

        return Ok(new
        {
            message = "If the account exists and is not verified, a new verification email has been sent."
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