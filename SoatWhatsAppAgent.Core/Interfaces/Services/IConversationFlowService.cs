using SoatWhatsAppAgent.Core.Models;

namespace SoatWhatsAppAgent.Core.Interfaces.Services;

public interface IConversationFlowService
{
    Task<string> HandleMessageAsync(IncomingMessage incomingMessage);
}
