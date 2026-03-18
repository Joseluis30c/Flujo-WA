using SoatWhatsAppAgent.Core.Models;

namespace SoatWhatsAppAgent.Core.Interfaces.Services;

public interface IOfferApiService
{
    Task<List<OfferDto>> GetOffersAsync(string dni, string plate);
    Task<OfferDto?> GetOfferByIdAsync(string offerId);
}
