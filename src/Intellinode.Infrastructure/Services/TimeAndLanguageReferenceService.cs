using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Domain.Entities;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Intellinode.Infrastructure.Services;

public sealed class TimeAndLanguageReferenceService : ITimeAndLanguageReferenceService
{
    private readonly IntellinodeDbContext _dbContext;

    public TimeAndLanguageReferenceService(IntellinodeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TimeAndLanguageReferenceResult<RegionLocationMasterDto>> GetLocationsAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = BuildRegionLocationQuery('L', includeInactive);
            var items = await ProjectRegionLocations(query, cancellationToken);
            return TimeAndLanguageReferenceResult<RegionLocationMasterDto>.Success(
                BuildListResponse(items, "Locations retrieved successfully."));
        }
        catch (Exception ex)
        {
            return TimeAndLanguageReferenceResult<RegionLocationMasterDto>.Failure(
                "LegacyBehaviorExecutionFailed",
                ex.Message);
        }
    }

    public async Task<TimeAndLanguageReferenceResult<RegionLocationMasterDto>> GetRegionsAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = BuildRegionLocationQuery('R', includeInactive);
            var items = await ProjectRegionLocations(query, cancellationToken);
            return TimeAndLanguageReferenceResult<RegionLocationMasterDto>.Success(
                BuildListResponse(items, "Regions retrieved successfully."));
        }
        catch (Exception ex)
        {
            return TimeAndLanguageReferenceResult<RegionLocationMasterDto>.Failure(
                "LegacyBehaviorExecutionFailed",
                ex.Message);
        }
    }

    public async Task<TimeAndLanguageReferenceResult<WindowsTimeZoneMasterDto>> GetTimeZonesAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _dbContext.WindowsTimeZoneMasters.AsNoTracking();
            if (!includeInactive)
            {
                query = query.Where(x => x.IsActive);
            }

            var items = await query
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.DisplayName)
                .Select(x => new WindowsTimeZoneMasterDto
                {
                    Id = x.Id,
                    DisplayName = x.DisplayName,
                    WindowsTzKey = x.WindowsTzKey
                })
                .ToListAsync(cancellationToken);

            return TimeAndLanguageReferenceResult<WindowsTimeZoneMasterDto>.Success(
                BuildListResponse(items, "Time zones retrieved successfully."));
        }
        catch (Exception ex)
        {
            return TimeAndLanguageReferenceResult<WindowsTimeZoneMasterDto>.Failure(
                "LegacyBehaviorExecutionFailed",
                ex.Message);
        }
    }

    private IQueryable<RegionAndLocationMaster> BuildRegionLocationQuery(char identifier, bool includeInactive)
    {
        var query = _dbContext.RegionAndLocationMasters
            .AsNoTracking()
            .Where(x => x.Identifier == identifier)
            .Where(x => x.Id != 39070 && x.Value != "World");

        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        return query;
    }

    private static async Task<List<RegionLocationMasterDto>> ProjectRegionLocations(
        IQueryable<RegionAndLocationMaster> query,
        CancellationToken cancellationToken)
    {
        return await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Value)
            .Select(x => new RegionLocationMasterDto
            {
                Id = x.Id,
                Identifier = x.Identifier,
                Value = x.Value,
                Bcp47Code = x.Bcp47Code
            })
            .ToListAsync(cancellationToken);
    }

    private static TimeAndLanguageReferenceListResponse<T> BuildListResponse<T>(List<T> items, string message) =>
        new()
        {
            Success = true,
            Message = message,
            Data = items
        };
}
