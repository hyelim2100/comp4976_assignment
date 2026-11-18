namespace MEMS.Data.Entities;

// Rows that belong to exactly one tenant. Every ITenantOwned entity gets a global query filter.
public interface ITenantOwned
{
    int MunicipalityId { get; }
}
