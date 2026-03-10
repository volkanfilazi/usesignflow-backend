using DynamicFormBuilder.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.Text;
using Microsoft.AspNetCore.HttpOverrides;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
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

builder.Services.AddSingleton<FormRepository>();
builder.Services.AddSingleton<AuthRepository>();
builder.Services.AddScoped<ILegalDocumentService, LegalDocumentService>();
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
    {
        policy.WithOrigins(
                "http://localhost:4200",
                "https://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.UseCors("DevCors");
}

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();