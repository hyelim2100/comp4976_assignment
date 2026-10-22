using System.ComponentModel.DataAnnotations;
using MunicipalElections.Validation;

namespace MunicipalElections.Areas.Admin.Models;

public class MunicipalityFormViewModel
{
    public int MunicipalityId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Province { get; set; } = "British Columbia";

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Election Date")]
    public DateTime? ElectionDate { get; set; }

    [StringLength(1000)]
    [DataType(DataType.MultilineText)]
    public string? Description { get; set; }

    [ImageFile]
    [Display(Name = "Logo")]
    public IFormFile? LogoFile { get; set; }

    public string? CurrentLogoFileName { get; set; }

    [Display(Name = "Remove current logo")]
    public bool RemoveLogo { get; set; }
}
