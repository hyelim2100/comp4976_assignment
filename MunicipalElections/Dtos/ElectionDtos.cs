using System.ComponentModel.DataAnnotations;

namespace MunicipalElections.Dtos;

public record MunicipalityDto(
    int MunicipalityId,
    string Name,
    string Province,
    DateOnly ElectionDate,
    string? Description,
    string? LogoUrl);

public record MunicipalityDetailDto(
    int MunicipalityId,
    string Name,
    string Province,
    DateOnly ElectionDate,
    string? Description,
    string? LogoUrl,
    IReadOnlyList<PositionDto> Positions);

public record PositionDto(
    int PositionId,
    string Name,
    int SeatsAvailable,
    int MunicipalityId,
    string MunicipalityName);

public record CandidateDto(
    int CandidateId,
    string Name,
    string? Biography,
    string? PhotoUrl,
    string? Website,
    string? Email,
    int PositionId,
    string PositionName,
    int MunicipalityId,
    string MunicipalityName,
    DateTime DateCreated);

public record SavedCandidateDto(
    int SavedCandidateId,
    int CandidateId,
    DateTime DateSaved,
    CandidateDto Candidate);

public record SaveCandidateRequest([Required] int? CandidateId);
