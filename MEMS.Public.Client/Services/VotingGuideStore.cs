using System.Text.Json;
using MEMS.Shared.Dtos;
using Microsoft.JSInterop;

namespace MEMS.Public.Client.Services;

// The voter's personal voting guide.
//
// ELECTION PRIVACY (spec "Election Privacy and Vote Confidentiality"):
// * Stored ONLY in the browser's sessionStorage (native JavaScript API via JS interop).
//   It disappears automatically when the tab/browser session ends.
// * Never written to SQL Server and never sent to the API: this class has no HttpClient, and the
//   API client has no write methods. Only candidate/position ids are stored.
// * Administrators cannot see it - it never leaves the voter's browser.
//
// Rules:
// * A guide belongs to exactly one municipality (tenant). Adding a candidate of another municipality
//   requires the voter to confirm replacing the existing guide.
// * At most SeatsAvailable candidates can be chosen per position.
public sealed class VotingGuideStore(IJSRuntime js)
{
    private const string StorageKey = "mems.votingGuide";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public event Action? Changed;

    public async Task<VotingGuide> GetAsync()
    {
        try
        {
            var json = await js.InvokeAsync<string?>("sessionStorage.getItem", StorageKey);
            return string.IsNullOrEmpty(json) ? new VotingGuide() : JsonSerializer.Deserialize<VotingGuide>(json, Json) ?? new VotingGuide();
        }
        catch (JsonException)
        {
            // Corrupted / tampered value: start over.
            await ClearAsync();
            return new VotingGuide();
        }
    }

    // Tries to add a candidate. Nothing is changed when the result is not Added.
    public async Task<GuideAddResult> AddAsync(CandidateDto candidate, bool replaceOtherMunicipality = false)
    {
        var guide = await GetAsync();

        if (guide.Items.Count > 0 && guide.TenantSlug != candidate.MunicipalitySlug)
        {
            if (!replaceOtherMunicipality)
            {
                return GuideAddResult.OtherMunicipality(guide.MunicipalityName ?? guide.TenantSlug ?? "another municipality");
            }
            guide = new VotingGuide();
        }

        if (guide.Items.Any(i => i.CandidateId == candidate.Id))
        {
            return GuideAddResult.AlreadyInGuide();
        }

        var chosenForPosition = guide.Items.Count(i => i.PositionId == candidate.PositionId);
        if (chosenForPosition >= candidate.SeatsAvailable)
        {
            return GuideAddResult.SeatsFull(candidate.PositionName, candidate.SeatsAvailable);
        }

        guide.TenantSlug = candidate.MunicipalitySlug;
        guide.MunicipalityName = candidate.MunicipalityName;
        guide.Items.Add(new GuideItem(candidate.Id, candidate.PositionId));
        await SaveAsync(guide);
        return GuideAddResult.Added();
    }

    public async Task RemoveAsync(int candidateId)
    {
        var guide = await GetAsync();
        if (guide.Items.RemoveAll(i => i.CandidateId == candidateId) > 0)
        {
            await SaveAsync(guide);
        }
    }

    // Drops ids that no longer exist (candidate deleted by an administrator since it was added).
    public async Task RemoveMissingAsync(string tenantSlug, IReadOnlySet<int> existingCandidateIds)
    {
        var guide = await GetAsync();
        if (guide.TenantSlug == tenantSlug && guide.Items.RemoveAll(i => !existingCandidateIds.Contains(i.CandidateId)) > 0)
        {
            await SaveAsync(guide);
        }
    }

    public async Task ClearAsync()
    {
        await js.InvokeVoidAsync("sessionStorage.removeItem", StorageKey);
        Changed?.Invoke();
    }

    private async Task SaveAsync(VotingGuide guide)
    {
        if (guide.Items.Count == 0)
        {
            await ClearAsync();
            return;
        }
        await js.InvokeVoidAsync("sessionStorage.setItem", StorageKey, JsonSerializer.Serialize(guide, Json));
        Changed?.Invoke();
    }
}

public sealed class VotingGuide
{
    public string? TenantSlug { get; set; }
    public string? MunicipalityName { get; set; }
    public List<GuideItem> Items { get; set; } = [];

    public bool BelongsTo(string slug) => Items.Count > 0 && TenantSlug == slug;

    public bool Contains(int candidateId) => Items.Any(i => i.CandidateId == candidateId);

    public int CountFor(int positionId) => Items.Count(i => i.PositionId == positionId);
}

public sealed record GuideItem(int CandidateId, int PositionId);

public enum GuideAddStatus
{
    Added,
    AlreadyInGuide,
    SeatsFull,
    OtherMunicipality,
}

public sealed record GuideAddResult(GuideAddStatus Status, string? Detail = null, int Seats = 0)
{
    public static GuideAddResult Added() => new(GuideAddStatus.Added);
    public static GuideAddResult AlreadyInGuide() => new(GuideAddStatus.AlreadyInGuide);
    public static GuideAddResult SeatsFull(string positionName, int seats) => new(GuideAddStatus.SeatsFull, positionName, seats);
    public static GuideAddResult OtherMunicipality(string municipalityName) => new(GuideAddStatus.OtherMunicipality, municipalityName);
}
