namespace SoatWhatsAppAgent.Core.Interfaces.Services;

public interface IPlateApiService
{
    Task<(bool valid, string? brand, string? model)> ValidatePlateAsync(string plate);
}
