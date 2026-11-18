using MEMS.Data.Services;
using MEMS.Shared.Validation;
using Microsoft.AspNetCore.Mvc;

namespace MEMS.Api.Infrastructure;

[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    // Service outcome -> HTTP: 404 / 403 / 400 ValidationProblemDetails.
    protected ActionResult FromFailure(ServiceResult result)
    {
        switch (result.Status)
        {
            case ServiceStatus.NotFound:
                return Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found", detail: result.Message);
            case ServiceStatus.Forbidden:
                return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: result.Message);
            case ServiceStatus.Invalid:
                foreach (var (field, messages) in result.Errors)
                {
                    foreach (var message in messages)
                    {
                        ModelState.AddModelError(field, message);
                    }
                }
                return ValidationProblem(ModelState);
            default:
                throw new InvalidOperationException($"{result.Status} is not a failure.");
        }
    }

    protected ActionResult NotFoundProblem(string detail) =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found", detail: detail);

    // Reads a multipart image upload, rejecting oversized files before buffering them.
    protected async Task<ImageUpload?> ReadImageAsync(IFormFile? file, string field)
    {
        if (file is null)
        {
            ModelState.AddModelError(field, "Please choose an image file.");
            return null;
        }

        var metadataError = ImageRules.ValidateMetadata(file.FileName, file.Length);
        if (metadataError is not null)
        {
            ModelState.AddModelError(field, metadataError);
            return null;
        }

        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, HttpContext.RequestAborted);
        return new ImageUpload(file.FileName, buffer.ToArray());
    }
}
