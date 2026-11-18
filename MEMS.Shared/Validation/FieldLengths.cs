namespace MEMS.Shared.Validation;

// Single source for string lengths: used by EF entity constraints (database layer),
// DTO/input models (Blazor and API validation) and the generated columns.
public static class FieldLengths
{
    public const int MunicipalityName = 100;
    public const int Slug = 50;
    public const int Province = 50;
    public const int Tagline = 150;
    public const int Description = 1000;
    public const int Color = 7;

    public const int PositionName = 100;
    public const int PositionDescription = 500;
    public const int MinSeats = 1;
    public const int MaxSeats = 100;

    public const int PersonName = 100;
    public const int Profile = 2000;
    public const int Url = 255;
    public const int Email = 255;

    public const int UserName = 50;
    public const int MinPasswordLength = 8;

    public const int ContentType = 50;
    public const int FileName = 255;
}
