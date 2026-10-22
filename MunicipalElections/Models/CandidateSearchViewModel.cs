using Microsoft.AspNetCore.Mvc.Rendering;

namespace MunicipalElections.Models;

public class CandidateSearchViewModel
{
    public string? Search { get; set; }

    public int? MunicipalityId { get; set; }

    public int? PositionId { get; set; }

    public IReadOnlyList<Candidate> Candidates { get; set; } = [];

    public SelectList MunicipalityOptions { get; set; } = new(Array.Empty<object>());

    public IReadOnlyList<PositionOption> PositionOptions { get; set; } = [];

    public bool IsFiltered => !string.IsNullOrWhiteSpace(Search) || MunicipalityId is not null || PositionId is not null;
}
