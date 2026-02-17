using System.Net.Http.Json;
using System.Text;
using HengcordTCG.Shared.Models;
using HengcordTCG.Shared.DTOs.Wiki;

namespace HengcordTCG.Shared.Clients;

public class HengcordTCGClient
{
    private readonly HttpClient _http;

    public string BaseUrl => _http.BaseAddress?.ToString().TrimEnd('/') ?? "";

    public HengcordTCGClient(HttpClient http)
    {
        _http = http;
    }

    // --- Users ---
    public async Task<GameStats> GetStatsAsync()
    {
        try { return await _http.GetFromJsonAsync<GameStats>("api/users/stats") ?? new(); }
        catch { return new(); }
    }

    public class GameStats
    {
        public int TotalCards { get; set; }
        public int TotalPacks { get; set; }
        public int TotalTrades { get; set; }
        public int TotalPlayers { get; set; }
    }

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

    public async Task<DailyResponse> ClaimDailyWebAsync()
    {
        try
        {
            var response = await _http.PostAsync("me/daily", null);
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
    public async Task<List<Trade>> GetTradesForUserAsync(ulong discordId)
    {
        try { return await _http.GetFromJsonAsync<List<Trade>>($"api/trades/user/{discordId}") ?? new(); }
        catch { return new(); }
    }

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

    public async Task<bool> UpdateCardAsync(Card card)
    {
        var response = await _http.PutAsJsonAsync($"api/admin/update-card/{card.Id}", card);
        return response.IsSuccessStatusCode;
    }

    public async Task<string?> UploadImageAsync(Stream fileStream, string fileName, string folder = "cards")
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fileStream), "file", fileName);
        var response = await _http.PostAsync($"api/images/upload?folder={folder}", content);
        if (!response.IsSuccessStatusCode) return null;
        var result = await response.Content.ReadFromJsonAsync<UploadResult>();
        return result?.Path;
    }

    public record UploadResult(string Path);

    public async Task<List<User>> GetUsersAsync()
    {
        try { return await _http.GetFromJsonAsync<List<User>>("api/users") ?? new(); }
        catch { return new(); }
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

    public async Task<bool> UpdatePackAsync(PackType pack)
    {
        var response = await _http.PutAsJsonAsync($"api/admin/update-pack/{pack.Id}", pack);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RemovePackAsync(int packId)
    {
        var response = await _http.DeleteAsync($"api/admin/remove-pack/{packId}");
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

    // --- Decks ---
    public async Task<Deck?> GetDeckAsync(ulong discordId)
    {
        try { return await _http.GetFromJsonAsync<Deck>($"api/decks/{discordId}"); }
        catch { return null; }
    }

    public record SaveDeckRequest(
        ulong DiscordId,
        string? Name,
        int CommanderId,
        List<int> MainDeckCardIds,
        List<int> CloserCardIds
    );

    public record SaveDeckResponse(string Message);

    public async Task<(bool Success, string Message)> SaveDeckAsync(SaveDeckRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/decks/save", request);
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<SaveDeckResponse>();
            return (true, result?.Message ?? "Deck saved!");
        }
        var error = await response.Content.ReadAsStringAsync();
        return (false, error);
    }

    // --- Match Results ---
    public record SaveMatchRequest(ulong WinnerDiscordId, ulong LoserDiscordId, int Turns, int WinnerHpRemaining);

    public async Task<bool> SaveMatchAsync(SaveMatchRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/matchresults", request);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public record PlayerStatsResponse(
        int Wins, int Losses, int TotalGames, double WinRate,
        int BestHpRemaining, int ShortestWin,
        List<RecentMatchInfo> RecentMatches
    );

    public record RecentMatchInfo(string OpponentName, bool Won, int Turns, int HpRemaining, DateTime FinishedAt);

    public async Task<PlayerStatsResponse?> GetPlayerStatsAsync(ulong discordId)
    {
        try { return await _http.GetFromJsonAsync<PlayerStatsResponse>($"api/matchresults/stats/{discordId}"); }
        catch { return null; }
    }

    public record LeaderboardEntry(string Username, ulong DiscordId, int Wins, int Losses, double WinRate);

    public async Task<List<LeaderboardEntry>> GetLeaderboardAsync(int top = 10)
    {
        try { return await _http.GetFromJsonAsync<List<LeaderboardEntry>>($"api/matchresults/leaderboard?top={top}") ?? new(); }
        catch { return new(); }
    }

    // --- Wiki Proposals ---
    public async Task<List<WikiProposalListDto>> GetWikiProposalsAsync()
    {
        try { return await _http.GetFromJsonAsync<List<WikiProposalListDto>>("api/wiki/proposals") ?? new(); }
        catch { return new(); }
    }

    public async Task<bool> ApproveWikiProposalAsync(int proposalId)
    {
        var response = await _http.PostAsync($"api/wiki/proposals/{proposalId}/approve", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RejectWikiProposalAsync(int proposalId, string reason)
    {
        var response = await _http.PostAsJsonAsync($"api/wiki/proposals/{proposalId}/reject", new { Reason = reason });
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> CreateWikiPageAsync(string title, string slug, string content)
    {
        var response = await _http.PostAsJsonAsync("api/wiki", new { Title = title, Slug = slug, Content = content });
        return response.IsSuccessStatusCode;
    }
}
