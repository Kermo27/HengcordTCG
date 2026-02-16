using System.Net.Http.Json;
using HengcordTCG.Blazor.Client.DTOs;
using HengcordTCG.Blazor.Client.Services;

namespace HengcordTCG.Blazor.Client.Services;

public class WikiService
{
    private readonly HttpClient _http;
    private readonly AuthService _authService;

    public WikiService(HttpClient http, AuthService authService)
    {
        _http = http;
        _authService = authService;
    }

    private async Task EnsureAuthHeader()
    {
        var token = _authService.GetToken();
        if (!string.IsNullOrEmpty(token) && _http.DefaultRequestHeaders.Authorization?.Parameter != token)
        {
            _http.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<List<WikiPageItem>> GetPagesAsync()
    {
        await EnsureAuthHeader();
        var response = await _http.GetAsync("api/wiki");
        return await response.Content.ReadFromJsonAsync<List<WikiPageItem>>() ?? new();
    }

    public async Task<List<WikiPageTreeItem>> GetTreeAsync()
    {
        await EnsureAuthHeader();
        var response = await _http.GetAsync("api/wiki/tree");
        return await response.Content.ReadFromJsonAsync<List<WikiPageTreeItem>>() ?? new();
    }

    public async Task<WikiPageDetail?> GetPageAsync(string slug)
    {
        await EnsureAuthHeader();
        var response = await _http.GetAsync($"api/wiki/{slug}");
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<WikiPageDetail>();
    }

    public async Task<List<WikiPageItem>> SearchAsync(string query)
    {
        await EnsureAuthHeader();
        var response = await _http.GetAsync($"api/wiki/search?q={Uri.EscapeDataString(query)}");
        return await response.Content.ReadFromJsonAsync<List<WikiPageItem>>() ?? new();
    }

    public async Task<WikiPageItem> CreatePageAsync(CreateWikiPageRequest request)
    {
        await EnsureAuthHeader();
        var response = await _http.PostAsJsonAsync("api/wiki", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WikiPageItem>() ?? throw new Exception("Failed to create page");
    }

    public async Task<WikiPageItem> UpdatePageAsync(int id, UpdateWikiPageRequest request)
    {
        await EnsureAuthHeader();
        var response = await _http.PutAsJsonAsync($"api/wiki/{id}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WikiPageItem>() ?? throw new Exception("Failed to update page");
    }

    public async Task DeletePageAsync(int id)
    {
        await EnsureAuthHeader();
        var response = await _http.DeleteAsync($"api/wiki/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<WikiHistoryItem>> GetHistoryAsync(int pageId)
    {
        await EnsureAuthHeader();
        var response = await _http.GetAsync($"api/wiki/{pageId}/history");
        return await response.Content.ReadFromJsonAsync<List<WikiHistoryItem>>() ?? new();
    }

    public async Task<WikiProposalItem> CreateProposalAsync(CreateProposalRequest request)
    {
        await EnsureAuthHeader();
        var response = await _http.PostAsJsonAsync("api/wiki/proposals", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WikiProposalItem>() ?? throw new Exception("Failed to create proposal");
    }

    public async Task<List<WikiProposalListItem>> GetProposalsAsync()
    {
        await EnsureAuthHeader();
        var response = await _http.GetAsync("api/wiki/proposals");
        return await response.Content.ReadFromJsonAsync<List<WikiProposalListItem>>() ?? new();
    }

    public async Task<WikiProposalDetail?> GetProposalAsync(int id)
    {
        await EnsureAuthHeader();
        var response = await _http.GetAsync($"api/wiki/proposals/{id}");
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<WikiProposalDetail>();
    }

    public async Task ApproveProposalAsync(int id)
    {
        await EnsureAuthHeader();
        var response = await _http.PostAsync($"api/wiki/proposals/{id}/approve", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task RejectProposalAsync(int id, string reason)
    {
        await EnsureAuthHeader();
        var response = await _http.PostAsJsonAsync($"api/wiki/proposals/{id}/reject", new RejectProposalRequest { Reason = reason });
        response.EnsureSuccessStatusCode();
    }
}
