namespace MEMS.Shared.Validation;

// Image upload restrictions (from A1 ImageStorage): extension, size and the file's real signature.
// Shared so the Admin app (Blazor InputFile) and the API (multipart upload) apply identical rules.
public static class ImageRules
{
    public const long MaxBytes = 2 * 1024 * 1024;
    public const int HeaderBytes = 12;

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
    };

    public static IReadOnlyCollection<string> AllowedExtensions => ContentTypes.Keys;

    // Value for an <input type="file" accept="..."> attribute.
    public static string AcceptAttribute => string.Join(",", ContentTypes.Keys);

    public static string Description => $"{string.Join(", ", ContentTypes.Keys)} up to {MaxBytes / 1024 / 1024} MB";

    // Checks that can run before the file is read (Blazor InputFile, client side).
    public static string? ValidateMetadata(string fileName, long length)
    {
        var extension = Path.GetExtension(fileName);
        if (!ContentTypes.ContainsKey(extension))
        {
            return $"Only {string.Join(", ", ContentTypes.Keys)} images are allowed.";
        }
        if (length <= 0)
        {
            return "The image file is empty.";
        }
        if (length > MaxBytes)
        {
            return $"The image must be {MaxBytes / 1024 / 1024} MB or smaller.";
        }
        return null;
    }

    // Full validation of an uploaded image. Returns an error message, or null when acceptable.
    public static string? Validate(string fileName, ReadOnlySpan<byte> content)
    {
        var error = ValidateMetadata(fileName, content.Length);
        if (error is not null)
        {
            return error;
        }

        var actual = DetectContentType(content);
        if (actual is null)
        {
            return "The file is not a valid image.";
        }
        if (actual != ContentTypeFor(fileName))
        {
            return "The file extension does not match the image content.";
        }
        return null;
    }

    public static string ContentTypeFor(string fileName) =>
        ContentTypes.TryGetValue(Path.GetExtension(fileName), out var type) ? type : "application/octet-stream";

    // Identifies the image format from its magic number (first bytes of the file).
    public static string? DetectContentType(ReadOnlySpan<byte> h)
    {
        if (h.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF])) return "image/jpeg";
        if (h.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])) return "image/png";
        if (h.StartsWith("GIF87a"u8) || h.StartsWith("GIF89a"u8)) return "image/gif";
        if (h.Length >= 12 && h[..4].SequenceEqual("RIFF"u8) && h[8..12].SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }
}
