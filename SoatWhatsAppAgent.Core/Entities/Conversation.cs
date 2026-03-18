using SoatWhatsAppAgent.Core.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SoatWhatsAppAgent.Core.Entities;

public class Conversation
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(20)]
    public string PhoneNumber { get; set; } = "";

    public ConversationState State { get; set; } = ConversationState.Greeting;

    [StringLength(8)]
    public string? Dni { get; set; }

    [StringLength(6)]
    public string? Plate { get; set; }

    [StringLength(100)]
    public string? ClientName { get; set; }

    [StringLength(50)]
    public string? SelectedOfferId { get; set; }

    [StringLength(500)]
    public string? PaymentLink { get; set; }

    public bool PolicyIssued { get; set; } = false;

    // NUEVOS campos para mejor seguimiento
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastInteractionUtc { get; set; } = DateTime.UtcNow;

    [StringLength(100)]
    public string? Email { get; set; }

    public int InteractionCount { get; set; } = 0;

    // Campos de auditoría adicionales
    [StringLength(50)]
    public string? LastIntent { get; set; }

    public bool HasCompletedFlow { get; set; } = false;

    // Relación con mensajes
    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
}

public class Message
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ConversationId { get; set; }

    [Required]
    [StringLength(10)]
    public string Direction { get; set; } = ""; // "inbound" o "outbound"

    [Required]
    [StringLength(4000)]
    public string Text { get; set; } = "";

    //Timestamp para el historial
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    //campos para análisis
    [StringLength(50)]
    public string? Intent { get; set; }

    public double? Confidence { get; set; }

    [StringLength(1000)]
    public string? ExtractedEntities { get; set; } // JSON serializado

    [StringLength(500)]
    public string? MediaUrl { get; set; }

    [StringLength(50)]
    public string? MediaContentType { get; set; }

    public bool HasMedia { get; set; } = false;

    //Datos extraídos de imagen si aplica
    [StringLength(2000)]
    public string? ExtractedImageData { get; set; } // JSON serializado

    [ForeignKey("ConversationId")]
    public virtual Conversation Conversation { get; set; } = null!;
}