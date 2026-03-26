using DynamicFormBuilder.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Microsoft.Extensions.FileProviders;
using System.Text;
using Microsoft.AspNetCore.HttpOverrides;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json.Serialization;
using QuestPDF.Infrastructure;
using DynamicFormBuilder.Services.Billing;
using DynamicFormBuilder.Repositories.Billing;
using DynamicFormBuilder.Services.Common;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddOpenApi();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            """{"message":"Too many requests. Please try again later."}""",
            cancellationToken);
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ip,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    options.AddPolicy("auth-strict", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"auth:{ip}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    var connectionString = builder.Configuration["MongoDb:ConnectionString"];
    var databaseName = builder.Configuration["MongoDb:DatabaseName"];

    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("MongoDb:ConnectionString is missing.");

    if (string.IsNullOrWhiteSpace(databaseName))
        throw new InvalidOperationException("MongoDb:DatabaseName is missing.");

    var client = new MongoClient(connectionString);
    return client.GetDatabase(databaseName);
});

builder.Services.AddSingleton<AuthRepository>();
builder.Services.AddSingleton<JwtService>();
builder.Services.AddSingleton<FormRepository>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<FormSubmissionRepository>();
builder.Services.AddScoped<SignatureRequestRepository>();
builder.Services.AddScoped<SubmissionAccessTokenRepository>();
builder.Services.AddScoped<AgreementTemplateRepository>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<BillingOverviewService>();
builder.Services.Configure<LemonOptions>(builder.Configuration.GetSection("Lemon"));
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>(); 
builder.Services.AddScoped<BillingWebhookEventRepository>();
builder.Services.AddScoped<ILemonPlanMapper, LemonPlanMapper>();
builder.Services.AddScoped<ILemonWebhookVerifier, LemonWebhookVerifier>();
builder.Services.AddScoped<ILemonWebhookProcessor, LemonWebhookProcessor>();
builder.Services.AddScoped<IPlanEntitlementService, PlanEntitlementService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IBillingService, BillingService>();
builder.Services.AddScoped<EmailLogRepository>();
builder.Services.AddScoped<PdfExportGuard>();
builder.Services.AddScoped<ILegalDocumentService, LegalDocumentService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IPdfService, PdfService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AppCors", policy =>
    {
        policy.WithOrigins(
                "http://localhost:4200",
                "https://localhost:4200",
                "https://usesignflow.com",
                "https://www.usesignflow.com")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.Configure<GoogleAuthOptions>(
    builder.Configuration.GetSection("GoogleAuth"));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtKey = builder.Configuration["Jwt:Key"]
                     ?? throw new InvalidOperationException("JWT key is missing.");

        var jwtIssuer = builder.Configuration["Jwt:Issuer"]
                        ?? throw new InvalidOperationException("JWT issuer is missing.");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtIssuer,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

QuestPDF.Settings.EnableDebugging = true;
var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseCors("AppCors");
app.UseRateLimiter();

app.UseStaticFiles();

var uploadsPath = builder.Configuration["UploadSettings:PhysicalRoot"];
if (string.IsNullOrWhiteSpace(uploadsPath))
    throw new InvalidOperationException("UploadSettings:PhysicalRoot is missing.");

Directory.CreateDirectory(uploadsPath);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();