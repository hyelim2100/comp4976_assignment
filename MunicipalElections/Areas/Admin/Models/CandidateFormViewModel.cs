using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using MunicipalElections.Models;
using MunicipalElections.Validation;

namespace MunicipalElections.Areas.Admin.Models;

public class CandidateFormViewModel
{
    public int CandidateId { get; set; }

    [Required]
    [StringLength(100)]
    [Display(Name = "Candidate Name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select a municipality.")]
    [Display(Name = "Municipality")]
    public int? MunicipalityId { get; set; }

    [Required(ErrorMessage = "Please select a position.")]
    [Display(Name = "Position")]
    public int? PositionId { get; set; }

    [StringLength(2000)]
    [DataType(DataType.MultilineText)]
    [Display(Name = "Biography / Profile")]
    public string? Profile { get; set; }

    [Url]
    [StringLength(255)]
    [Display(Name = "Website URL")]
    public string? Website { get; set; }

    [EmailAddress]
    [StringLength(255)]
    [Display(Name = "Email Address")]
    public string? Email { get; set; }

    [ImageFile]
    [Display(Name = "Photograph")]
    public IFormFile? PhotoFile { get; set; }

    public string? CurrentPhotoFileName { get; set; }

    [Display(Name = "Remove current photograph")]
    public bool RemovePhoto { get; set; }

    [ValidateNever]
    public SelectList MunicipalityOptions { get; set; } = new(Array.Empty<object>());

    [ValidateNever]
    public IReadOnlyList<PositionOption> PositionOptions { get; set; } = [];
}
