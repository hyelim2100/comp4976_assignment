using System.ComponentModel.DataAnnotations;
using MEMS.Shared.Validation;

namespace MEMS.Data.Entities;

// Logos and candidate photos are stored in SQL Server so every app (Admin, API, Public) sees the same
// files; the API serves them at /api/images/{id}. Only validated images are saved (ImageRules).
public class StoredImage : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(FieldLengths.ContentType)]
    public string ContentType { get; set; } = string.Empty;

    [MaxLength(FieldLengths.FileName)]
    public string? FileName { get; set; }

    [Required]
    public byte[] Data { get; set; } = [];

    public int MunicipalityId { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
