using MarksApi.Data;
using MarksApi.Api;
using MarksApi.Middleware;
using MarksApi.Models;
using MarksApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;
using MarksApi.SmokeTests;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

var runSmokeTest = args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase);
var builderArguments = args
    .Where(argument => !string.Equals(argument, "--smoke-test", StringComparison.OrdinalIgnoreCase))
    .ToArray();
var builder = WebApplication.CreateBuilder(builderArguments);
runSmokeTest |= builder.Configuration.GetValue<bool>("SMOKE_TEST");

builder.Services.AddOpenApi();
builder.Services.AddValidation();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Instance = context.HttpContext.Request.Path;
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

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
builder.Services.AddScoped<IMarksService, MarksService>();
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("login", limiter =>
    {
        limiter.PermitLimit = 5;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too Many Requests",
            Detail = "Too many login attempts. Try again later.",
            Instance = context.HttpContext.Request.Path
        }, cancellationToken);
    };
});

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
app.UseHttpsRedirection();
if (allowedOrigins.Length > 0)
{
    app.UseCors();
}
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { status = "Running" }));

app.MapGet("/health", async (IMarksService service, CancellationToken cancellationToken) =>
{
    if (await service.CheckDatabaseHealthAsync(cancellationToken))
    {
        return Results.Ok(new { status = "Healthy" });
    }

    return Results.Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "Service Unavailable",
        detail: "The database is not currently reachable.");
})
    .WithName("HealthCheck")
    .WithSummary("Check API and PostgreSQL availability.")
    .Produces(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

app.MapResourceEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

if (app.Environment.IsDevelopment())
{
    await DevelopmentSeeder.SeedAsync(app.Services, app.Logger, app.Configuration);
}

if (runSmokeTest)
{
    if (!app.Environment.IsDevelopment())
    {
        app.Logger.LogError("Smoke tests are enabled only in the Development environment.");
        Environment.ExitCode = 1;
        return;
    }

    await app.StartAsync();
    var addresses = app.Services.GetRequiredService<IServer>()
        .Features.Get<IServerAddressesFeature>()?.Addresses;
    var baseAddress = addresses?
        .Select(address => new Uri(address))
        .FirstOrDefault(uri => uri.Scheme == Uri.UriSchemeHttp);
    if (baseAddress is null)
    {
        app.Logger.LogError("Smoke tests could not determine the API HTTP address.");
        Environment.ExitCode = 1;
        await app.StopAsync();
        return;
    }

    var succeeded = false;
    try
    {
        using var smokeTests = new SmokeTestRunner(app.Configuration);
        succeeded = await smokeTests.RunAsync(baseAddress);
    }
    catch (Exception exception)
    {
        app.Logger.LogError(exception, "Smoke test run failed unexpectedly.");
    }
    finally
    {
        await app.StopAsync();
    }
    Environment.ExitCode = succeeded ? 0 : 1;
    return;
}

await app.RunAsync();
