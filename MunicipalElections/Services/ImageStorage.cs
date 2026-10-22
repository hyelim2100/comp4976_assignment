namespace MunicipalElections.Services;

// Stores uploaded logos and candidate photos under GUID file names and serves them from /uploads.
// The folder defaults to wwwroot/uploads; on Azure set Uploads__Path to a persistent folder
// (e.g. /home/data/uploads) so files survive redeployments.
public class ImageStorage
{
    public const string RequestPath = "/uploads";
    public const long MaxBytes = 2 * 1024 * 1024;
    public static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];

    public ImageStorage(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configuredPath = configuration["Uploads:Path"];
        RootPath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(environment.WebRootPath, "uploads")
            : configuredPath;
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public string? GetUrl(string? fileName) =>
        string.IsNullOrEmpty(fileName) ? null : $"{RequestPath}/{Uri.EscapeDataString(fileName)}";

    // Returns an error message, or null when the file is an acceptable image.
    public static string? Validate(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            return $"Only {string.Join(", ", AllowedExtensions)} images are allowed.";
        }
        if (file.Length == 0)
        {
            return "The image file is empty.";
        }
        if (file.Length > MaxBytes)
        {
            return $"The image must be {MaxBytes / 1024 / 1024} MB or smaller.";
        }

        using var stream = file.OpenReadStream();
        var header = new byte[12];
        var read = stream.Read(header, 0, header.Length);
        return HasImageSignature(header.AsSpan(0, read)) ? null : "The file is not a valid image.";
    }

    public async Task<string> SaveAsync(IFormFile file)
    {
        var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
        await using var output = File.Create(Path.Combine(RootPath, fileName));
        await file.CopyToAsync(output);
        return fileName;
    }

    public void Delete(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName) || Path.GetFileName(fileName) != fileName)
        {
            return;
        }

        var path = Path.Combine(RootPath, fileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    // Copies bundled sample images into the uploads folder when they are missing.
    public void CopySeedImages(string sourceDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        foreach (var source in Directory.EnumerateFiles(sourceDirectory))
        {
            var target = Path.Combine(RootPath, Path.GetFileName(source));
            if (!File.Exists(target))
            {
                File.Copy(source, target);
            }
        }
    }

    private static bool HasImageSignature(ReadOnlySpan<byte> h) =>
        h.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF])                              // JPEG
        || h.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]) // PNG
        || h.StartsWith("GIF8"u8)                                                          // GIF
        || (h.Length >= 12 && h[..4].SequenceEqual("RIFF"u8) && h[8..12].SequenceEqual("WEBP"u8));
}
