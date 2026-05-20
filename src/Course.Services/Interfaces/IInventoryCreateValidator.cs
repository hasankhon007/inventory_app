using Course.Services.DTOs;

namespace Course.Services.Interfaces;

public interface IInventoryCreateValidator
{
    Task<InventoryCreateValidationResultDto> ValidateAsync(InventoryCreateDto model);
}
