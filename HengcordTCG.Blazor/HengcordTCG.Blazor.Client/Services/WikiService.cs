using System.Net.Http.Json;
using HengcordTCG.Shared.DTOs.Wiki;

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

    public async Task<List<WikiPageDto>> GetPagesAsync()
    {
        await EnsureAuthHeader();
        var response = await _http.GetAsync("api/wiki");
        return await response.Content.ReadFromJsonAsync<List<WikiPageDto>>() ?? new();
    }

    public async Task<List<WikiPageTreeDto>> GetTreeAsync()
    {
        await EnsureAuthHeader();
        var response = await _http.GetAsync("api/wiki/tree");
        return await response.Content.ReadFromJsonAsync<List<WikiPageTreeDto>>() ?? new();
    }

    public async Task<WikiPageDetailDto?> GetPageAsync(string slug)
    {
        await EnsureAuthHeader();
        var response = await _http.GetAsync($"api/wiki/{slug}");
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<WikiPageDetailDto>();
    }

    public async Task<List<WikiPageDto>> SearchAsync(string query)
    {
        await EnsureAuthHeader();
        var response = await _http.GetAsync($"api/wiki/search?q={Uri.EscapeDataString(query)}");
        return await response.Content.ReadFromJsonAsync<List<WikiPageDto>>() ?? new();
    }

    public async Task<WikiPageDto> CreatePageAsync(CreateWikiPageRequest request)
    {
        await EnsureAuthHeader();
        var response = await _http.PostAsJsonAsync("api/wiki", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WikiPageDto>() ?? throw new Exception("Failed to create page");
    }

    public async Task<WikiPageDto> UpdatePageAsync(int id, UpdateWikiPageRequest request)
    {
        await EnsureAuthHeader();
        var response = await _http.PutAsJsonAsync($"api/wiki/{id}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WikiPageDto>() ?? throw new Exception("Failed to update page");
    }

    public async Task DeletePageAsync(int id)
    {
        await EnsureAuthHeader();
        var response = await _http.DeleteAsync($"api/wiki/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<WikiHistoryDto>> GetHistoryAsync(int pageId)
    {
        await EnsureAuthHeader();
        var response = await _http.GetAsync($"api/wiki/{pageId}/history");
        return await response.Content.ReadFromJsonAsync<List<WikiHistoryDto>>() ?? new();
    }

    public async Task<WikiProposalDto> CreateProposalAsync(CreateProposalRequest request)
    {
        await EnsureAuthHeader();
        var response = await _http.PostAsJsonAsync("api/wiki/proposals", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WikiProposalDto>() ?? throw new Exception("Failed to create proposal");
    }

    public async Task<List<WikiProposalListDto>> GetProposalsAsync()
    {
        await EnsureAuthHeader();
        var response = await _http.GetAsync("api/wiki/proposals");
        return await response.Content.ReadFromJsonAsync<List<WikiProposalListDto>>() ?? new();
    }

    public async Task<WikiProposalDetailDto?> GetProposalAsync(int id)
    {
        await EnsureAuthHeader();
        var response = await _http.GetAsync($"api/wiki/proposals/{id}");
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<WikiProposalDetailDto>();
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
