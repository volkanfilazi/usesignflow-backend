using DynamicFormBuilder.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

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

builder.Services.AddSingleton<DynamicFormBuilder.Services.FormRepository>();
builder.Services.AddSingleton<DynamicFormBuilder.Services.AuthRepository>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        p => p.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod());
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

app.UseCors("AllowAngular");
app.UseRouting();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();