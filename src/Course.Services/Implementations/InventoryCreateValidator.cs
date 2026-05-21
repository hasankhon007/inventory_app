using Course.DataAccess.UnitOfWork;
using Course.Domain.Entities;
using Course.Domain.Exceptions;
using Course.Services.DTOs;
using Course.Services.Interfaces;
using Course.Services.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Course.Services.Implementations;

public class InventoryCreateValidator : IInventoryCreateValidator
{
    private readonly IUnitOfWork _unitOfWork;

    public InventoryCreateValidator(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<InventoryCreateValidationResultDto> ValidateAsync(InventoryCreateDto model)
    {
        if (model == null)
        {
            throw new ValidationAppException("Inventory payload is required.");
        }

        var errors = new List<string>();

        List<InventoryIdElement> idElements = new();
        if (!InventoryIdFormatService.TryParse(model.IdFormat, out idElements, out var formatError))
        {
            errors.Add(formatError ?? "Invalid ID format.");
        }

        var usersToShare = new List<ApplicationUser>();
        if (!model.IsPublic)
        {
            var sharedUserIds = ParseSharedUserIds(model.SharedUserIds, model.SharedWithUsers);
            if (sharedUserIds.Count > 0)
            {
                usersToShare = await _unitOfWork.Repository<ApplicationUser>()
                    .Query()
                    .Where(user => sharedUserIds.Contains(user.Id))
                    .ToListAsync();

                var missingUserIds = sharedUserIds
                    .Where(id => !usersToShare.Any(user => user.Id == id))
                    .ToList();

                if (missingUserIds.Count > 0)
                {
                    errors.Add($"These users were not found: {string.Join(", ", missingUserIds)}.");
                }
            }
        }

        return new InventoryCreateValidationResultDto
        {
            ParsedIdElements = idElements,
            SharedUsers = usersToShare,
            Errors = errors
        };
    }

    private static List<string> ParseSharedUserIds(IReadOnlyCollection<string>? sharedUserIds, string? rawValue)
    {
        var values = new List<string>();
        if (sharedUserIds != null && sharedUserIds.Count > 0)
        {
            values.AddRange(sharedUserIds.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()));
        }

        if (values.Count > 0)
        {
            return values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return new List<string>();
        }

        return rawValue
            .Split([',', ';', '\n', '\r', '\t', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
