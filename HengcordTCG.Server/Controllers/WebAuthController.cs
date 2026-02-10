using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HengcordTCG.Server.Controllers;

[ApiController]
public class WebAuthController : ControllerBase
{
    private readonly IConfiguration _config;

    public WebAuthController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet("login")]
    [AllowAnonymous]
    public async Task Login([FromQuery] string? returnUrl = null)
    {
        var fallback = _config["ClientUrl"] ?? "/";
        var redirect = string.IsNullOrWhiteSpace(returnUrl) ? fallback : returnUrl;

        await HttpContext.ChallengeAsync("Discord", new AuthenticationProperties
        {
            RedirectUri = redirect
        });
    }

    [HttpGet("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromQuery] string? returnUrl = null)
    {
        var fallback = _config["ClientUrl"] ?? "/";
        var redirect = string.IsNullOrWhiteSpace(returnUrl) ? fallback : returnUrl;

        await HttpContext.SignOutAsync();
        return Redirect(redirect);
    }

    [HttpGet("auth/me")]
    [AllowAnonymous]
    public IActionResult Me()
    {
        var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
        var name = User.Identity?.Name;
        var avatarUrl = User.FindFirst("avatar_url")?.Value;
        var isAdmin = User.HasClaim("is_admin", "true");

        return Ok(new
        {
            isAuthenticated,
            name,
            avatarUrl,
            isAdmin
        });
    }
}

