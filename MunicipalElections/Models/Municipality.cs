using System.ComponentModel.DataAnnotations;

namespace MunicipalElections.Models;

public class Municipality
{
    public int MunicipalityId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Province { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Election Date")]
    public DateTime ElectionDate { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(255)]
    [Display(Name = "Logo")]
    public string? LogoFileName { get; set; }

    public ICollection<Position> Positions { get; set; } = new List<Position>();
}
