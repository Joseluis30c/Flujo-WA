using SoatWhatsAppAgent.Core.Models;

namespace SoatWhatsAppAgent.Core.Interfaces.Services;

public interface IClientApiService
{
    Task<(bool exists, string? fullName, string? email)> ValidateClientAsync(string dni);
    Task<(bool created, string? fullName, string? error)> RegisterClientAsync(ClientDto client);
    Task<(bool update, string? fullName, string? error)> UpdateClientAsync(ClientDto client);
}
