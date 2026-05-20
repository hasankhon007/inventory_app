using Course.Services.DTOs;

namespace Course.Services.Interfaces;

public interface IProfileService
{
    Task<ProfileDto> GetProfileAsync(string userId);
}
