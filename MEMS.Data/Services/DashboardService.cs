using MEMS.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace MEMS.Data.Services;

// Admin dashboard totals. The query filters make these counts tenant-scoped automatically:
// a Municipality Admin sees 1 municipality and only their own positions and candidates.
public class DashboardService(IDbContextFactory<MemsDbContext> dbFactory)
{
    public async Task<DashboardDto> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return new DashboardDto(
            await db.Municipalities.CountAsync(cancellationToken),
            await db.Positions.CountAsync(cancellationToken),
            await db.Candidates.CountAsync(cancellationToken));
    }
}
