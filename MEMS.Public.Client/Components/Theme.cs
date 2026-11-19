using System.Globalization;

namespace MEMS.Public.Client.Components;

// Tenant-specific look: each municipality's primary colour becomes CSS variables on the tenant shell.
public static class Theme
{
    // CSS custom properties for a municipality colour, with a readable text colour on top of it.
    public static string Style(string? hexColor)
    {
        var color = IsHex(hexColor) ? hexColor! : "#1F6FB2";
        return $"--tenant: {color}; --tenant-contrast: {ContrastText(color)};";
    }

    // Black or white text depending on the background's relative luminance (WCAG formula).
    public static string ContrastText(string hexColor)
    {
        if (!IsHex(hexColor))
        {
            return "#ffffff";
        }
        double Channel(int offset)
        {
            var c = int.Parse(hexColor.Substring(offset, 2), NumberStyles.HexNumber) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        var luminance = 0.2126 * Channel(1) + 0.7152 * Channel(3) + 0.0722 * Channel(5);
        return luminance > 0.45 ? "#111111" : "#ffffff";
    }

    private static bool IsHex(string? value) =>
        value is { Length: 7 } && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);
}
