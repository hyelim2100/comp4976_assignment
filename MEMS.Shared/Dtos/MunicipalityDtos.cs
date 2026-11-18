using System.ComponentModel.DataAnnotations;
using MEMS.Shared.Validation;

namespace MEMS.Shared.Dtos;

// Card in the public "Browse municipalities" list.
public sealed record MunicipalitySummaryDto(
    int Id,
    string Name,
    string Slug,
    string Province,
    DateOnly ElectionDate,
    string? Tagline,
    string PrimaryColor,
    Guid? LogoImageId);

public sealed record MunicipalityDto(
    int Id,
    string Name,
    string Slug,
    string Province,
    DateOnly ElectionDate,
    string? Tagline,
    string? Description,
    string PrimaryColor,
    Guid? LogoImageId,
    int PositionCount,
    int CandidateCount);

// What deleting a tenant removes; shown on the confirmation screen before deleting.
public sealed record TenantDeletionImpactDto(
    int MunicipalityId,
    string Name,
    string Slug,
    int PositionCount,
    int CandidateCount,
    int ImageCount,
    IReadOnlyList<string> AdminUserNames);

public class MunicipalityInput
{
    [Required]
    [StringLength(FieldLengths.MunicipalityName)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(FieldLengths.Slug)]
    [RegularExpression(SlugRules.Pattern, ErrorMessage = SlugRules.PatternMessage)]
    [NotReservedSlug]
    [Display(Name = "Tenant slug")]
    public string Slug { get; set; } = string.Empty;

    [Required]
    [StringLength(FieldLengths.Province)]
    public string Province { get; set; } = "British Columbia";

    [Required(ErrorMessage = "The Election Date field is required.")]
    [Display(Name = "Election Date")]
    public DateOnly? ElectionDate { get; set; }

    [StringLength(FieldLengths.Tagline)]
    public string? Tagline { get; set; }

    [StringLength(FieldLengths.Description)]
    public string? Description { get; set; }

    [Required]
    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "Enter a hex colour such as #1F6FB2.")]
    [Display(Name = "Primary colour")]
    public string PrimaryColor { get; set; } = "#1F6FB2";
}

// Creating a tenant also creates its first Municipality Admin account (A1 deficiency #3).
public class TenantCreateInput : MunicipalityInput, IValidatableObject
{
    [Required]
    [StringLength(FieldLengths.UserName, MinimumLength = 3)]
    [RegularExpression("^[A-Za-z0-9._-]+$", ErrorMessage = "Use letters, numbers, dots, hyphens or underscores.")]
    [Display(Name = "Admin username")]
    public string AdminUserName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(FieldLengths.Email)]
    [Display(Name = "Admin email")]
    public string AdminEmail { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = FieldLengths.MinPasswordLength)]
    [DataType(DataType.Password)]
    [Display(Name = "Admin password")]
    public string AdminPassword { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    public string ConfirmAdminPassword { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AdminPassword != ConfirmAdminPassword)
        {
            yield return new ValidationResult("The passwords do not match.", [nameof(ConfirmAdminPassword)]);
        }
    }
}
