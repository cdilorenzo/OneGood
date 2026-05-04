using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.EntityFrameworkCore;
using OneGood.Api;
using OneGood.Api.Hubs;
using OneGood.Api.Services;
using OneGood.Core.Interfaces;
using OneGood.Infrastructure;
using OneGood.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Add OneGood infrastructure services
builder.Services.AddInfrastructure(builder.Configuration);

// Self-ping to prevent Render free tier from sleeping (only active when KeepAlive:Url is set)
builder.Services.AddHostedService<KeepAliveService>();

// Run background worker in-process (causes refresh, classification, cache warming)
builder.Services.AddHostedService<OneGood.Workers.CauseRefreshWorker>();

// Add API services
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddSingleton<ICauseNotifier, SignalRCauseNotifier>();

// Google OAuth Authentication (optional - only if configured)
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
var googleAuthConfigured = !string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret);

var isDevelopment = builder.Environment.IsDevelopment();

// Always register cookie auth so UseAuthentication() never throws.
// Google handler is added on top only when credentials are present.
var authBuilder = builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        // In dev: Lax + HTTP-allowed so the correlation cookie survives the OAuth round-trip
        // In prod: None + Secure for cross-site cookie support
        options.Cookie.SameSite = isDevelopment ? SameSiteMode.Lax : SameSiteMode.None;
        options.Cookie.SecurePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    });

if (googleAuthConfigured)
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId!;
        options.ClientSecret = googleClientSecret!;
        options.Scope.Add("email");
        options.Scope.Add("profile");
        options.SaveTokens = true;
        options.CallbackPath = "/api/auth/google-callback";
        // Fix correlation cookie in dev (same issue — must survive HTTP redirect)
        options.CorrelationCookie.SameSite = isDevelopment ? SameSiteMode.Lax : SameSiteMode.None;
        options.CorrelationCookie.SecurePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    });
}

// CORS - configured via appsettings.json
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
    ?? ["http://localhost:5133", "https://localhost:7168"];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

var app = builder.Build();
// Apply EF Core migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OneGoodDbContext>();
    db.Database.Migrate();

    // Only seed dummy data if explicitly enabled in appsettings
    var useSeedData = app.Configuration.GetValue<bool>("Features:UseSeedData");
    if (useSeedData)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("🌱 Seeding dummy data (Features:UseSeedData = true)");
        await SeedData.SeedAsync(db);
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors();

// Serve static files (HTML, CSS, JS)
app.UseDefaultFiles();
app.UseStaticFiles();

// Authentication (if configured)
app.UseAuthentication();
app.UseAuthorization();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.MapControllers();
app.MapHub<CauseHub>("/hubs/causes");

app.Run();
