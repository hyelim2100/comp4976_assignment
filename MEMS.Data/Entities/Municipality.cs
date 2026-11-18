using System.ComponentModel.DataAnnotations;
using MEMS.Shared.Validation;

namespace MEMS.Data.Entities;

// A municipality is a tenant. Its Slug is the public URL segment (/burnaby) and is unique.
public class Municipality
{
    public int Id { get; set; }

    [Required]
    [MaxLength(FieldLengths.MunicipalityName)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(FieldLengths.Slug)]
    public string Slug { get; set; } = string.Empty;

    [Required]
    [MaxLength(FieldLengths.Province)]
    public string Province { get; set; } = string.Empty;

    public DateOnly ElectionDate { get; set; }

    [MaxLength(FieldLengths.Tagline)]
    public string? Tagline { get; set; }

    [MaxLength(FieldLengths.Description)]
    public string? Description { get; set; }

    // Tenant-specific UI colour used by the public site (hex, e.g. #8E44AD).
    [Required]
    [MaxLength(FieldLengths.Color)]
    public string PrimaryColor { get; set; } = "#1F6FB2";

    public Guid? LogoImageId { get; set; }

    public StoredImage? LogoImage { get; set; }

    public ICollection<Position> Positions { get; set; } = new List<Position>();

    public ICollection<Candidate> Candidates { get; set; } = new List<Candidate>();

    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
}
