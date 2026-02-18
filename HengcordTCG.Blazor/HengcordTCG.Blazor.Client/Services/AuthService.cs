using System.Net.Http.Json;
using HengcordTCG.Blazor.Client.DTOs;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace HengcordTCG.Blazor.Client.Services;

public class AuthService
{
    private readonly HttpClient _http;
    private readonly AuthStateProvider _authStateProvider;
    private readonly IJSRuntime _jsRuntime;
    private const string TokenKey = "jwt_token";

    public AuthService(HttpClient http, AuthStateProvider authStateProvider, IJSRuntime jsRuntime)
    {
        _http = http;
        _authStateProvider = authStateProvider;
        _jsRuntime = jsRuntime;
    }

    public async Task<UserInfo?> InitializeAsync()
    {
        // Check for legacy token in localStorage (for migration)
        var legacyToken = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", TokenKey);
        if (!string.IsNullOrEmpty(legacyToken))
        {
            // Set Authorization header for backward compatibility during this session
            _http.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", legacyToken);
            
            // Clear legacy token from localStorage (we use cookies now)
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenKey);
        }
        
        var user = await GetCurrentUserAsync();
        if (user != null)
        {
            user.Username = user.Username == "Unknown" ? $"User_{user.Id}" : user.Username;
            _authStateProvider.SetAuthenticated(user);
            return user;
        }
        
        _authStateProvider.SetAnonymous();
        return null;
    }

    public string GetDiscordLoginUrl()
    {
        var serverUrl = _http.BaseAddress?.ToString().TrimEnd('/') ?? "https://localhost:7156";
        var blazorUrl = _http.BaseAddress?.ToString().TrimEnd('/') ?? "https://localhost:5001";
        var returnUrl = Uri.EscapeDataString($"{blazorUrl}/login");
        return $"{serverUrl}/login?returnUrl={returnUrl}";
    }

    public async Task SetTokenAsync(string token)
    {
        // For backward compatibility with legacy token-based auth
        _http.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", TokenKey, token);
    }

    public async Task ClearTokenAsync()
    {
        _http.DefaultRequestHeaders.Authorization = null;
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenKey);
    }

    public string? GetToken() => null; // Token is now in HttpOnly cookie

    public async Task<bool> CheckAuthStatusAsync()
    {
        try
        {
            var response = await _http.GetAsync("api/auth/me");
            if (response.IsSuccessStatusCode)
            {
                var authInfo = await response.Content.ReadFromJsonAsync<AuthInfo>();
                if (authInfo?.IsAuthenticated == true)
                {
                    var userId = !string.IsNullOrEmpty(authInfo.UserId) && ulong.TryParse(authInfo.UserId, out var parsedId) 
                        ? parsedId 
                        : 0;
                    
                    long gold = 0;
                    try
                    {
                        var userResponse = await _http.GetAsync($"api/users/{userId}");
                        if (userResponse.IsSuccessStatusCode)
                        {
                            var userData = await userResponse.Content.ReadFromJsonAsync<UserDetails>();
                            if (userData != null)
                            {
                                gold = userData.Gold;
                            }
                        }
                    }
                    catch { }
                    
                    var user = new UserInfo
                    {
                        Id = userId,
                        Username = authInfo.Name ?? "Unknown",
                        AvatarUrl = authInfo.AvatarUrl,
                        IsBotAdmin = authInfo.IsAdmin,
                        Gold = gold
                    };
                    _authStateProvider.SetAuthenticated(user);
                    return true;
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
                    ulong userId = 0;
                    if (!string.IsNullOrEmpty(authInfo.UserId) && ulong.TryParse(authInfo.UserId, out var parsedId))
                    {
                        userId = parsedId;
                    }
                    
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
                    catch { }
                    
                    return new UserInfo
                    {
                        Id = userId,
                        Username = authInfo.Name ?? $"User_{userId}",
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

    public UserInfo? GetCurrentUser() => _authStateProvider.GetCurrentUser();
}
