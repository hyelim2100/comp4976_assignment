using System.Text.Json;
using Microsoft.JSInterop;

namespace MEMS.Public.Client.Services;

// Candidate comparison (kept from Assignment 1): up to 3 candidates of one municipality, side by side.
// Like the voting guide it lives only in sessionStorage and is never sent to the server. Selecting a
// candidate of another municipality simply starts a new comparison (nothing is lost that matters).
public sealed class CompareStore(IJSRuntime js)
{
    public const int MaxCandidates = 3;
    private const string StorageKey = "mems.compare";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public event Action? Changed;

    public async Task<CompareSelection> GetAsync()
    {
        try
        {
            var json = await js.InvokeAsync<string?>("sessionStorage.getItem", StorageKey);
            return string.IsNullOrEmpty(json) ? new CompareSelection() : JsonSerializer.Deserialize<CompareSelection>(json, Json) ?? new CompareSelection();
        }
        catch (JsonException)
        {
            return new CompareSelection();
        }
    }

    // Returns false when the comparison is already full.
    public async Task<bool> ToggleAsync(string tenantSlug, int candidateId)
    {
        var selection = await GetAsync();
        if (selection.TenantSlug != tenantSlug)
        {
            selection = new CompareSelection { TenantSlug = tenantSlug };
        }

        if (!selection.CandidateIds.Remove(candidateId))
        {
            if (selection.CandidateIds.Count >= MaxCandidates)
            {
                return false;
            }
            selection.CandidateIds.Add(candidateId);
        }

        await js.InvokeVoidAsync("sessionStorage.setItem", StorageKey, JsonSerializer.Serialize(selection, Json));
        Changed?.Invoke();
        return true;
    }

    public async Task ClearAsync()
    {
        await js.InvokeVoidAsync("sessionStorage.removeItem", StorageKey);
        Changed?.Invoke();
    }
}

public sealed class CompareSelection
{
    public string? TenantSlug { get; set; }
    public List<int> CandidateIds { get; set; } = [];

    public IReadOnlyList<int> For(string slug) => TenantSlug == slug ? CandidateIds : [];
}
