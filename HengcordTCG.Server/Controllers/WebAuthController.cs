using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using HengcordTCG.Shared.Data;
using Microsoft.EntityFrameworkCore;
using HengcordTCG.Server.Authentication;

namespace HengcordTCG.Server.Controllers;

[ApiController]
public class WebAuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;

    public WebAuthController(IConfiguration config, AppDbContext db)
    {
        _config = config;
        _db = db;
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

    [HttpGet("api/auth/logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromQuery] string? returnUrl = null)
    {
        var fallback = _config["ClientUrl"] ?? "/";
        var redirect = string.IsNullOrWhiteSpace(returnUrl) ? fallback : returnUrl;

        await HttpContext.SignOutAsync();
        return Redirect(redirect);
    }

    [HttpGet("api/auth/callback")]
    [Authorize(AuthenticationSchemes = "Discord")]
    public async Task<IActionResult> Callback([FromQuery] string? returnUrl = null)
    {
        var jwtSecret = _config["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = User.Claims.ToList();
        claims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));

        // Check if user is admin from database and add claim
        var discordIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(discordIdStr) && ulong.TryParse(discordIdStr, out var discordId))
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.DiscordId == discordId);
            if (user?.IsBotAdmin == true)
            {
                claims.Add(new Claim("is_admin", "true"));
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            }
        }

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
    [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{ApiKeyAuthenticationOptions.DefaultScheme}")]
    public IActionResult Me()
    {
        try
        {
        var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
        var name = User.Identity?.Name;
        var avatarHash = User.FindFirst("urn:discord:avatar")?.Value;
        var isAdmin = User.HasClaim("is_admin", "true");
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        // Debug: log all claims
        var allClaims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
        
        // Try to get from different claim types
        if (string.IsNullOrEmpty(userId))
            userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            userId = User.FindFirst("nameid")?.Value;
            
        var roles = User.Claims.Where(c => c.Type == ClaimTypes.Role || c.Type == "role").Select(c => c.Value).ToList();

        // Build full avatar URL from Discord CDN
        string? avatarUrl = null;
        if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(avatarHash))
        {
            avatarUrl = $"https://cdn.discordapp.com/avatars/{userId}/{avatarHash}.png";
        }
        else if (!string.IsNullOrEmpty(userId))
        {
            // User has default Discord avatar (no custom avatar set)
            // Use the default avatar based on user discriminator modulo 5
            var discriminator = User.FindFirst("urn:discord:discriminator")?.Value;
            int defaultAvatarIndex = 0;
            if (!string.IsNullOrEmpty(discriminator) && int.TryParse(discriminator, out var disc))
            {
                defaultAvatarIndex = disc % 5;
            }
            else if (ulong.TryParse(userId, out var userIdNum))
            {
                defaultAvatarIndex = (int)(userIdNum % 5);
            }
            avatarUrl = $"https://cdn.discordapp.com/embed/avatars/{defaultAvatarIndex}.png";
        }

        return Ok(new
        {
            IsAuthenticated = isAuthenticated,
            Name = name,
            AvatarUrl = avatarUrl,
            IsAdmin = isAdmin,
            UserId = userId,
            Roles = roles,
            DebugClaims = allClaims
        });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
        }
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

    [HttpGet("api/auth/debug-claims")]
    [AllowAnonymous]
    public IActionResult DebugClaims()
    {
        var claims = User.Claims.Select(c => new { type = c.Type, value = c.Value }).ToList();
        var nameIdentifier = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var sub = User.FindFirst("sub")?.Value;
        return Ok(new { claims, nameIdentifier, sub });
    }

    [HttpGet("api/auth/debug-token")]
    [AllowAnonymous]
    public IActionResult DebugToken([FromQuery] string? token)
    {
        if (string.IsNullOrEmpty(token))
            return BadRequest("Token required");

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            
            var claims = jwtToken.Claims.Select(c => new { type = c.Type, value = c.Value }).ToList();
            var sub = jwtToken.Subject;
            var nameIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "nameid");
            
            return Ok(new { subject = sub, nameId = nameIdClaim?.Value, claims });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
