using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Intellinode.Infrastructure.Services;

public sealed class PowerManagementReferenceService : IPowerManagementReferenceService
{
    private readonly IntellinodeDbContext _dbContext;

    public PowerManagementReferenceService(IntellinodeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PowerManagementReferenceResult<WindowsPowerPlanMasterDto>> GetPowerPlansAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _dbContext.WindowsPowerPlanMasters.AsNoTracking();
            if (!includeInactive)
            {
                query = query.Where(x => x.IsActive);
            }

            var items = await query
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.PlanName)
                .Select(x => new WindowsPowerPlanMasterDto
                {
                    Id = x.Id,
                    PlanName = x.PlanName,
                    IsDefault = x.IsDefault
                })
                .ToListAsync(cancellationToken);

            return PowerManagementReferenceResult<WindowsPowerPlanMasterDto>.Success(
                BuildListResponse(items, "Power plans retrieved successfully."));
        }
        catch (Exception ex)
        {
            return PowerManagementReferenceResult<WindowsPowerPlanMasterDto>.Failure(
                "LegacyBehaviorExecutionFailed",
                ex.Message);
        }
    }

    public async Task<PowerManagementReferenceResult<WindowsPowerTimeoutMasterDto>> GetTimeoutsAsync(
        WindowsPowerTimeoutCategory? category = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _dbContext.WindowsPowerTimeoutMasters.AsNoTracking();
            if (!includeInactive)
            {
                query = query.Where(x => x.IsActive);
            }

            if (category.HasValue)
            {
                query = query.Where(x => x.Category == category.Value);
            }

            var items = await query
                .OrderBy(x => x.Category)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.DisplayText)
                .Select(x => new WindowsPowerTimeoutMasterDto
                {
                    Id = x.Id,
                    DisplayText = x.DisplayText,
                    ValueSeconds = x.ValueSeconds,
                    Category = x.Category
                })
                .ToListAsync(cancellationToken);

            return PowerManagementReferenceResult<WindowsPowerTimeoutMasterDto>.Success(
                BuildListResponse(items, "Power timeouts retrieved successfully."));
        }
        catch (Exception ex)
        {
            return PowerManagementReferenceResult<WindowsPowerTimeoutMasterDto>.Failure(
                "LegacyBehaviorExecutionFailed",
                ex.Message);
        }
    }

    public async Task<PowerManagementReferenceResult<WindowsPowerAdvancedOptionGroupCatalogDto>> GetAdvancedOptionsAsync(
        string? planName = null,
        string? optionName = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedPlan = string.IsNullOrWhiteSpace(planName) ? null : planName.Trim();
            var normalizedOption = string.IsNullOrWhiteSpace(optionName) ? null : optionName.Trim();

            var query = _dbContext.WindowsPowerAdvancedOptionMasters.AsNoTracking();
            if (!includeInactive)
            {
                query = query.Where(x => x.IsActive);
            }

            if (normalizedPlan is not null)
            {
                query = query.Where(x => x.PlanName == null || x.PlanName == normalizedPlan);
            }

            if (normalizedOption is not null)
            {
                query = query.Where(x => x.OptionName == normalizedOption);
            }

            var rows = await query
                .OrderBy(x => x.OptionName)
                .ThenBy(x => x.SettingName)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.DisplayText)
                .ToListAsync(cancellationToken);

            var groups = rows
                .GroupBy(x => x.OptionName, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var first = group.First();
                    return new WindowsPowerAdvancedOptionGroupCatalogDto
                    {
                        OptionName = group.Key,
                        PlanName = first.PlanName,
                        Settings = group
                            .GroupBy(x => x.SettingName, StringComparer.OrdinalIgnoreCase)
                            .Select(settingGroup => new WindowsPowerAdvancedOptionSettingCatalogDto
                            {
                                SettingName = settingGroup.Key,
                                Values = settingGroup
                                    .Select(row => new WindowsPowerAdvancedOptionValueDto
                                    {
                                        Id = row.Id,
                                        DisplayText = row.DisplayText,
                                        ValueText = row.ValueText
                                    })
                                    .ToList()
                            })
                            .ToList()
                    };
                })
                .ToList();

            return PowerManagementReferenceResult<WindowsPowerAdvancedOptionGroupCatalogDto>.Success(
                BuildListResponse(groups, "Advanced power options retrieved successfully."));
        }
        catch (Exception ex)
        {
            return PowerManagementReferenceResult<WindowsPowerAdvancedOptionGroupCatalogDto>.Failure(
                "LegacyBehaviorExecutionFailed",
                ex.Message);
        }
    }

    private static TimeAndLanguageReferenceListResponse<T> BuildListResponse<T>(List<T> items, string message) =>
        new()
        {
            Success = true,
            Message = message,
            Data = items
        };
}
