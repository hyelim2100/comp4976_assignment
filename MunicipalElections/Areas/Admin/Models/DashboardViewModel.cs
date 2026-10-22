namespace MunicipalElections.Areas.Admin.Models;

public class DashboardViewModel
{
    public bool IsSuperAdmin { get; set; }

    public int MunicipalityCount { get; set; }

    public int PositionCount { get; set; }

    public int CandidateCount { get; set; }

    public IReadOnlyList<MunicipalitySummary> Municipalities { get; set; } = [];
}

public record MunicipalitySummary(int MunicipalityId, string Name, DateTime ElectionDate, string? LogoFileName, int PositionCount, int CandidateCount);
