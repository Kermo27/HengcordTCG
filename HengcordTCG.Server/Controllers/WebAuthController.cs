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
using HengcordTCG.Shared.Services;

namespace HengcordTCG.Server.Controllers;

[ApiController]
public class WebAuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;
    private readonly UserService _userService;

    public WebAuthController(IConfiguration config, AppDbContext db, UserService userService)
    {
        _config = config;
        _db = db;
        _userService = userService;
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
    [HttpPost("api/auth/logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromQuery] string? returnUrl = null)
    {
        var fallback = _config["ClientUrl"] ?? "/";
        var redirect = string.IsNullOrWhiteSpace(returnUrl) ? fallback : returnUrl;

        // Delete the JWT cookie
        Response.Cookies.Delete("auth_token", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = "/"
        });

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

        // Add Discord username as Name claim
        var discordUsername = User.FindFirst("urn:discord:username")?.Value;
        if (string.IsNullOrEmpty(discordUsername))
        {
            discordUsername = User.FindFirst("urn:discord:global_name")?.Value;
        }
        if (string.IsNullOrEmpty(discordUsername))
        {
            discordUsername = User.FindFirst(ClaimTypes.Name)?.Value;
        }
        if (!string.IsNullOrEmpty(discordUsername))
        {
            claims.Add(new Claim(ClaimTypes.Name, discordUsername));
        }

// Check if user is admin from database and add claim
        var discordIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(discordIdStr) && ulong.TryParse(discordIdStr, out var discordId))
        {
            // Get or create user in database
            var user = await _userService.GetOrCreateUserAsync(discordId, discordUsername ?? $"User_{discordId}");
            
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
        
        // Set JWT as HttpOnly cookie for security
        Response.Cookies.Append("auth_token", tokenString, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = DateTime.Now.AddDays(7),
            Path = "/"
        });
        
        // Redirect back to Blazor without token in URL
        string redirectUrl;
        if (string.IsNullOrEmpty(returnUrl))
        {
            redirectUrl = "/login?success=true";
        }
        else
        {
            var separator = returnUrl.Contains("?") ? "&" : "?";
            redirectUrl = $"{returnUrl}{separator}success=true";
        }
        
        return Redirect(redirectUrl);
    }

    [HttpGet("api/auth/me")]
    [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{ApiKeyAuthenticationOptions.DefaultScheme}")]
    public async Task<IActionResult> Me()
    {
        try {
        var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
        var name = User.FindFirst("urn:discord:username")?.Value;
        if (string.IsNullOrEmpty(name))
        {
            name = User.FindFirst("urn:discord:global_name")?.Value;
        }
        if (string.IsNullOrEmpty(name))
        {
            name = User.Identity?.Name;
        }
        var avatarHash = User.FindFirst("urn:discord:avatar")?.Value;
        var isAdmin = false;
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        // Always check admin status from DB for live updates
        if (!string.IsNullOrEmpty(userId) && ulong.TryParse(userId, out var discordId))
        {
            var dbUser = await _db.Users.FirstOrDefaultAsync(u => u.DiscordId == discordId);
            if (dbUser != null)
            {
                isAdmin = dbUser.IsBotAdmin;
                if (string.IsNullOrEmpty(name))
                    name = dbUser.Username;
            }
        }
        
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
            Roles = roles
        });
        }
        catch
        {
            return StatusCode(500, new { error = "An error occurred while retrieving user information" });
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
            Username = User.FindFirst("urn:discord:username")?.Value 
                    ?? User.FindFirst("urn:discord:global_name")?.Value 
                    ?? User.Identity?.Name,
            UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            IsAdmin = User.HasClaim("is_admin", "true")
        });
    }
}
