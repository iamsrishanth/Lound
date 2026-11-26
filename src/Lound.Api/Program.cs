using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Lound.Api.Api;
using Lound.Api.Data;
using Lound.Api.Domain;
using Lound.Api.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection on the API server.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString,
    sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager();

var issuer = builder.Configuration["Jwt:Issuer"] ?? "Lound.Api";
var audience = builder.Configuration["Jwt:Audience"] ?? "Lound.Desktop";
var configuredSigningKey = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(configuredSigningKey))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException("Set Jwt:SigningKey using server-side secret configuration before starting outside Development.");
    configuredSigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    Console.Error.WriteLine("Warning: using an ephemeral development JWT key. Set Jwt:SigningKey to preserve sessions across restarts.");
}
var signingKeyBytes = Encoding.UTF8.GetBytes(configuredSigningKey);
if (signingKeyBytes.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 UTF-8 bytes.");
var signingKey = new SymmetricSecurityKey(signingKeyBytes);
var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
builder.Services.AddSingleton(signingCredentials);
builder.Services.AddSingleton<TokenIssuer>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = ClaimTypes.Role
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userId = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
                var securityStamp = context.Principal?.FindFirstValue("sst");
                if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(securityStamp))
                {
                    context.Fail("The access token is not valid.");
                    return;
                }
                var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await users.FindByIdAsync(userId);
                if (user is null || !user.IsActive || user.IsDirectoryRemoved ||
                    !string.Equals(user.SecurityStamp, securityStamp, StringComparison.Ordinal))
                    context.Fail("The account is not available.");
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Lound.Api.UnhandledException");
    if (exception is not null) logger.LogError(exception, "Unhandled API error");
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new
    {
        code = "server_error",
        message = "An unexpected error occurred."
    });
}));
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapLoundEndpoints();

if (args.Length == 1 && args[0] == "--migrate")
{
    await DatabaseBootstrap.MigrateAndSeedRolesAsync(app.Services);
    Console.WriteLine("Database migrations are up to date.");
    return;
}
if (args.Length == 1 && args[0] == "--setup-admin")
{
    await DatabaseBootstrap.SetupFirstAdminAsync(app.Services);
    return;
}

await app.RunAsync();

public partial class Program { }
