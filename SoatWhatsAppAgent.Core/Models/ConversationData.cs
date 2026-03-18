using SoatWhatsAppAgent.Core.Enums;
using SoatWhatsAppAgent.Core.Interfaces.Services;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SoatWhatsAppAgent.Core.Models;

// Clase para representar mensajes en el historial conversacional
public class ConversationMessage
{
    public string Role { get; set; } = ""; // "user" o "assistant"
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

// Extensión de ConversationData para incluir historial
public class ConversationData
{
    public string PhoneNumber { get; set; } = "";
    public ConversationState CurrentState { get; set; } = ConversationState.Greeting;

    // Datos del usuario
    public string? Dni { get; set; }
    public string? Plate { get; set; }
    public string? ClientName { get; set; }
    public string? Email { get; set; }
    public string? BirthDate { get; set; }
    public string? Gender { get; set; }

    // Estado de la conversación
    public bool HasGreeted { get; set; }
    public bool HasConfirmPay { get; set; }
    public bool PolicyIssued { get; set; }

    // Datos de ofertas y pago
    public List<OfferDto>? CachedOffers { get; set; }
    public string? SelectedOfferId { get; set; }
    public string? PaymentLink { get; set; }

    //Datos del comprobante de pago procesado
    public PaymentReceiptData? PaymentReceiptData { get; set; }

    //Historial conversacional
    public List<ConversationMessage> ConversationHistory { get; set; } = new();

    // Métricas y seguimiento
    public DateTime LastInteraction { get; set; } = DateTime.UtcNow;
    public int InteractionCount { get; set; } = 0;
}

// Resultado del NLU
public class AiResult
{
    public string Intent { get; set; } = "UNKNOWN";

    // Entidades existentes
    public string? Dni { get; set; }
    public string? Plate { get; set; }
    public string? ClientName { get; set; }
    public string? BirthDate { get; set; }
    public string? Gender { get; set; }
    public string? Email { get; set; }
    public string? CodVendedor { get; set; }

    public string? OfferSelection { get; set; } // Para manejar selección de ofertas
    public double Confidence { get; set; } = 0.0; // Confianza del modelo
    public string? RawModelOutput { get; set; } // Respuesta cruda del modelo para debugging

    // Información contextual
    public bool RequiresFollowUp { get; set; } = false;
    public List<string> ExtractedEntities { get; set; } = new();
    public bool HasImage { get; set; }
    public string? ImageReference { get; set; }
    public bool HasDocument { get; set; }
    public string? DocumentReference { get; set; }
}

public class OfferDto
{
    [Required]
    public string Id { get; set; } = "";

    [Required]
    public string Vendor { get; set; } = "";

    [Required]
    public string Description { get; set; } = "";

    [Required]
    public decimal Price { get; set; }

    public string? Coverage { get; set; }
    public DateTime ValidUntil { get; set; }
    public bool IsRecommended { get; set; } = false;
    public string? BenefitsHighlight { get; set; }
}

public class ClientDto
{
    [Required]
    [StringLength(8, MinimumLength = 8)]
    public string Dni { get; set; } = "";

    [Required]
    [StringLength(100)]
    public string ClientName { get; set; } = "";

    [Required]
    public string BirthDate { get; set; } = ""; // YYYY-MM-DD

    [Required]
    [RegularExpression("^[MF]$")]
    public string Gender { get; set; } = "";

    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";
}

public class ModelOutput
{
    [JsonPropertyName("intent")]
    public string Intent { get; set; } = "UNKNOWN";

    [JsonPropertyName("entities")]
    public EntitiesContainer? Entities { get; set; }
}

public class EntitiesContainer
{
    [JsonPropertyName("dni")]
    public string? Dni { get; set; }

    [JsonPropertyName("plate")]
    public string? Plate { get; set; }

    [JsonPropertyName("clientname")]
    public string? ClientName { get; set; }

    [JsonPropertyName("birthdate")]
    public string? BirthDate { get; set; }

    [JsonPropertyName("gender")]
    public string? Gender { get; set; }

    [JsonPropertyName("Email")]
    public string? Email { get; set; }

    [JsonPropertyName("CodVendedor")]
    public string? CodVendedor { get; set; }

    [JsonPropertyName("OfferSelection")]
    public string? OfferSelection { get; set; }
}

public class IncomingMessageDto
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? MediaUrl { get; set; }
    public string? MediaContentType { get; set; }
}
public class TestImageDto
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public class IncomingMessage
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool HasMedia { get; set; } = false;
    public string? MediaUrl { get; set; }
    public string? MediaContentType { get; set; }

    public bool IsImage => !string.IsNullOrEmpty(MediaContentType) &&
                          MediaContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
}