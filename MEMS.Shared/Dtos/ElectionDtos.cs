using System.ComponentModel.DataAnnotations;
using MEMS.Shared.Validation;

namespace MEMS.Shared.Dtos;

public sealed record PositionDto(
    int Id,
    string Name,
    string? Description,
    int SeatsAvailable,
    int MunicipalityId,
    string MunicipalityName,
    string MunicipalitySlug,
    int CandidateCount);

public sealed record CandidateDto(
    int Id,
    string Name,
    string? Profile,
    string? Website,
    string? Email,
    Guid? PhotoImageId,
    int PositionId,
    string PositionName,
    int SeatsAvailable,
    int MunicipalityId,
    string MunicipalityName,
    string MunicipalitySlug,
    DateTime DateCreated);

public sealed record DashboardDto(int MunicipalityCount, int PositionCount, int CandidateCount);

public class PositionInput
{
    [Required]
    [StringLength(FieldLengths.PositionName)]
    public string Name { get; set; } = string.Empty;

    [StringLength(FieldLengths.PositionDescription)]
    public string? Description { get; set; }

    [Range(FieldLengths.MinSeats, FieldLengths.MaxSeats)]
    [Display(Name = "Seats Available")]
    public int SeatsAvailable { get; set; } = 1;

    // Only used for a Super Admin. A Municipality Admin's positions always go to their own tenant,
    // whatever value is sent here.
    [Display(Name = "Municipality")]
    public int? MunicipalityId { get; set; }
}

public class CandidateInput
{
    [Required]
    [StringLength(FieldLengths.PersonName)]
    public string Name { get; set; } = string.Empty;

    [StringLength(FieldLengths.Profile)]
    [Display(Name = "Biography")]
    public string? Profile { get; set; }

    [Url]
    [StringLength(FieldLengths.Url)]
    public string? Website { get; set; }

    [EmailAddress]
    [StringLength(FieldLengths.Email)]
    public string? Email { get; set; }

    // The candidate's tenant is taken from the position, never from the client.
    [Required(ErrorMessage = "Please choose a position.")]
    [Range(1, int.MaxValue, ErrorMessage = "Please choose a position.")]
    [Display(Name = "Position")]
    public int? PositionId { get; set; }
}

public sealed record CandidateQuery(string? Search = null, int? PositionId = null);
