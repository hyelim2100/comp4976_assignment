using System.ComponentModel.DataAnnotations;
using MEMS.Shared.Validation;

namespace MEMS.Data.Entities;

public class Position : ITenantOwned
{
    public int Id { get; set; }

    [Required]
    [MaxLength(FieldLengths.PositionName)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(FieldLengths.PositionDescription)]
    public string? Description { get; set; }

    [Range(FieldLengths.MinSeats, FieldLengths.MaxSeats)]
    public int SeatsAvailable { get; set; }

    public int MunicipalityId { get; set; }

    public Municipality? Municipality { get; set; }

    public ICollection<Candidate> Candidates { get; set; } = new List<Candidate>();
}
