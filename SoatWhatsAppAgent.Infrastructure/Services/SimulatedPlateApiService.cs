using SoatWhatsAppAgent.Core.Interfaces.Services;

namespace SoatWhatsAppAgent.Infrastructure.Services;

public class SimulatedPlateApiService : IPlateApiService
{
    private static readonly Dictionary<string, (string brand, string model)> Plates = new()
    {
        ["FRG466"] = ("Toyota", "Corolla 2020"),
        ["XYZ123"] = ("Hyundai", "Tucson 2019"),
        ["ABC123"] = ("Hyundai", "Tucson 2019")
    };

    public Task<(bool valid, string? brand, string? model)> ValidatePlateAsync(string plate)
    {
        var key = plate.Replace("-", "").ToUpper();
        if (Plates.TryGetValue(key, out var data))
            return Task.FromResult<(bool valid, string? brand, string? model)>((true, data.brand, data.model));

        return Task.FromResult<(bool valid, string? brand, string? model)>((false, null, null));
    }
}
