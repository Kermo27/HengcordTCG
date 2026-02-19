using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using HengcordTCG.Shared.DTOs.Auth;

namespace HengcordTCG.Blazor.Client.Services;

public class AuthStateProvider : AuthenticationStateProvider
{
    private UserInfo? _currentUser;

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (_currentUser == null)
        {
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _currentUser.Id.ToString()),
            new(ClaimTypes.Name, _currentUser.Username),
        };

        if (_currentUser.IsBotAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        var identity = new ClaimsIdentity(claims, "Discord");
        var user = new ClaimsPrincipal(identity);
        return Task.FromResult(new AuthenticationState(user));
    }

    public void SetAuthenticated(UserInfo user)
    {
        _currentUser = user;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public void SetAnonymous()
    {
        _currentUser = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public UserInfo? GetCurrentUser() => _currentUser;
}

public static class AuthPolicy
{
    public const string Admin = "Admin";
    public const string User = "User";
}
