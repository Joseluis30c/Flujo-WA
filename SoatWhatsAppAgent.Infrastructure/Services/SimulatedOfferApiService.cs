using SoatWhatsAppAgent.Core.Interfaces.Services;
using SoatWhatsAppAgent.Core.Models;

namespace SoatWhatsAppAgent.Infrastructure.Services;

public class SimulatedOfferApiService : IOfferApiService
{
    public Task<OfferDto?> GetOfferByIdAsync(string offerId)
    {
        var all = new List<OfferDto>
        {
        new OfferDto { Vendor = "RIMAC", Description = "SOAT Básico", Price = 120, Id = "1"},
        new OfferDto { Vendor = "PACÍFICO", Description = "SOAT Plus Asistencia", Price = 150, Id = "2", },
        new OfferDto { Vendor = "LA POSITIVA", Description = "SOAT Premium", Price = 180, Id = "3", }
        };
        return Task.FromResult(all.FirstOrDefault(o => o.Id == offerId));
    }

    public Task<List<OfferDto>> GetOffersAsync(string dni, string plate)
    {
        var offers = new List<OfferDto>
        {
        new OfferDto {Vendor = "RIMAC", Description = "SOAT Básico", Price = 120, Id = "1"},
        new OfferDto {Vendor = "PACÍFICO", Description = "SOAT Plus Asistencia", Price = 150, Id = "2"},
        new OfferDto {Vendor = "LA POSITIVA", Description = "SOAT Premium", Price = 180, Id = "3"}
        };
        return Task.FromResult(offers);
    }
}
