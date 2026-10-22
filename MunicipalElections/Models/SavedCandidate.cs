namespace MunicipalElections.Models;

// A candidate saved to a mobile app user's voting guide (COMP4977 Web API).
public class SavedCandidate
{
    public int SavedCandidateId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    public int CandidateId { get; set; }

    public Candidate? Candidate { get; set; }

    public DateTime DateSaved { get; set; } = DateTime.UtcNow;
}
