using MEMS.Api.Infrastructure;
using MEMS.Data.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace MEMS.Api.Controllers;

/// <summary>Logos and candidate photos stored in SQL Server.</summary>
[Route("api/{tenant}/images")]
public class ImagesController(ImageService images) : ApiControllerBase
{
    /// <summary>Returns an image of this tenant (logo or candidate photo).</summary>
    /// <remarks>Image ids change on every upload, so responses are cached as immutable.</remarks>
    /// <response code="200">The image bytes.</response>
    /// <response code="404">Unknown tenant, or the image belongs to another tenant.</response>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [Produces("image/png", "image/jpeg", "image/gif", "image/webp", "application/problem+json")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get(string tenant, Guid id, CancellationToken cancellationToken)
    {
        var image = await images.GetAsync(id, cancellationToken);
        if (image is null)
        {
            return NotFoundProblem("The image was not found.");
        }

        Response.Headers[HeaderNames.CacheControl] = "public, max-age=31536000, immutable";
        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        return File(image.Data, image.ContentType, lastModified: null, new EntityTagHeaderValue($"\"{id:N}\""));
    }
}
