using System.Net;
using System.Net.Http.Json;
using MEMS.Shared.Dtos;

namespace MEMS.Public.Client.Services;

// The public site's only way to get data: read-only GET calls to MEMS.Api (proxied by the MEMS.Public
// host at /api/*). The WebAssembly app never touches the database.
//
// Election privacy: this client has no POST/PUT/DELETE methods at all, so voting-guide selections
// (kept in sessionStorage by VotingGuideStore) cannot be sent to the server.
public sealed class MemsApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<MunicipalitySummaryDto>> GetMunicipalitiesAsync(CancellationToken cancellationToken = default) =>
        await http.GetFromJsonAsync<List<MunicipalitySummaryDto>>("api/municipalities", cancellationToken) ?? [];

    // Null when the slug does not exist (404).
    public Task<MunicipalityDto?> GetMunicipalityAsync(string slug, CancellationToken cancellationToken = default) =>
        GetOrNullAsync<MunicipalityDto>($"api/{Uri.EscapeDataString(slug)}/municipality", cancellationToken);

    public async Task<IReadOnlyList<PositionDto>> GetPositionsAsync(string slug, CancellationToken cancellationToken = default) =>
        await GetOrNullAsync<List<PositionDto>>($"api/{Uri.EscapeDataString(slug)}/positions", cancellationToken) ?? [];

    public async Task<IReadOnlyList<CandidateDto>> GetCandidatesAsync(string slug, string? search = null, int? positionId = null, CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        }
        if (positionId is int id)
        {
            query.Add($"positionId={id}");
        }
        var url = $"api/{Uri.EscapeDataString(slug)}/candidates" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        return await GetOrNullAsync<List<CandidateDto>>(url, cancellationToken) ?? [];
    }

    public Task<CandidateDto?> GetCandidateAsync(string slug, int id, CancellationToken cancellationToken = default) =>
        GetOrNullAsync<CandidateDto>($"api/{Uri.EscapeDataString(slug)}/candidates/{id}", cancellationToken);

    // Images are served by the API per tenant; another tenant's image id returns 404.
    public static string? ImageUrl(string slug, Guid? imageId) =>
        imageId is Guid id ? $"api/{Uri.EscapeDataString(slug)}/images/{id}" : null;

    private async Task<T?> GetOrNullAsync<T>(string url, CancellationToken cancellationToken) where T : class
    {
        using var response = await http.GetAsync(url, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }
}
