using System.ComponentModel.DataAnnotations;

namespace MunicipalElections.Models;

public class Position
{
    public int PositionId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(1, 100)]
    [Display(Name = "Seats Available")]
    public int SeatsAvailable { get; set; }

    [Required]
    [Display(Name = "Municipality")]
    public int MunicipalityId { get; set; }

    public Municipality? Municipality { get; set; }

    public ICollection<Candidate> Candidates { get; set; } = new List<Candidate>();
}
