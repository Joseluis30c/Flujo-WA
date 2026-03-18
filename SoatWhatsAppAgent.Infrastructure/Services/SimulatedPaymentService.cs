using SoatWhatsAppAgent.Core.Interfaces.Services;
using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;

namespace SoatWhatsAppAgent.Infrastructure.Services;

public class SimulatedPaymentService : IPaymentService
{
    private readonly ConcurrentDictionary<string, PaymentInfo> _payments = new();
    private readonly string _baseUrl;

    public SimulatedPaymentService(IConfiguration configuration)
    {
        _baseUrl = configuration["PaymentSettings:BaseUrl"] ?? "https://localhost:7074";
    }

    public Task<string> GeneratePaymentLink(string offerId, string dni, string plate)
    {
        // Crear ID único para el pago
        var timestamp = DateTime.UtcNow.Ticks.ToString("x");
        var random = new Random().Next(1000, 9999);
        var paymentId = $"{timestamp}{random}";

        // Guardar información del pago
        _payments[paymentId] = new PaymentInfo
        {
            PaymentId = paymentId,
            OfferId = offerId,
            Dni = dni,
            Plate = plate,
            CreatedAt = DateTime.UtcNow,
            IsPaid = false
        };

        // Generar link
        var link = $"{_baseUrl}/api/payment/checkout/{paymentId}";
        return Task.FromResult(link);
    }

    public Task<bool> IsPaymentConfirmedAsync(string paymentLink)
    {
        // Extraer paymentId del link
        var paymentId = ExtractPaymentIdFromLink(paymentLink);

        if (_payments.TryGetValue(paymentId, out var payment))
        {
            return Task.FromResult(payment.IsPaid);
        }

        return Task.FromResult(false);
    }

    public void MarkAsPaid(string paymentLink, PaymentReceiptData? receiptData = null)
    {
        var paymentId = ExtractPaymentIdFromLink(paymentLink);

        if (_payments.TryGetValue(paymentId, out var payment))
        {
            payment.IsPaid = true;
            payment.PaidAt = DateTime.UtcNow;
            payment.ReceiptData = receiptData;
        }
    }

    public Task<bool> ValidateReceiptDataAsync(PaymentReceiptData receiptData, string expectedOfferId)
    {
        // Validación básica
        if (receiptData == null || !receiptData.IsValid)
            return Task.FromResult(false);

        if (receiptData.Amount <= 0)
            return Task.FromResult(false);

        // Validar que la fecha no sea muy antigua (ej: máximo 7 días)
        if (receiptData.PaymentDate.HasValue)
        {
            var daysDifference = (DateTime.UtcNow - receiptData.PaymentDate.Value).TotalDays;
            if (daysDifference > 7 || daysDifference < 0)
                return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public PaymentInfo? GetPaymentInfo(string paymentId)
    {
        _payments.TryGetValue(paymentId, out var payment);
        return payment;
    }

    private string ExtractPaymentIdFromLink(string link)
    {
        // Extrae el ID desde el link: https://localhost:7074/api/payment/checkout/{paymentId}
        var parts = link.Split('/');
        return parts[^1]; // Último segmento
    }
}