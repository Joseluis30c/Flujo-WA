using SoatWhatsAppAgent.Core.Models;

namespace SoatWhatsAppAgent.Core.Interfaces.Services;

public interface IAiResponseService
{
    // Extrae intención y entidades. Usa Groq + regex fallback
    Task<AiResult> ExtractAsync(string userMessage, ConversationData context);

    // Produce la respuesta NATURAL en español usando Groq
    Task<string> ComposeAsync(string instruction, ConversationData context, AiResult? nlu = null, object? extra = null);
}