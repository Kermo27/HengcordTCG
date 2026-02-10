using System.Net.Http.Json;
using System.Text;
using HengcordTCG.Shared.Models;

namespace HengcordTCG.Shared.Clients;

public class HengcordTCGClient
{
    private readonly HttpClient _http;

    public HengcordTCGClient(HttpClient http)
    {
        _http = http;
    }

    // --- Users ---
    public async Task<User?> GetUserAsync(ulong discordId)
    {
        try 
        {
            return await _http.GetFromJsonAsync<User>($"api/users/{discordId}");
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"HTTP request failed: {ex.Message}");
            return null;
        }
    }

    public async Task<User?> GetOrCreateUserAsync(ulong discordId, string username)
    {
        try
        {
            var content = new StringContent("", Encoding.UTF8, "application/json");
            var response = await _http.PostAsync($"api/users/{discordId}/sync?username={Uri.EscapeDataString(username)}", content);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<User>();
        }
        catch
        {
            return null;
        }
    }

    public record DailyResponse(bool Success, int Amount, string? TimeRemaining);

    public async Task<DailyResponse> ClaimDailyAsync(ulong discordId, string username)
    {
        try 
        {
            var content = new StringContent("", Encoding.UTF8, "application/json");
            var response = await _http.PostAsync($"api/users/{discordId}/daily?username={Uri.EscapeDataString(username)}", content);
            return await response.Content.ReadFromJsonAsync<DailyResponse>() ?? new DailyResponse(false, 0, null);
        }
        catch { return new DailyResponse(false, 0, null); }
    }

    public async Task<long> GetBalanceAsync(ulong discordId)
    {
        var user = await GetUserAsync(discordId);
        return user?.Gold ?? 0;
    }

    // --- Shop ---
    public async Task<List<PackType>> GetPacksAsync()
    {
        try { return await _http.GetFromJsonAsync<List<PackType>>("api/shop/packs") ?? new(); }
        catch { return new(); }
    }

    public async Task<(bool success, List<Card>? cards, string? message)> BuyPackAsync(ulong discordId, string username, string packName)
    {
        var response = await _http.PostAsync($"api/shop/buy-pack?discordId={discordId}&username={Uri.EscapeDataString(username)}&packName={Uri.EscapeDataString(packName)}", null);
        
        if (response.IsSuccessStatusCode)
        {
            var cards = await response.Content.ReadFromJsonAsync<List<Card>>();
            return (true, cards, "Paczka otwarta!");
        }
        
        var message = await response.Content.ReadAsStringAsync();
        return (false, null, message);
    }

    // --- Cards ---
    public async Task<List<Card>> GetCardsAsync()
    {
        try { return await _http.GetFromJsonAsync<List<Card>>("api/cards") ?? new(); }
        catch { return new(); }
    }

    // --- Collections ---
    public async Task<List<UserCard>> GetCollectionAsync(ulong discordId)
    {
        try { return await _http.GetFromJsonAsync<List<UserCard>>($"api/collections/{discordId}") ?? new(); }
        catch { return new(); }
    }

    // --- Trades ---
    public record CreateTradeRequest(
        ulong InitiatorId, string InitiatorName,
        ulong TargetId, string TargetName,
        string Offer, string Request
    );

    public record TradeResponse(
        bool Success,
        string Message,
        Trade? Trade,
        TradeContent? OfferContent, 
        TradeContent? RequestContent
    );

    public async Task<TradeResponse> CreateTradeAsync(CreateTradeRequest req)
    {
        var response = await _http.PostAsJsonAsync("api/trades/create", req);
        return await response.Content.ReadFromJsonAsync<TradeResponse>() 
               ?? new TradeResponse(false, "Błąd komunikacji z serwerem", null, null, null);
    }

    public async Task<(bool Success, string Message)> AcceptTradeAsync(int tradeId, ulong userId)
    {
        var response = await _http.PostAsync($"api/trades/{tradeId}/accept?userId={userId}", null);
        var message = await response.Content.ReadAsStringAsync();
        return (response.IsSuccessStatusCode, message);
    }

    public async Task<(bool Success, string Message)> RejectTradeAsync(int tradeId, ulong userId)
    {
        var response = await _http.PostAsync($"api/trades/{tradeId}/reject?userId={userId}", null);
        var message = await response.Content.ReadAsStringAsync();
        return (response.IsSuccessStatusCode, message);
    }

    // --- Admin ---
    public async Task<bool> AddCardAsync(Card card)
    {
        var response = await _http.PostAsJsonAsync("api/admin/add-card", card);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RemoveCardAsync(string name)
    {
        var response = await _http.DeleteAsync($"api/admin/remove-card/{Uri.EscapeDataString(name)}");
        return response.IsSuccessStatusCode;
    }

    public async Task<long> GiveGoldAdminAsync(ulong discordId, int amount)
    {
        var response = await _http.PostAsync($"api/admin/give-gold?discordId={discordId}&amount={amount}", null);
        if (response.IsSuccessStatusCode) return await response.Content.ReadFromJsonAsync<long>();
        return -1;
    }

    public async Task<long> SetGoldAdminAsync(ulong discordId, int amount)
    {
        var response = await _http.PostAsync($"api/admin/set-gold?discordId={discordId}&amount={amount}", null);
        if (response.IsSuccessStatusCode) return await response.Content.ReadFromJsonAsync<long>();
        return -1;
    }

    public async Task<bool> CreatePackAsync(PackType pack)
    {
        var response = await _http.PostAsJsonAsync("api/admin/create-pack", pack);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> TogglePackAsync(string name)
    {
        var response = await _http.PostAsync($"api/admin/toggle-pack/{Uri.EscapeDataString(name)}", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SetCardPackAsync(string cardName, string packName)
    {
        var response = await _http.PostAsync($"api/admin/set-card-pack?cardName={Uri.EscapeDataString(cardName)}&packName={Uri.EscapeDataString(packName)}", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> GiveCardAsync(ulong discordId, string cardName, int amount)
    {
        var response = await _http.PostAsync($"api/admin/give-card?discordId={discordId}&cardName={Uri.EscapeDataString(cardName)}&amount={amount}", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> FixInventoryAsync()
    {
        var response = await _http.PostAsync("api/admin/fix-inventory", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> AddAdminAsync(ulong discordId)
    {
        var response = await _http.PostAsync($"api/admin/add-admin/{discordId}", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RemoveAdminAsync(ulong discordId)
    {
        var response = await _http.PostAsync($"api/admin/remove-admin/{discordId}", null);
        return response.IsSuccessStatusCode;
    }
}
