namespace MunicipalElections.Services;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    // Base64 signing key of at least 256 bits. Kept out of source control
    // (appsettings.Development.json locally, the Jwt__Key app setting on Azure).
    public string Key { get; set; } = string.Empty;

    public int ExpiresMinutes { get; set; } = 1440;
}
