using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using MunicipalElections.Services;

namespace MunicipalElections.Validation;

// Validates an optional image upload: extension and size in the browser (wwwroot/js/image-validation.js),
// plus extension, size and file signature on the server.
[AttributeUsage(AttributeTargets.Property)]
public class ImageFileAttribute : ValidationAttribute, IClientModelValidator
{
    public ImageFileAttribute()
        : base($"Please choose a {string.Join(", ", ImageStorage.AllowedExtensions)} image of {ImageStorage.MaxBytes / 1024 / 1024} MB or less.")
    {
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not IFormFile file)
        {
            return ValidationResult.Success;
        }

        var error = ImageStorage.Validate(file);
        return error is null ? ValidationResult.Success : new ValidationResult(error, [validationContext.MemberName!]);
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        context.Attributes["data-val"] = "true";
        context.Attributes["data-val-imagefile"] = FormatErrorMessage(context.ModelMetadata.GetDisplayName());
        context.Attributes["data-val-imagefile-extensions"] = string.Join(",", ImageStorage.AllowedExtensions);
        context.Attributes["data-val-imagefile-maxbytes"] = ImageStorage.MaxBytes.ToString();
        context.Attributes["accept"] = string.Join(",", ImageStorage.AllowedExtensions);
    }
}
