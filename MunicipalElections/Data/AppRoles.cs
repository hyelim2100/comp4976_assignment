namespace MunicipalElections.Data;

public static class AppRoles
{
    public const string SuperAdmin = "Super Admin";

    public static string MunicipalityAdmin(string municipalityName) => $"{municipalityName} Admin";
}
