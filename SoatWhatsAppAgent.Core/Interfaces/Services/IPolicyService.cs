namespace SoatWhatsAppAgent.Core.Interfaces.Services;

public interface IPolicyService
{
    Task<string> IssuePolicy(string dni, string plate, string offerId);
}
