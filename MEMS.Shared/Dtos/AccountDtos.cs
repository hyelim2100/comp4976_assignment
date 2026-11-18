using System.ComponentModel.DataAnnotations;
using MEMS.Shared.Security;
using MEMS.Shared.Validation;

namespace MEMS.Shared.Dtos;

public sealed record AdminUserDto(
    string Id,
    string UserName,
    string? Email,
    string Role,
    int? MunicipalityId,
    string? MunicipalityName);

// Rule shared by the user-management form and the service layer:
// a Municipality Admin must belong to exactly one tenant; a Super Admin belongs to none.
public static class AdminAccountRules
{
    public static string? Validate(string? role, int? municipalityId) => role switch
    {
        AppRoles.SuperAdmin when municipalityId is not null => "A Super Admin cannot be assigned to a municipality.",
        AppRoles.MunicipalityAdmin when municipalityId is null or <= 0 => "A Municipality Admin must be assigned to a municipality.",
        AppRoles.SuperAdmin or AppRoles.MunicipalityAdmin => null,
        _ => "Choose a valid role.",
    };
}

// Changes the role / tenant of an existing administrator.
public class AdminUserAssignmentInput : IValidatableObject
{
    [Required]
    public string Role { get; set; } = AppRoles.MunicipalityAdmin;

    [Display(Name = "Municipality")]
    public int? MunicipalityId { get; set; }

    [Required]
    [EmailAddress]
    [StringLength(FieldLengths.Email)]
    public string Email { get; set; } = string.Empty;

    public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var error = AdminAccountRules.Validate(Role, MunicipalityId);
        if (error is not null)
        {
            yield return new ValidationResult(error, [nameof(MunicipalityId)]);
        }
    }
}

public class AdminUserCreateInput : AdminUserAssignmentInput
{
    [Required]
    [StringLength(FieldLengths.UserName, MinimumLength = 3)]
    [RegularExpression("^[A-Za-z0-9._-]+$", ErrorMessage = "Use letters, numbers, dots, hyphens or underscores.")]
    [Display(Name = "Username")]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = FieldLengths.MinPasswordLength)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var result in base.Validate(validationContext))
        {
            yield return result;
        }
        if (Password != ConfirmPassword)
        {
            yield return new ValidationResult("The passwords do not match.", [nameof(ConfirmPassword)]);
        }
    }
}

public class ResetPasswordInput : IValidatableObject
{
    [Required]
    [StringLength(100, MinimumLength = FieldLengths.MinPasswordLength)]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (NewPassword != ConfirmPassword)
        {
            yield return new ValidationResult("The passwords do not match.", [nameof(ConfirmPassword)]);
        }
    }
}

// API login (JWT).
public sealed class LoginRequest
{
    [Required]
    public string UserName { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public sealed record LoginResponse(
    string Token,
    DateTime ExpiresUtc,
    string UserName,
    string Role,
    int? MunicipalityId,
    string? MunicipalitySlug);
