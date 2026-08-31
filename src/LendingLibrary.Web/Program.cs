using System.Threading.RateLimiting;
using LendingLibrary.Web.Data;
using LendingLibrary.Web.Domain.Entities;
using LendingLibrary.Web.Infrastructure;
using LendingLibrary.Web.Services.Abstractions;
using LendingLibrary.Web.Services.Implementations;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

// One-shot migration mode: `dotnet LendingLibrary.Web.dll --migrate` applies pending migrations
// against the same image, then exits — the prod migration strategy (an init/one-shot container),
// separate from the app's own normal startup, which never auto-migrates outside Development.
if (args.Contains("--migrate"))
{
    var migrationBuilder = WebApplication.CreateBuilder(args);
    migrationBuilder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(migrationBuilder.Configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.")));

    await using var migrationHost = migrationBuilder.Build();
    using var migrationScope = migrationHost.Services.CreateScope();
    var migrationDb = migrationScope.ServiceProvider.GetRequiredService<AppDbContext>();
    await migrationDb.Database.MigrateAsync();

    Console.WriteLine("Migrations applied successfully.");
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeAreaFolder("Admin", "/", "RequireAdmin");
});

// Reads builder.Configuration lazily (not into a variable up front) so that
// WebApplicationFactory-based tests, which merge their config overrides in at
// builder.Build() time, can still replace the connection string.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Connection string 'Default' is not configured.")));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.Password.RequiredLength = 12;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.SignIn.RequireConfirmedEmail = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders()
    .AddPasswordValidator<CommonPasswordValidator>();

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin"))
    .AddPolicy("RequireUser", policy => policy.RequireAuthenticatedUser());

// Blunts brute-force attempts against login/register (applied via [EnableRateLimiting("auth")]).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // KnownProxies/KnownNetworks default to loopback-only, which is correct for a same-host
    // sidecar proxy but too strict for a separate proxy container/host. Before deploying behind
    // one, add its address, e.g.: options.KnownNetworks.Add(IPNetwork.Parse("10.0.0.0/8"));
    // Trusting an unrestricted set of proxies lets a client spoof X-Forwarded-* headers.
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// Persisted so cookies/tokens survive restarts and are shared across replicas in prod.
var dataProtectionKeyPath = builder.Configuration["DataProtection:KeyPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeyPath))
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath));
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IEmailSender, ConsoleEmailSender>();
builder.Services.AddScoped<ICatalogueService, CatalogueService>();
builder.Services.AddScoped<ILendingService, LendingService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<IUserAdminService, UserAdminService>();
builder.Services.AddHostedService<ReservationExpiryService>();
builder.Services.Configure<LendingOptions>(builder.Configuration.GetSection(LendingOptions.SectionName));

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Connection string 'Default' is not configured."));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
    await IdentitySeeder.SeedAsync(scope.ServiceProvider, app.Configuration, timeProvider);
    await CatalogueSeeder.SeedAsync(db, timeProvider);
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Error/{0}");

// Must run before anything that reads scheme/remote IP (HTTPS redirection, the rate limiter's
// per-IP partitioning, HSTS) so those see the original client info, not the proxy's.
app.UseForwardedHeaders();

app.UseHttpsRedirection();

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.MapHealthChecks("/health");

app.Run();

// Exposed so WebApplicationFactory<Program> can host this app in integration tests.
public partial class Program;
