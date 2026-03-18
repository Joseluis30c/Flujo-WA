using SoatWhatsAppAgent.Core.Interfaces.Services;
using SoatWhatsAppAgent.Core.Models;

namespace SoatWhatsAppAgent.Infrastructure.Services;

public class SimulatedPolicyService : IPolicyService
{
    public Task<string> IssuePolicy(string dni, string plate, string offerId)
    {
        // número de póliza simulado
        var pol = $"SOAT-{DateTime.UtcNow:yyyyMMdd}-{offerId}-{dni[^4..]}";
        return Task.FromResult(pol);
    }
}