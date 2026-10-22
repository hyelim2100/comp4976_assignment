using System.Text.Json;

namespace MunicipalElections.Services;

// The anonymous visitor's personal voting guide: the selected candidate ids, kept in session state.
public class VotingGuide(IHttpContextAccessor httpContextAccessor)
{
    private const string SessionKey = "VotingGuide.CandidateIds";

    private ISession Session => httpContextAccessor.HttpContext!.Session;

    public IReadOnlyList<int> CandidateIds =>
        Session.GetString(SessionKey) is { } json ? JsonSerializer.Deserialize<List<int>>(json) ?? [] : [];

    public int Count => CandidateIds.Count;

    public bool Contains(int candidateId) => CandidateIds.Contains(candidateId);

    public void Add(int candidateId)
    {
        var ids = CandidateIds.ToList();
        if (!ids.Contains(candidateId))
        {
            ids.Add(candidateId);
            Save(ids);
        }
    }

    public void Remove(int candidateId) => Save(CandidateIds.Where(id => id != candidateId));

    public void Clear() => Session.Remove(SessionKey);

    // Drops ids of candidates that no longer exist (e.g. deleted by an administrator).
    public void KeepOnly(IEnumerable<int> existingIds)
    {
        var existing = existingIds.ToHashSet();
        var ids = CandidateIds;
        if (ids.Any(id => !existing.Contains(id)))
        {
            Save(ids.Where(existing.Contains));
        }
    }

    private void Save(IEnumerable<int> ids) => Session.SetString(SessionKey, JsonSerializer.Serialize(ids.ToList()));
}
