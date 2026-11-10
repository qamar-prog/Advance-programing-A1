using MarksApi.Data;
using MarksApi.Api;
using MarksApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Instance = context.HttpContext.Request.Path;
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Database"))
        .UseSnakeCaseNamingConvention());

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Configuration value Jwt:Issuer is required.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Configuration value Jwt:Audience is required.");
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Secret configuration value Jwt:Key is required.");
var jwtLifetimeMinutes = builder.Configuration.GetValue<int>("Jwt:LifetimeMinutes");
if (jwtLifetimeMinutes <= 0)
{
    throw new InvalidOperationException("Jwt:LifetimeMinutes must be greater than zero.");
}

var passMark = builder.Configuration.GetValue<int>("PassMark");
if (passMark is < 0 or > 100)
{
    throw new InvalidOperationException("PassMark must be between 0 and 100.");
}

var signingKey = JwtSecurity.Key(jwtKey);
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.Name,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                await Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Unauthorized",
                    detail: "A valid bearer token is required.")
                    .ExecuteAsync(context.HttpContext);
            },
            OnForbidden = async context =>
            {
                await Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Forbidden",
                    detail: "You are not allowed to perform this operation.")
                    .ExecuteAsync(context.HttpContext);
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];
if (allowedOrigins.Length > 0)
{
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
if (allowedOrigins.Length > 0)
{
    app.UseCors();
}
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { status = "Running" }));

app.MapGet("/health", async (AppDbContext db, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    if (await db.Database.CanConnectAsync(cancellationToken))
    {
        return Results.Ok(new { status = "Healthy" });
    }

    logger.LogWarning("Health check failed because PostgreSQL is unavailable.");
    return Results.Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "Service Unavailable",
        detail: "The database is not currently reachable.");
});

app.MapResourceEndpoints();

if (app.Environment.IsDevelopment())
{
    await DevelopmentSeeder.SeedAsync(app.Services, app.Logger);
}

app.Run();
