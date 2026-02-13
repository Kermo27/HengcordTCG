using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;

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
            RedirectUri = "/api/auth/callback?returnUrl=" + Uri.EscapeDataString(redirect)
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

    [HttpGet("api/auth/callback")]
    [Authorize]
    public IActionResult Callback([FromQuery] string? returnUrl = null)
    {
        var jwtSecret = _config["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = User.Claims.ToList();
        claims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "HengcordTCG",
            audience: _config["Jwt:Audience"] ?? "HengcordTCG.Web",
            claims: claims,
            expires: DateTime.Now.AddDays(7),
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        
        // Redirect back to Blazor with token
        string redirectUrl;
        if (string.IsNullOrEmpty(returnUrl))
        {
            redirectUrl = $"/login?token={tokenString}";
        }
        else
        {
            // Check if returnUrl already has query parameters
            var separator = returnUrl.Contains("?") ? "&" : "?";
            redirectUrl = $"{returnUrl}{separator}token={tokenString}";
        }
        
        return Redirect(redirectUrl);
    }

    [HttpGet("api/auth/me")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public IActionResult Me()
    {
        var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
        var name = User.Identity?.Name;
        var avatarUrl = User.FindFirst("urn:discord:avatar")?.Value;
        var isAdmin = User.HasClaim("is_admin", "true");
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Ok(new
        {
            isAuthenticated,
            name,
            avatarUrl,
            isAdmin,
            userId
        });
    }

    [HttpGet("api/auth/token")]
    [Authorize]
    public IActionResult GetToken()
    {
        var jwtSecret = _config["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = User.Claims.ToList();
        claims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "HengcordTCG",
            audience: _config["Jwt:Audience"] ?? "HengcordTCG.Web",
            claims: claims,
            expires: DateTime.Now.AddDays(7),
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new
        {
            Token = tokenString,
            Username = User.Identity?.Name,
            UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            IsAdmin = User.HasClaim("is_admin", "true")
        });
    }

    [HttpGet("api/auth/discord")]
    [AllowAnonymous]
    public IActionResult DiscordLogin([FromQuery] string? returnUrl = null)
    {
        var fallback = _config["ClientUrl"] ?? "/";
        var redirect = string.IsNullOrWhiteSpace(returnUrl) ? fallback : returnUrl;

        var props = new AuthenticationProperties
        {
            RedirectUri = "/api/auth/callback?returnUrl=" + Uri.EscapeDataString(redirect)
        };
        return Challenge(props, "Discord");
    }
}
