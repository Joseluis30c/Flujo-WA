namespace SoatWhatsAppAgent.Core.Interfaces.Services;

public interface IImageProcessingService
{
    Task<PaymentReceiptData> ExtractPaymentDataFromImageAsync(string imageUrl, string mediaContentType);
    Task<byte[]> DownloadImageFromTwilioAsync(string mediaUrl);
}

public class PaymentReceiptData
{
    public bool IsValid { get; set; }
    public string PaymentMethod { get; set; } = "";
    public decimal Amount { get; set; }
    public string TransactionId { get; set; } = "";
    public DateTime? PaymentDate { get; set; }
    public string PayerInfo { get; set; } = "";
    public string Concept { get; set; } = "";
    public string? ErrorMessage { get; set; }
}

public class ExtractedReceiptData
{
    public string TipoComprobante { get; set; } = "";
    public string Monto { get; set; } = "";
    public string Fecha { get; set; } = "";
    public string Hora { get; set; } = "";
    public string NumeroOperacion { get; set; } = "";
    public string EntidadEmisor { get; set; } = "";
    public string NumeroDestino { get; set; } = "";
    public string Concepto { get; set; } = "";
    public bool EsValido { get; set; }
    public string RazonInvalido { get; set; } = "";
}

public class PaymentInfo
{
    public string PaymentId { get; set; } = "";
    public string OfferId { get; set; } = "";
    public string Dni { get; set; } = "";
    public string Plate { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }
    public PaymentReceiptData? ReceiptData { get; set; }
    public decimal Amount { get; set; }
    public string ProductName { get; set; } = "";
    public string VendorName { get; set; } = "";
}