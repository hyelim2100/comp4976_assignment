using System.ComponentModel.DataAnnotations;

namespace MunicipalElections.Models;

public class Candidate
{
    public int CandidateId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    [Display(Name = "Biography")]
    public string? Profile { get; set; }

    [StringLength(255)]
    [Display(Name = "Photo")]
    public string? PhotoFileName { get; set; }

    [Url]
    [StringLength(255)]
    public string? Website { get; set; }

    [EmailAddress]
    [StringLength(255)]
    public string? Email { get; set; }

    [Required]
    [Display(Name = "Position")]
    public int PositionId { get; set; }

    public Position? Position { get; set; }

    [Display(Name = "Date Created")]
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
}
