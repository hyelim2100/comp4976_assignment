using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace MEMS.Shared.Validation;

// Tenant slugs are lowercase URL segments (e.g. "pitt-meadows"). Public URLs are matched
// case-insensitively by normalizing to lowercase before lookup.
public static partial class SlugRules
{
    public const string Pattern = "^[a-z0-9]+(?:-[a-z0-9]+)*$";
    public const string PatternMessage = "Use lowercase letters, numbers and single hyphens only (e.g. pitt-meadows).";

    // Segments used by the apps themselves; a tenant with one of these slugs would hide a route.
    public static readonly IReadOnlySet<string> Reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "api", "account", "guide", "print", "health", "alive", "swagger", "openapi",
        "not-found", "error", "css", "js", "lib", "images", "_framework", "_content", "favicon",
    };

    public static string Normalize(string? slug) => (slug ?? string.Empty).Trim().ToLowerInvariant();

    public static bool IsValid(string? slug) =>
        !string.IsNullOrEmpty(slug) && slug.Length <= FieldLengths.Slug && SlugRegex().IsMatch(slug) && !Reserved.Contains(slug);

    // Suggests a slug from a municipality name ("Pitt Meadows" -> "pitt-meadows").
    public static string FromName(string name) => NonSlugChars().Replace(name.ToLowerInvariant(), "-").Trim('-');

    [GeneratedRegex(Pattern)]
    private static partial Regex SlugRegex();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugChars();
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class NotReservedSlugAttribute() : ValidationAttribute("This slug is reserved by the application. Please choose another one.")
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) =>
        value is string slug && SlugRules.Reserved.Contains(slug)
            ? new ValidationResult(ErrorMessage, [validationContext.MemberName!])
            : ValidationResult.Success;
}
