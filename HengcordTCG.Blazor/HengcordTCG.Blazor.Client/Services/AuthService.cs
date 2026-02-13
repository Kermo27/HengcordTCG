using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace HengcordTCG.Blazor.Client.Services;

public class AuthService
{
    private readonly HttpClient _http;
    private readonly AuthStateProvider _authStateProvider;
    private readonly IJSRuntime _jsRuntime;
    private string? _jwtToken;
    private const string TokenKey = "jwt_token";

    public AuthService(HttpClient http, AuthStateProvider authStateProvider, IJSRuntime jsRuntime)
    {
        _http = http;
        _authStateProvider = authStateProvider;
        _jsRuntime = jsRuntime;
    }

    public async Task<UserInfo?> InitializeAsync()
    {
        // Load token from localStorage on startup
        var token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", TokenKey);
        if (!string.IsNullOrEmpty(token))
        {
            _jwtToken = token;
            _http.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            
            // Fetch user info and update auth state
            var user = await GetCurrentUserAsync();
            if (user != null)
            {
                _authStateProvider.SetAuthenticated(user);
                return user;
            }
            else
            {
                // Token is invalid, clear it
                await LogoutAsync();
            }
        }
        return null;
    }

    public string GetDiscordLoginUrl()
    {
        // The Server handles Discord OAuth
        // Redirect to the server's auth endpoint with return URL back to Blazor
        var serverUrl = _http.BaseAddress?.ToString().TrimEnd('/') ?? "https://localhost:7156";
        var blazorUrl = "https://localhost:5001";
        var returnUrl = Uri.EscapeDataString($"{blazorUrl}/login");
        return $"{serverUrl}/login?returnUrl={returnUrl}";
    }

    public async Task SetTokenAsync(string token)
    {
        _jwtToken = token;
        // Add JWT to HttpClient default headers for subsequent requests
        _http.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        // Persist to localStorage
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", TokenKey, token);
    }

    public async Task ClearTokenAsync()
    {
        _jwtToken = null;
        _http.DefaultRequestHeaders.Authorization = null;
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenKey);
    }

    public string? GetToken() => _jwtToken;

    private ulong ExtractUserIdFromToken(string token)
    {
        try
        {
            // Simple JWT parsing - split by . and decode payload
            var parts = token.Split('.');
            if (parts.Length >= 2)
            {
                var payload = parts[1];
                // Add padding if needed
                var padding = 4 - payload.Length % 4;
                if (padding != 4) payload += new string('=', padding);
                
                var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                // Find nameid claim - look for "nameid":"12345"
                var match = System.Text.RegularExpressions.Regex.Match(json, @"""nameid"""""":\s*""""""""""(\d+)""""""");
                if (match.Success && ulong.TryParse(match.Groups[1].Value, out var id))
                {
                    return id;
                }
            }
        }
        catch { }
        return 0;
    }

    public async Task<bool> CheckAuthStatusAsync()
    {
        try
        {
            // If we have a JWT token, validate it by calling the API
            if (!string.IsNullOrEmpty(_jwtToken))
            {
                var response = await _http.GetAsync("api/auth/me");
                if (response.IsSuccessStatusCode)
                {
                    var authInfo = await response.Content.ReadFromJsonAsync<AuthInfo>();
                    if (authInfo?.IsAuthenticated == true)
                    {
                        // Extract user ID from JWT token payload (simple base64 decode)
                        var userId = ExtractUserIdFromToken(_jwtToken);
                        
                        var user = new UserInfo
                        {
                            Id = userId,
                            Username = authInfo.Name ?? "Unknown",
                            AvatarUrl = authInfo.AvatarUrl,
                            IsBotAdmin = authInfo.IsAdmin
                        };
                        _authStateProvider.SetAuthenticated(user);
                        return true;
                    }
                }
            }

            _authStateProvider.SetAnonymous();
            return false;
        }
        catch
        {
            _authStateProvider.SetAnonymous();
            return false;
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            await _http.PostAsync("api/auth/logout", null);
        }
        catch { }
        await ClearTokenAsync();
        _authStateProvider.SetAnonymous();
    }

    public async Task<UserInfo?> GetCurrentUserAsync()
    {
        try
        {
            var response = await _http.GetAsync("api/auth/me");
            if (response.IsSuccessStatusCode)
            {
                var authInfo = await response.Content.ReadFromJsonAsync<AuthInfo>();
                if (authInfo?.IsAuthenticated == true)
                {
                    // Use user ID from server response (more reliable than JWT parsing)
                    ulong userId = 0;
                    if (!string.IsNullOrEmpty(authInfo.UserId) && ulong.TryParse(authInfo.UserId, out var parsedId))
                    {
                        userId = parsedId;
                    }
                    // Fallback to JWT parsing if server doesn't return user ID
                    else if (!string.IsNullOrEmpty(_jwtToken))
                    {
                        userId = ExtractUserIdFromToken(_jwtToken);
                    }
                    
                    // Fetch user details (including gold) from users API
                    long gold = 0;
                    DateTime? lastDaily = null;
                    try
                    {
                        var userResponse = await _http.GetAsync($"api/users/{userId}");
                        if (userResponse.IsSuccessStatusCode)
                        {
                            var userData = await userResponse.Content.ReadFromJsonAsync<UserDetails>();
                            if (userData != null)
                            {
                                gold = userData.Gold;
                                lastDaily = userData.LastDaily;
                            }
                        }
                    }
                    catch { /* Ignore errors, use defaults */ }
                    
                    return new UserInfo
                    {
                        Id = userId,
                        Username = authInfo.Name ?? "Unknown",
                        AvatarUrl = authInfo.AvatarUrl,
                        IsBotAdmin = authInfo.IsAdmin,
                        Gold = gold,
                        LastDaily = lastDaily
                    };
                }
            }
        }
        catch { }
        return null;
    }
}

public class UserInfo
{
    public ulong Id { get; set; }
    public string Username { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public long Gold { get; set; }
    public bool IsBotAdmin { get; set; }
    public DateTime? LastDaily { get; set; }
}

public class AuthInfo
{
    public bool IsAuthenticated { get; set; }
    public string? Name { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsAdmin { get; set; }
    public string? UserId { get; set; }
}

public class UserDetails
{
    public ulong Id { get; set; }
    public ulong DiscordId { get; set; }
    public string Username { get; set; } = "";
    public long Gold { get; set; }
    public bool IsBotAdmin { get; set; }
    public DateTime? LastDaily { get; set; }
}

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
