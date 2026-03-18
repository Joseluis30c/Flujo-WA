namespace SoatWhatsAppAgent.Core.Interfaces.Services;

public interface IWhatsAppService
{
    Task SendMessageAsync(string toPhoneNumber, string message);
}
