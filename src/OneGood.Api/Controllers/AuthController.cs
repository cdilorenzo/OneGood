using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using OneGood.Core.Interfaces;
using OneGood.Core.Models;
using System.Security.Claims;

namespace OneGood.Api.Controllers;

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
        var redirectUrl = Url.Action(nameof(GoogleCallback), "Auth", new { returnUrl });
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(properties, "Google");
    }

    /// <summary>
    /// Callback from Google OAuth.
    /// </summary>
    [HttpGet("google-callback")]
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

        // Find or create user profile
        var profile = await _userRepo.GetByEmailAsync(email);
        if (profile is null)
        {
            profile = new UserProfile
            {
                Id = Guid.NewGuid(),
                Email = email,
                DisplayName = name ?? email.Split('@')[0],
                GoogleId = googleId,
                AvatarUrl = picture,
                CreatedAt = DateTime.UtcNow
            };
            await _userRepo.CreateAsync(profile);
        }
        else
        {
            // Update existing user info
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
            email = profile.Email,
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

    private string GetFrontendUrl()
    {
        var origins = _config.GetSection("Cors:AllowedOrigins").Get<string[]>();
        return origins?.FirstOrDefault() ?? "http://localhost:5133";
    }
}
