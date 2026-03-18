namespace SoatWhatsAppAgent.Core.Interfaces.Services;

public interface IPaymentService
{
    Task<string> GeneratePaymentLink(string offerId, string dni, string plate);
    Task<bool> IsPaymentConfirmedAsync(string paymentLink);
    void MarkAsPaid(string paymentLink, PaymentReceiptData? receiptData = null);
    Task<bool> ValidateReceiptDataAsync(PaymentReceiptData receiptData, string expectedOfferId);
}