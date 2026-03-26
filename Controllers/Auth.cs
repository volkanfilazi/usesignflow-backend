using DynamicFormBuilder.Models;
using DynamicFormBuilder.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Text.RegularExpressions;
using OtpNet;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using DynamicFormBuilder.Services.Billing;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthRepository _repo;
    private readonly ISubscriptionService _subscriptionService;
    private readonly GoogleAuthOptions _googleOptions;
    private readonly AuthService _authService;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _emailService;
    private readonly JwtService _jwtService;

    public AuthController(
        AuthRepository repo,
        ISubscriptionService subscriptionService,
        IOptions<GoogleAuthOptions> googleOptions,
        IConfiguration configuration,
        IEmailService emailService,
        AuthService authService,
        JwtService jwtService)
    {
        _repo = repo;
        _subscriptionService = subscriptionService;
        _googleOptions = googleOptions.Value;
        _authService = authService;
        _configuration = configuration;
        _emailService = emailService;
        _jwtService = jwtService;
    }

    [EnableRateLimiting("auth-strict")]
    [HttpPost("register")]
    public async Task<ActionResult> Register(
    [FromBody] RegisterUserRequest request,
    [FromServices] ILegalDocumentService legalDocumentService)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new
                {
                    code = "VALIDATION_ERROR",
                    message = "Email is required."
                });

            if (string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new
                {
                    code = "VALIDATION_ERROR",
                    message = "Password is required."
                });

            if (string.IsNullOrWhiteSpace(request.FullName))
                return BadRequest(new
                {
                    code = "VALIDATION_ERROR",
                    message = "Full name is required."
                });

            if (!request.TermsAccepted || !request.PrivacyAccepted)
            {
                return BadRequest(new
                {
                    code = "VALIDATION_ERROR",
                    message = "Terms and Privacy Policy must be accepted."
                });
            }

            var email = request.Email.Trim().ToLowerInvariant();
            var fullName = request.FullName.Trim();
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var existingUser = await _repo.GetByEmailAsync(email);

            if (existingUser != null && !existingUser.IsDeleted)
            {
                return BadRequest(new
                {
                    code = "EMAIL_ALREADY_REGISTERED",
                    message = "Email is already registered."
                });
            }

            var terms = legalDocumentService.GetCurrentTerms();
            var privacy = legalDocumentService.GetCurrentPrivacy();

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers["User-Agent"].ToString();
            var now = DateTime.UtcNow;

            var acceptances = new List<LegalAcceptance>
        {
            new LegalAcceptance
            {
                Type = terms.Type,
                Version = terms.Version,
                Hash = terms.Hash,
                AcceptedAtUtc = now,
                IpAddress = ipAddress,
                UserAgent = userAgent
            },
            new LegalAcceptance
            {
                Type = privacy.Type,
                Version = privacy.Version,
                Hash = privacy.Hash,
                AcceptedAtUtc = now,
                IpAddress = ipAddress,
                UserAgent = userAgent
            }
        };

            var rawVerifyToken = TokenHelper.GenerateSecureToken();
            var verifyTokenHash = TokenHelper.ComputeSha256(rawVerifyToken);

            AuthDefinition user;

            if (existingUser != null && existingUser.IsDeleted)
            {
                existingUser.Email = email;
                existingUser.FullName = fullName;
                existingUser.PasswordHash = passwordHash;
                existingUser.EmailVerified = false;
                existingUser.EmailVerificationTokenHash = verifyTokenHash;
                existingUser.EmailVerificationTokenExpiresAtUtc = now.AddHours(24);

                existingUser.IsDeleted = false;
                existingUser.DeletedAtUtc = null;
                existingUser.DeleteReason = null;
                existingUser.IsAnonymized = false;
                existingUser.UpdatedAtUtc = now;

                existingUser.LegalAcceptances = acceptances;
                existingUser.RefreshTokens = new List<RefreshTokenDefinition>();
                existingUser.ExternalLogins = new List<ExternalLogin>();

                existingUser.TwoFactorEnabled = false;
                existingUser.TwoFactorSecret = null;

                user = existingUser;
                await _repo.UpdateAsync(user);
            }
            else
            {
                user = new AuthDefinition
                {
                    Email = email,
                    FullName = fullName,
                    PasswordHash = passwordHash,
                    EmailVerified = false,
                    EmailVerificationTokenHash = verifyTokenHash,
                    EmailVerificationTokenExpiresAtUtc = now.AddHours(24),
                    LegalAcceptances = acceptances,
                    RefreshTokens = new List<RefreshTokenDefinition>(),
                    ExternalLogins = new List<ExternalLogin>(),
                    TwoFactorEnabled = false,
                    TwoFactorSecret = null,
                    IsDeleted = false,
                    DeletedAtUtc = null,
                    DeleteReason = null,
                    IsAnonymized = false,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };

                await _repo.CreateAsync(user);

                await _subscriptionService.GetOrCreateForUserAsync(user.Id);
            }

            var frontendBaseUrl = _configuration["App:FrontendBaseUrl"]?.TrimEnd('/');

            if (string.IsNullOrWhiteSpace(frontendBaseUrl))
                throw new InvalidOperationException("App:FrontendBaseUrl is missing.");

            var verifyUrl =
                $"{frontendBaseUrl}/verification-process?token={Uri.EscapeDataString(rawVerifyToken)}&email={Uri.EscapeDataString(user.Email)}";

            await _emailService.SendVerificationEmailAsync(user.Email, verifyUrl, user.FullName);

            return Ok(new
            {
                message = "Registration successful. Please verify your email address."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                code = "SERVER_ERROR",
                message = ex.Message,
                detail = ex.ToString()
            });
        }
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return BadRequest("User id not found.");
        }

        var user = await _repo.GetByIdAsync(userId);

        if (user == null)
            return Unauthorized();

        return Ok(new
        {
            email = user.Email,
            fullName = user.FullName,
            twoFactorEnabled = user.TwoFactorEnabled,
            notificationsEnabled = user.NotificationsEnabled
        });
    }

    [EnableRateLimiting("auth-strict")]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                code = "VALIDATION_ERROR",
                message = "Email and password are required."
            });
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var password = request.Password;

        var user = await _repo.GetByEmailAsync(email);

        if (user == null)
        {
            return Unauthorized(new
            {
                code = "INVALID_CREDENTIALS",
                message = "Invalid credentials."
            });
        }

        if (user.IsDeleted)
        {
            return Unauthorized(new
            {
                code = "ACCOUNT_DELETED",
                message = "This account is no longer available."
            });
        }

        if (!user.EmailVerified)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "EMAIL_NOT_VERIFIED",
                message = "Please verify your email before logging in."
            });
        }

        var hasPassword = !string.IsNullOrWhiteSpace(user.PasswordHash);
        var hasExternal = user.ExternalLogins?.Any() == true;

        if (!hasPassword)
        {
            if (hasExternal)
            {
                return Unauthorized(new
                {
                    code = "PASSWORD_LOGIN_NOT_AVAILABLE",
                    message = "This account cannot be used with password login."
                });
            }

            return Unauthorized(new
            {
                code = "INVALID_CREDENTIALS",
                message = "Invalid credentials."
            });
        }

        var validPassword = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        if (!validPassword)
        {
            return Unauthorized(new
            {
                code = "INVALID_CREDENTIALS",
                message = "Invalid credentials."
            });
        }

        await _subscriptionService.GetOrCreateForUserAsync(user.Id!);

        if (user.TwoFactorEnabled)
        {
            var twoFactorToken = _jwtService.GenerateTwoFactorToken(user);

            return Ok(new AuthResponse
            {
                RequiresTwoFactor = true,
                TwoFactorToken = twoFactorToken,
                Email = user.Email,
                FullName = user.FullName
            });
        }

        var response = await CreateAuthResponseAsync(user);
        return Ok(response);
    }

    [EnableRateLimiting("auth-strict")]
    [HttpPost("forgot-password")]
    public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new
                {
                    code = "VALIDATION_ERROR",
                    message = "Email is required."
                });
            }

            var email = request.Email.Trim().ToLowerInvariant();

            var user = await _repo.GetByEmailAsync(email);

            if (user == null || user.IsDeleted)
            {
                return Ok(new
                {
                    message = "If an account exists for this email, a password reset link has been sent."
                });
            }

            var now = DateTime.UtcNow;
            var rawResetToken = TokenHelper.GenerateSecureToken();
            var resetTokenHash = TokenHelper.ComputeSha256(rawResetToken);

            user.PasswordResetTokenHash = resetTokenHash;
            user.PasswordResetTokenExpiresAtUtc = now.AddHours(1);
            user.PasswordResetRequestedAtUtc = now;
            user.UpdatedAtUtc = now;

            await _repo.UpdateAsync(user);

            var frontendBaseUrl = _configuration["App:FrontendBaseUrl"]?.TrimEnd('/');

            if (string.IsNullOrWhiteSpace(frontendBaseUrl))
                throw new InvalidOperationException("App:FrontendBaseUrl is missing.");

            var resetUrl =
                $"{frontendBaseUrl}/reset-password?token={Uri.EscapeDataString(rawResetToken)}&email={Uri.EscapeDataString(user.Email)}";

            await _emailService.SendPasswordResetEmailAsync(user.Email, resetUrl, user.FullName);

            return Ok(new
            {
                message = "If an account exists for this email, a password reset link has been sent."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                code = "SERVER_ERROR",
                message = ex.Message,
                detail = ex.ToString()
            });
        }
    }

    [HttpPost("validate-reset-token")]
    public async Task<ActionResult> ValidateResetToken([FromBody] ValidateResetTokenRequest request)
    {
        var email = request.Email?.Trim().ToLowerInvariant();
        var token = request.Token?.Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new
            {
                code = "VALIDATION_ERROR",
                message = "Email and token are required."
            });
        }

        var user = await _repo.GetByEmailAsync(email);
        if (user == null || user.IsDeleted || string.IsNullOrWhiteSpace(user.PasswordResetTokenHash))
        {
            return BadRequest(new
            {
                code = "INVALID_RESET_REQUEST",
                message = "Invalid or expired password reset request."
            });
        }

        var tokenHash = TokenHelper.ComputeSha256(token);

        if (!string.Equals(user.PasswordResetTokenHash, tokenHash, StringComparison.Ordinal) ||
            !user.PasswordResetTokenExpiresAtUtc.HasValue ||
            user.PasswordResetTokenExpiresAtUtc.Value < DateTime.UtcNow)
        {
            return BadRequest(new
            {
                code = "INVALID_RESET_REQUEST",
                message = "Invalid or expired password reset request."
            });
        }

        return Ok(new
        {
            message = "Reset token is valid."
        });
    }

    [EnableRateLimiting("auth-strict")]
    [HttpPost("google")]
    public async Task<ActionResult<AuthResponse>> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Credential))
            return BadRequest("Credential can not be empty.");

        var user = await GetOrCreateGoogleUserAsync(request.Credential);

        if (user == null)
            return Unauthorized("Invalid Google credential.");

        if (user.IsDeleted)
            throw new UnauthorizedAccessException("This account has been deleted.");

        await _subscriptionService.GetOrCreateForUserAsync(user.Id!);

        if (user.TwoFactorEnabled)
        {
            var twoFactorToken = _jwtService.GenerateTwoFactorToken(user);

            return Ok(new AuthResponse
            {
                RequiresTwoFactor = true,
                TwoFactorToken = twoFactorToken,
                Email = user.Email,
                FullName = user.FullName
            });
        }

        var response = await CreateAuthResponseAsync(user);

        return Ok(response);
    }

    [EnableRateLimiting("auth-strict")]
    [HttpPost("google/redirect")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> GoogleRedirect([FromForm] string credential)
    {
        if (string.IsNullOrWhiteSpace(credential))
            return Redirect($"{_googleOptions.FrontendGoogleCallbackUrl}?error=missing_credential");

        AuthDefinition? user;
        try
        {
            user = await GetOrCreateGoogleUserAsync(credential);
        }
        catch
        {
            return Redirect($"{_googleOptions.FrontendGoogleCallbackUrl}?error=invalid_google_login");
        }

        if (user == null)
            return Redirect($"{_googleOptions.FrontendGoogleCallbackUrl}?error=invalid_google_login");

        if (user.IsDeleted)
            return Redirect($"{_googleOptions.FrontendGoogleCallbackUrl}?error=account_deleted");

        if (user.TwoFactorEnabled)
        {
            var twoFactorToken = _jwtService.GenerateTwoFactorToken(user);

            var twoFactorRedirect =
                $"{_googleOptions.FrontendGoogleCallbackUrl}" +
                $"?requiresTwoFactor=true" +
                $"&twoFactorToken={Uri.EscapeDataString(twoFactorToken)}" +
                $"&email={Uri.EscapeDataString(user.Email ?? string.Empty)}" +
                $"&fullName={Uri.EscapeDataString(user.FullName ?? string.Empty)}";

            return Redirect(twoFactorRedirect);
        }

        var response = await CreateAuthResponseAsync(user);

        var redirectUrl =
            $"{_googleOptions.FrontendGoogleCallbackUrl}" +
            $"?token={Uri.EscapeDataString(response.Token ?? string.Empty)}" +
            $"&refreshToken={Uri.EscapeDataString(response.RefreshToken ?? string.Empty)}" +
            $"&email={Uri.EscapeDataString(response.Email ?? string.Empty)}" +
            $"&fullName={Uri.EscapeDataString(response.FullName ?? string.Empty)}" +
            $"&requiresTwoFactor=false";

        return Redirect(redirectUrl);
    }

    private async Task<AuthResponse> CreateAuthResponseAsync(AuthDefinition user)
    {
        var accessToken = _jwtService.GenerateAccessToken(user);
        var accessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(30);

        var rawRefreshToken = TokenHelper.GenerateSecureToken();
        var refreshTokenHash = TokenHelper.ComputeSha256(rawRefreshToken);

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var now = DateTime.UtcNow;

        user.RefreshTokens ??= new List<RefreshTokenDefinition>();

        user.RefreshTokens = user.RefreshTokens
            .Where(x => x.RevokedAtUtc == null && x.ExpiresAtUtc > now)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(5)
            .ToList();

        var refreshToken = new RefreshTokenDefinition
        {
            TokenHash = refreshTokenHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(7),
            CreatedByIp = ipAddress
        };

        user.RefreshTokens.Add(refreshToken);
        await _repo.UpdateAsync(user);

        return new AuthResponse
        {
            Token = accessToken,
            TokenExpiresAtUtc = accessTokenExpiresAtUtc,
            RefreshToken = rawRefreshToken,
            RefreshTokenExpiresAtUtc = refreshToken.ExpiresAtUtc,
            Email = user.Email,
            FullName = user.FullName,
            RequiresTwoFactor = false
        };
    }

    private async Task<AuthDefinition?> GetOrCreateGoogleUserAsync(string credential)
    {
        GoogleJsonWebSignature.Payload payload;

        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                credential,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { _googleOptions.ClientId }
                });
        }
        catch
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(payload.Subject))
            return null;

        if (string.IsNullOrWhiteSpace(payload.Email))
            return null;

        if (!payload.EmailVerified)
            return null;

        var googleSub = payload.Subject;
        var email = payload.Email.Trim().ToLowerInvariant();
        var fullName = payload.Name?.Trim() ?? string.Empty;

        var user = await _repo.GetByExternalLoginAsync("Google", googleSub);

        if (user == null)
        {
            user = await _repo.GetByEmailAsync(email);

            if (user != null)
            {
                user.ExternalLogins ??= new List<ExternalLogin>();

                var alreadyLinked = user.ExternalLogins.Any(x =>
                    x.Provider == "Google" &&
                    x.ProviderUserId == googleSub);

                if (!alreadyLinked)
                {
                    user.ExternalLogins.Add(new ExternalLogin
                    {
                        Provider = "Google",
                        ProviderUserId = googleSub,
                        LinkedAtUtc = DateTime.UtcNow
                    });
                }

                user.EmailVerified = true;

                if (string.IsNullOrWhiteSpace(user.FullName) && !string.IsNullOrWhiteSpace(fullName))
                    user.FullName = fullName;

                await _repo.UpdateAsync(user);
            }
            else
            {
                user = new AuthDefinition
                {
                    Email = email,
                    FullName = fullName,
                    PasswordHash = null,
                    EmailVerified = true,
                    ExternalLogins = new List<ExternalLogin>
                {
                    new ExternalLogin
                    {
                        Provider = "Google",
                        ProviderUserId = googleSub,
                        LinkedAtUtc = DateTime.UtcNow
                    }
                },
                    RefreshTokens = new List<RefreshTokenDefinition>(),
                    CreatedAtUtc = DateTime.UtcNow
                };

                await _repo.CreateAsync(user);
            }
        }

        user.RefreshTokens ??= new List<RefreshTokenDefinition>();
        user.ExternalLogins ??= new List<ExternalLogin>();

        return user;
    }

    [Authorize]
    [HttpDelete("me")]
    public async Task<IActionResult> DeleteMyAccount([FromBody] DeleteAccountRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var result = await _authService.DeleteAccountAsync(userId, request);

        return result switch
        {
            DeleteAccountResult.Success => NoContent(),

            DeleteAccountResult.InvalidPassword =>
            BadRequest(new ApiError
            {
                Code = "AUTH_PASSWORD_INVALID",
                Message = "Password is incorrect."
            }),
            DeleteAccountResult.AlreadyDeleted =>
            BadRequest(new ApiError
            {
                Code = "AUTH_ACCOUNT_DELETED",
                Message = "Account is already deleted."
            }),
            DeleteAccountResult.UserNotFound =>
            BadRequest(new ApiError
            {
                Code = "AUTH_USER_NOT_FOUND",
                Message = "User not found."
            }),
            _ => BadRequest(new ApiError
            {
                Code = "AUTH_NOT_DELETED",
                Message = "Account could not be deleted."
            })
        };
    }

    [EnableRateLimiting("auth-strict")]
    [HttpPost("login/2fa")]
    public async Task<ActionResult<AuthResponse>> VerifyTwoFactor([FromBody] VerifyTwoFactorRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TwoFactorToken) || string.IsNullOrWhiteSpace(request.Code))
            return BadRequest("Two-factor token and code are required.");

        var principal = _jwtService.ValidateTwoFactorToken(request.TwoFactorToken);
        if (principal == null)
            return Unauthorized("Invalid or expired two-factor token.");

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized("Invalid two-factor token.");

        var user = await _repo.GetByIdAsync(userId);
        if (user == null)
            return Unauthorized("User not found.");

        if (!user.TwoFactorEnabled || string.IsNullOrWhiteSpace(user.TwoFactorSecret))
            return BadRequest("Two-factor authentication is not enabled for this user.");

        byte[] secretBytes;

        try
        {
            secretBytes = Base32Encoding.ToBytes(user.TwoFactorSecret);
        }
        catch
        {
            return BadRequest("Invalid two-factor secret.");
        }

        var totp = new Totp(secretBytes);
        var isValid = totp.VerifyTotp(
            request.Code.Replace(" ", "").Replace("-", ""),
            out _,
            new VerificationWindow(previous: 1, future: 1)
        );

        if (!isValid)
            return Unauthorized("Invalid authentication code.");

        var accessToken = _jwtService.GenerateAccessToken(user);
        var accessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(30);

        var rawRefreshToken = TokenHelper.GenerateSecureToken();
        var refreshTokenHash = TokenHelper.ComputeSha256(rawRefreshToken);

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        var refreshToken = new RefreshTokenDefinition
        {
            TokenHash = refreshTokenHash,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedByIp = ipAddress
        };

        var now = DateTime.UtcNow;
        user.RefreshTokens = user.RefreshTokens
            .Where(x => x.RevokedAtUtc == null && x.ExpiresAtUtc > now)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(5)
            .ToList();

        user.RefreshTokens.Add(refreshToken);
        await _repo.UpdateAsync(user);

        return Ok(new AuthResponse
        {
            Token = accessToken,
            TokenExpiresAtUtc = accessTokenExpiresAtUtc,
            RefreshToken = rawRefreshToken,
            RefreshTokenExpiresAtUtc = refreshToken.ExpiresAtUtc,
            Email = user.Email,
            FullName = user.FullName,
            RequiresTwoFactor = false
        });
    }

    [Authorize]
    [HttpPost("2fa/setup")]
    public async Task<IActionResult> SetupTwoFactor()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id is required");

        var user = await _repo.GetByIdAsync(userId);

        if (user == null)
            return Unauthorized();

        var secretBytes = KeyGeneration.GenerateRandomKey(20);
        var secret = Base32Encoding.ToString(secretBytes);

        user.TwoFactorSecret = secret;
        user.TwoFactorEnabled = false;

        await _repo.UpdateAsync(user);

        var issuer = "UseSignFlow";
        var email = user.Email;

        var otpauthUrl =
            $"otpauth://totp/{issuer}:{email}?secret={secret}&issuer={issuer}&digits=6";

        return Ok(new
        {
            secret,
            otpauthUrl
        });
    }

    [Authorize]
    [HttpPost("2fa/disable")]
    public async Task<IActionResult> DisableTwoFactor([FromBody] DisableTwoFactorRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id is required");

        var user = await _repo.GetByIdAsync(userId);

        if (user == null)
            return Unauthorized();

        var passwordHasher = new PasswordHasher<AuthDefinition>();
        var verify = BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash);

        if (!verify)
            return BadRequest(new ApiError
            {
                Code = "AUTH_PASSWORD_INVALID",
                Message = "Incorrect password."
            });

        if (user.TwoFactorEnabled && !string.IsNullOrEmpty(user.TwoFactorSecret))
        {
            var totp = new Totp(Base32Encoding.ToBytes(user.TwoFactorSecret));
            var isValid = totp.VerifyTotp(request.Code, out _, new VerificationWindow(1, 1));

            if (!isValid)
                return BadRequest(new ApiError
                {
                    Code = "2FA_CODE_INVALID",
                    Message = "Invalid 2FA code"
                });
        }

        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        user.RefreshTokens.Clear();

        await _repo.UpdateAsync(user);

        return Ok(new { message = "2FA disabled successfully" });
    }

    [Authorize]
    [HttpPost("2fa/enable")]
    public async Task<IActionResult> EnableTwoFactor([FromBody] Enable2FARequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id is required");

        var user = await _repo.GetByIdAsync(userId);

        if (user == null)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(user.TwoFactorSecret))
            return BadRequest("2FA setup not initialized.");

        var secretBytes = Base32Encoding.ToBytes(user.TwoFactorSecret);
        var totp = new Totp(secretBytes);

        var isValid = totp.VerifyTotp(
            request.Code.Replace(" ", "").Replace("-", ""),
            out _,
            new VerificationWindow(previous: 1, future: 1)
        );

        if (!isValid)
            return BadRequest("Invalid code.");

        user.TwoFactorEnabled = true;
        await _repo.UpdateAsync(user);

        return Ok(new { message = "2FA enabled." });
    }

    [HttpGet("verify-email")]
    public async Task<ActionResult> VerifyEmail([FromQuery] string email, [FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            return BadRequest("Invalid verification request.");

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _repo.GetByEmailAsync(normalizedEmail);

        if (user == null)
            return BadRequest("User not found.");

        if (user.EmailVerified)
            return Ok(new { message = "Email is already verified." });

        if (user.EmailVerificationTokenExpiresAtUtc == null ||
            user.EmailVerificationTokenExpiresAtUtc < DateTime.UtcNow)
            return BadRequest("Verification token has expired.");

        var incomingTokenHash = TokenHelper.ComputeSha256(token);

        if (!string.Equals(user.EmailVerificationTokenHash, incomingTokenHash, StringComparison.OrdinalIgnoreCase))
            return BadRequest("Invalid verification token.");

        user.EmailVerified = true;
        user.EmailVerificationTokenHash = null;
        user.EmailVerificationTokenExpiresAtUtc = null;

        await _repo.UpdateAsync(user);

        return Ok(new { message = "Email verified successfully." });
    }

    [Authorize]
    [HttpPost("notifications")]
    public async Task<IActionResult> SetNotifications([FromBody] EnableNotificationsRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id is required");

        var user = await _repo.GetByIdAsync(userId);

        if (user == null)
            return Unauthorized();

        user.NotificationsEnabled = request.Enabled;

        await _repo.UpdateAsync(user);

        return Ok(new { message = $"Notifications {(request.Enabled ? "enabled" : "disabled")}." });
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

    [EnableRateLimiting("auth-strict")]
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest("Email and refresh token are required.");

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _repo.GetByEmailAsync(email);

        if (user == null)
            return Unauthorized("Invalid refresh request.");

        var incomingRefreshTokenHash = TokenHelper.ComputeSha256(request.RefreshToken);

        var existingRefreshToken = user.RefreshTokens
            .FirstOrDefault(x =>
                x.TokenHash == incomingRefreshTokenHash &&
                x.RevokedAtUtc == null &&
                x.ExpiresAtUtc > DateTime.UtcNow);

        if (existingRefreshToken == null)
            return Unauthorized("Invalid or expired refresh token.");

        var newAccessToken = _jwtService.GenerateAccessToken(user);
        var newAccessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(30);

        var newRawRefreshToken = TokenHelper.GenerateSecureToken();
        var newRefreshTokenHash = TokenHelper.ComputeSha256(newRawRefreshToken);

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        existingRefreshToken.RevokedAtUtc = DateTime.UtcNow;
        existingRefreshToken.RevokedByIp = ipAddress;
        existingRefreshToken.ReplacedByTokenHash = newRefreshTokenHash;

        var newRefreshToken = new RefreshTokenDefinition
        {
            TokenHash = newRefreshTokenHash,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedByIp = ipAddress
        };

        user.RefreshTokens.Add(newRefreshToken);

        user.RefreshTokens = user.RefreshTokens
            .Where(x => x.RevokedAtUtc == null && x.ExpiresAtUtc > DateTime.UtcNow)
            .ToList();

        await _repo.UpdateAsync(user);

        return Ok(new AuthResponse
        {
            Token = newAccessToken,
            TokenExpiresAtUtc = newAccessTokenExpiresAtUtc,
            RefreshToken = newRawRefreshToken
        });
    }

    [EnableRateLimiting("auth-strict")]
    [Authorize]
    [HttpPut("change/password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
            string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new ApiError
            {
                Code = "AUTH_PASSWORD_INVALID",
                Message = "Both the current password and the new password are required."
            });
        }

        if (request.NewPassword == request.CurrentPassword)
        {
            return BadRequest(new ApiError
            {
                Code = "AUTH_PASSWORD_INVALID",
                Message = "Please choose a different password."
            });
        }

        if (!IsPasswordValid(request.NewPassword))
        {
            return BadRequest(new ApiError
            {
                Code = "AUTH_PASSWORD_INVALID",
                Message = "The password must be at least 8 characters long and include at least one uppercase letter, one lowercase letter, and one special character."
            });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new ApiError
            {
                Code = "AUTH_UNAUTHORIZED",
                Message = "User is not authenticated."
            });
        }

        var user = await _repo.GetByIdAsync(userId);
        if (user == null)
        {
            return NotFound(new ApiError
            {
                Code = "AUTH_USER_NOT_FOUND",
                Message = "User not found."
            });
        }

        bool isCurrentPasswordValid;
        try
        {
            isCurrentPasswordValid = BCrypt.Net.BCrypt.Verify(
                request.CurrentPassword,
                user.PasswordHash
            );
        }
        catch
        {
            return BadRequest(new ApiError
            {
                Code = "AUTH_PASSWORD_HASH_INVALID",
                Message = "Stored password hash is invalid."
            });
        }

        if (!isCurrentPasswordValid)
        {
            return BadRequest(new ApiError
            {
                Code = "AUTH_PASSWORD_INCORRECT",
                Message = "Current password is incorrect."
            });
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _repo.UpdateAsync(user);

        return Ok();
    }

    [EnableRateLimiting("auth-strict")]
    [HttpPost("reset-password")]
    public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { code = "VALIDATION_ERROR", message = "Email is required." });

            if (string.IsNullOrWhiteSpace(request.Token))
                return BadRequest(new { code = "VALIDATION_ERROR", message = "Token is required." });

            if (string.IsNullOrWhiteSpace(request.NewPassword))
                return BadRequest(new { code = "VALIDATION_ERROR", message = "New password is required." });

            var email = request.Email.Trim().ToLowerInvariant();
            var tokenHash = TokenHelper.ComputeSha256(request.Token);

            var user = await _repo.GetByEmailAsync(email);

            if (user == null || user.IsDeleted ||
                string.IsNullOrWhiteSpace(user.PasswordResetTokenHash) ||
                !string.Equals(user.PasswordResetTokenHash, tokenHash, StringComparison.Ordinal) ||
                !user.PasswordResetTokenExpiresAtUtc.HasValue ||
                user.PasswordResetTokenExpiresAtUtc.Value < DateTime.UtcNow)
            {
                return BadRequest(new
                {
                    code = "INVALID_RESET_REQUEST",
                    message = "Invalid or expired password reset request."
                });
            }

            if (!IsPasswordValid(request.NewPassword))
            {
                return BadRequest(new
                {
                    code = "AUTH_PASSWORD_INVALID",
                    message = "The password must be at least 8 characters long and include at least one uppercase letter, one lowercase letter, and one special character."
                });
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.PasswordResetTokenHash = null;
            user.PasswordResetTokenExpiresAtUtc = null;
            user.PasswordResetRequestedAtUtc = null;
            user.UpdatedAtUtc = DateTime.UtcNow;
            user.RefreshTokens = new List<RefreshTokenDefinition>();


            await _repo.UpdateAsync(user);

            return Ok(new
            {
                message = "Your password has been reset successfully."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                code = "SERVER_ERROR",
                message = ex.Message,
                detail = ex.ToString()
            });
        }
    }

    private static bool IsPasswordValid(string password)
    {
        return Regex.IsMatch(password, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*[\W_]).{8,}$");
    }
}