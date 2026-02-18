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
        var token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", TokenKey);
        if (!string.IsNullOrEmpty(token))
        {
            _jwtToken = token;
            _http.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            
                var user = await GetCurrentUserAsync();
                if (user != null)
                {
                    user.Username = user.Username == "Unknown" ? $"User_{user.Id}" : user.Username;
                    _authStateProvider.SetAuthenticated(user);
                    return user;
                }
            else
            {
                await LogoutAsync();
            }
        }
        return null;
    }

    public string GetDiscordLoginUrl()
    {
        var serverUrl = _http.BaseAddress?.ToString().TrimEnd('/') ?? "https://localhost:7156";
        var blazorUrl = "https://localhost:5001";
        var returnUrl = Uri.EscapeDataString($"{blazorUrl}/login");
        return $"{serverUrl}/login?returnUrl={returnUrl}";
    }

    public async Task SetTokenAsync(string token)
    {
        _jwtToken = token;
        _http.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", TokenKey, token);
    }

    public async Task ClearTokenAsync()
    {
        _jwtToken = null;
        _http.DefaultRequestHeaders.Authorization = null;
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenKey);
    }

    public string? GetToken() => _jwtToken;

    private static bool ExtractIsAdminFromToken(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length >= 2)
            {
                var payload = parts[1];
                var padding = 4 - payload.Length % 4;
                if (padding != 4) payload += new string('=', padding);
                
                var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("role", out var role) && role.GetString() == "Admin")
                    return true;
                if (root.TryGetProperty("is_admin", out var isAdmin) && isAdmin.GetString() == "true")
                    return true;
            }
        }
        catch { }
        return false;
    }

    private static ulong ExtractUserIdFromToken(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length >= 2)
            {
                var payload = parts[1];
                var padding = 4 - payload.Length % 4;
                if (padding != 4) payload += new string('=', padding);
                
                var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("sub", out var sub))
                {
                    if (ulong.TryParse(sub.GetString(), out var id))
                        return id;
                }
                if (root.TryGetProperty("nameid", out var nameid))
                {
                    if (ulong.TryParse(nameid.GetString(), out var id))
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
            if (!string.IsNullOrEmpty(_jwtToken))
            {
                var response = await _http.GetAsync("api/auth/me");
                if (response.IsSuccessStatusCode)
                {
                    var authInfo = await response.Content.ReadFromJsonAsync<AuthInfo>();
                    if (authInfo?.IsAuthenticated == true)
                    {
                        var userId = !string.IsNullOrEmpty(authInfo.UserId) && ulong.TryParse(authInfo.UserId, out var parsedId) 
                            ? parsedId 
                            : ExtractUserIdFromToken(_jwtToken);
                        
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
                            IsBotAdmin = authInfo.IsAdmin || ExtractIsAdminFromToken(_jwtToken ?? ""),
                            Gold = gold
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
                    ulong userId = 0;
                    if (!string.IsNullOrEmpty(authInfo.UserId) && ulong.TryParse(authInfo.UserId, out var parsedId))
                    {
                        userId = parsedId;
                    }
                    else if (!string.IsNullOrEmpty(_jwtToken))
                    {
                        userId = ExtractUserIdFromToken(_jwtToken);
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
                        IsBotAdmin = authInfo.IsAdmin || ExtractIsAdminFromToken(_jwtToken ?? ""),
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
