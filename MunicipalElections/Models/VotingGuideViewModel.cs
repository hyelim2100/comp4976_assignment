namespace MunicipalElections.Models;

// Selected candidates grouped by municipality, then by position.
public class VotingGuideViewModel
{
    public IReadOnlyList<GuideMunicipality> Municipalities { get; set; } = [];

    public int CandidateCount => Municipalities.Sum(m => m.Positions.Sum(p => p.Candidates.Count));

    public bool ShowPhotos { get; set; } = true;

    public int? MunicipalityFilter { get; set; }
}

public record GuideMunicipality(Municipality Municipality, IReadOnlyList<GuidePosition> Positions);

public record GuidePosition(Position Position, IReadOnlyList<Candidate> Candidates)
{
    public bool IsOverSelected => Candidates.Count > Position.SeatsAvailable;
}
