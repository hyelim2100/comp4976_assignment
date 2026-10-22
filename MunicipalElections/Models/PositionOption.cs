namespace MunicipalElections.Models;

// A position in a drop-down, tagged with its municipality for client-side filtering (position-filter.js).
public record PositionOption(int PositionId, string Name, int MunicipalityId);
