using System.ComponentModel.DataAnnotations;
using MEMS.Shared.Validation;

namespace MEMS.Data.Entities;

// MunicipalityId is stored on the candidate for row-level tenant filtering. A composite foreign key
// (PositionId, MunicipalityId) -> Position(Id, MunicipalityId) guarantees in the database that it always
// matches the position's tenant.
public class Candidate : ITenantOwned
{
    public int Id { get; set; }

    [Required]
    [MaxLength(FieldLengths.PersonName)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(FieldLengths.Profile)]
    public string? Profile { get; set; }

    [Url]
    [MaxLength(FieldLengths.Url)]
    public string? Website { get; set; }

    [EmailAddress]
    [MaxLength(FieldLengths.Email)]
    public string? Email { get; set; }

    public Guid? PhotoImageId { get; set; }

    public StoredImage? PhotoImage { get; set; }

    public int PositionId { get; set; }

    public Position? Position { get; set; }

    public int MunicipalityId { get; set; }

    public Municipality? Municipality { get; set; }

    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
}
