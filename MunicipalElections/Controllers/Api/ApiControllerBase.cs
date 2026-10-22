using Microsoft.AspNetCore.Mvc;
using MunicipalElections.Dtos;
using MunicipalElections.Models;
using MunicipalElections.Services;

namespace MunicipalElections.Controllers.Api;

// Base for the mobile app (COMP4977) Web API. Maps entities to the DTO shapes in docs/api-contract.md.
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected string? ImageUrl(string? fileName) =>
        string.IsNullOrEmpty(fileName)
            ? null
            : $"{Request.Scheme}://{Request.Host}{Request.PathBase}{ImageStorage.RequestPath}/{Uri.EscapeDataString(fileName)}";

    // UTC, whole seconds: serializes as "2026-10-22T18:16:17Z", which Swift's .iso8601 decoder accepts.
    protected static DateTime ToUtcSeconds(DateTime value)
    {
        var utc = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return utc.AddTicks(-(utc.Ticks % TimeSpan.TicksPerSecond));
    }

    protected MunicipalityDto ToDto(Municipality m) =>
        new(m.MunicipalityId, m.Name, m.Province, DateOnly.FromDateTime(m.ElectionDate), m.Description, ImageUrl(m.LogoFileName));

    protected static PositionDto ToDto(Position p) =>
        new(p.PositionId, p.Name, p.SeatsAvailable, p.MunicipalityId, p.Municipality!.Name);

    // Requires Position and Position.Municipality to be loaded.
    protected CandidateDto ToDto(Candidate c) =>
        new(c.CandidateId, c.Name, c.Profile, ImageUrl(c.PhotoFileName), c.Website, c.Email,
            c.PositionId, c.Position!.Name, c.Position.MunicipalityId, c.Position.Municipality!.Name,
            ToUtcSeconds(c.DateCreated));
}
