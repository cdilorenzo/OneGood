using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using OneGood.Core.Interfaces;
using OneGood.Core.Models;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace OneGood.Api.Controllers;

public record SyncRequest(int Streak, int ActionsCompleted);

internal static class HashHelper
{
    internal static string Sha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input.Trim().ToLowerInvariant()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepo;
    private readonly IConfiguration _config;

    public AuthController(IUserRepository userRepo, IConfiguration config)
    {
        _userRepo = userRepo;
        _config = config;
    }

    /// <summary>
    /// Initiates Google OAuth login flow.
    /// </summary>
    [HttpGet("login/google")]
    public IActionResult LoginWithGoogle([FromQuery] string? returnUrl = null)
    {
        var googleClientId = _config["Authentication:Google:ClientId"];
        if (string.IsNullOrEmpty(googleClientId))
            return StatusCode(503, new { error = "Google login is not configured on this server." });

        // After the middleware handles /api/auth/google-callback it will redirect to this URI
        var completeUrl = Url.Action(nameof(GoogleCallback), "Auth", new { returnUrl },
            protocol: Request.Scheme, host: Request.Host.Value);
        var properties = new AuthenticationProperties { RedirectUri = completeUrl };
        return Challenge(properties, "Google");
    }

    /// <summary>
    /// Callback from Google OAuth — called by the middleware after /api/auth/google-callback is handled.
    /// The middleware signs the cookie in and then redirects here via RedirectUri.
    /// </summary>
    [HttpGet("complete")]
    public async Task<IActionResult> GoogleCallback([FromQuery] string? returnUrl = null)
    {
        var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        if (!result.Succeeded || result.Principal is null)
        {
            return Redirect(GetFrontendUrl() + "?error=auth_failed");
        }

        var email = result.Principal.FindFirst(ClaimTypes.Email)?.Value;
        var name = result.Principal.FindFirst(ClaimTypes.Name)?.Value;
        var googleId = result.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var picture = result.Principal.FindFirst("picture")?.Value;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(googleId))
        {
            return Redirect(GetFrontendUrl() + "?error=missing_email");
        }

        var emailHash = HashHelper.Sha256(email);

        // Find or create user + profile (look up by email hash)
        var profile = await _userRepo.GetByEmailAsync(emailHash);
        if (profile is null)
        {
            // Must create parent User first (UserProfile has FK → Users.Id)
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Email = emailHash,
                DisplayName = name ?? email.Split('@')[0],
                IsAnonymous = false,
                CreatedAt = DateTime.UtcNow,
                LastActiveAt = DateTime.UtcNow
            };
            await _userRepo.CreateAsync(user);

            profile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,            // FK satisfied
                Email = emailHash,
                DisplayName = name ?? email.Split('@')[0],
                GoogleId = googleId,
                AvatarUrl = picture,
                CreatedAt = DateTime.UtcNow
            };
            await _userRepo.CreateAsync(profile);
        }
        else
        {
            // Update existing user info (never overwrite email hash)
            profile.DisplayName = name ?? profile.DisplayName;
            profile.GoogleId = googleId;
            profile.AvatarUrl = picture;
            await _userRepo.UpdateAsync(profile);
        }

        // Redirect back to frontend with user ID in query (frontend will store in localStorage)
        var frontendUrl = returnUrl ?? GetFrontendUrl();
        return Redirect($"{frontendUrl}?userId={profile.Id}&name={Uri.EscapeDataString(profile.DisplayName ?? "")}");
    }

    /// <summary>
    /// Gets current user info.
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser([FromQuery] Guid? userId = null)
    {
        if (!userId.HasValue)
        {
            return Ok(new { isLoggedIn = false });
        }

        var profile = await _userRepo.GetProfileAsync(userId.Value);
        if (profile is null)
        {
            return Ok(new { isLoggedIn = false });
        }

        return Ok(new
        {
            isLoggedIn = true,
            userId = profile.Id,
            name = profile.DisplayName,
            avatar = profile.AvatarUrl,
            actionsCompleted = profile.ActionsCompleted,
            currentStreak = profile.CurrentStreak
        });
    }

    /// <summary>
    /// Logs out and clears auth cookie.
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { success = true });
    }

    /// <summary>
    /// Syncs streak/completion count from localStorage to the user's DB profile.
    /// Called after the user completes an action while logged in.
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> SyncProgress([FromQuery] Guid userId, [FromBody] SyncRequest req)
    {
        var profile = await _userRepo.GetProfileAsync(userId);
        if (profile is null) return NotFound();

        // Only update if the client streak is ahead (handles offline/multiple devices)
        if (req.Streak > profile.CurrentStreak)
            profile.CurrentStreak = req.Streak;
        if (req.ActionsCompleted > profile.ActionsCompleted)
            profile.ActionsCompleted = req.ActionsCompleted;

        await _userRepo.UpdateAsync(profile);
        return Ok(new { currentStreak = profile.CurrentStreak, actionsCompleted = profile.ActionsCompleted });
    }

    /// <summary>
    /// GDPR: deletes all personal data for this user (anonymises the profile).
    /// </summary>
    [HttpDelete("account")]
    public async Task<IActionResult> DeleteAccount([FromQuery] Guid userId)
    {
        var deleted = await _userRepo.DeleteAsync(userId);
        if (!deleted) return NotFound();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { success = true });
    }

    private string GetFrontendUrl()
    {
        var origins = _config.GetSection("Cors:AllowedOrigins").Get<string[]>();
        return origins?.FirstOrDefault() ?? "http://localhost:5133";
    }
}
