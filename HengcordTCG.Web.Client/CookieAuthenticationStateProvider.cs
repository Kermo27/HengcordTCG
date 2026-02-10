using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Json;
using System.Security.Claims;

namespace HengcordTCG.Web.Client;

public class CookieAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly HttpClient _http;

    public CookieAuthenticationStateProvider(HttpClient http)
    {
        _http = http;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var result = await _http.GetFromJsonAsync<AuthMeResponse>("auth/me");
            if (result is { IsAuthenticated: true })
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, result.Name ?? string.Empty)
                };

                if (!string.IsNullOrEmpty(result.AvatarUrl))
                {
                    claims.Add(new Claim("avatar_url", result.AvatarUrl));
                }

                if (result.IsAdmin)
                {
                    claims.Add(new Claim("is_admin", "true"));
                }

                var identity = new ClaimsIdentity(claims, "Cookies");
                var user = new ClaimsPrincipal(identity);
                return new AuthenticationState(user);
            }
        }
        catch
        {
            // ignore and fall back to anonymous
        }

        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        return new AuthenticationState(anonymous);
    }

    private sealed record AuthMeResponse(bool IsAuthenticated, string? Name, string? AvatarUrl, bool IsAdmin);
}

