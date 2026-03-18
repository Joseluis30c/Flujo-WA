using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoatWhatsAppAgent.Core.Interfaces.Services;
using SoatWhatsAppAgent.Core.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SoatWhatsAppAgent.Infrastructure.Services;

public class ImageProcessingService : IImageProcessingService
{
    private readonly HttpClient _httpClient;
    private readonly ModelSettings _settings;
    private readonly string _twilio_account;
    private readonly string _twilio_token;

    public ImageProcessingService(
        HttpClient httpClient,
        IOptions<ModelSettings> settings,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _twilio_account = configuration["Twilio:AccountSid"];
        _twilio_token = configuration["Twilio:AuthToken"];
    }

    public async Task<PaymentReceiptData> ExtractPaymentDataFromImageAsync(string imageUrl, string mediaContentType)
    {
        try
        {
            // 1. Descargar imagen si es URL de Twilio
            byte[] imageData;
            if (imageUrl.StartsWith("data:"))
            {
                // Es base64, extraer datos
                var base64Data = imageUrl.Split(',')[1];
                imageData = Convert.FromBase64String(base64Data);
            }
            else
            {
                // Es URL de Twilio, descargar
                imageData = await DownloadImageFromTwilioAsync(imageUrl);
            }

            // 2. Enviar a modelo para extracción de datos
            var extractedData = await ProcessImageWithAI(imageData, mediaContentType);

            return extractedData;
        }
        catch (Exception ex)
        {
            return new PaymentReceiptData
            {
                IsValid = false,
                ErrorMessage = "No pude procesar la imagen del comprobante"
            };
        }
    }

    public async Task<byte[]> DownloadImageFromTwilioAsync(string mediaUrl)
    {
        try
        {
            // Para Twilio
            var twilioAccountSid = _twilio_account;
            var twilioAuthToken = _twilio_token;

            if (!string.IsNullOrEmpty(twilioAccountSid) && !string.IsNullOrEmpty(twilioAuthToken))
            {
                var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{twilioAccountSid}:{twilioAuthToken}"));
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            }

            var response = await _httpClient.GetAsync(mediaUrl);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private async Task<PaymentReceiptData> ProcessImageWithAI(byte[] imageData, string contentType)
    {
        try
        {
            // Convertir imagen a base64 para enviar al modelo
            var base64Image = Convert.ToBase64String(imageData);
            var dataUrl = $"data:{contentType};base64,{base64Image}";

            // Usar modelo de visión (Claude, GPT-4V, o Groq con Llava)
            var extractedText = await ExtractTextWithVisionModel(dataUrl);

            // Procesar el texto extraído para obtener datos estructurados
            return ProcessExtractedText(extractedText);
        }
        catch (Exception ex)
        {
            return new PaymentReceiptData
            {
                IsValid = false,
                ErrorMessage = "Error procesando la imagen con IA"
            };
        }
    }

    private async Task<string> ExtractTextWithVisionModel(string dataUrl)
    {
        var systemPrompt = @"Eres un experto en extraer información de comprobantes de pago peruanos.
Analiza la imagen y extrae EXACTAMENTE la siguiente información en formato JSON:

{
  ""tipoComprobante"": ""yape|plin|transferencia|deposito|otro"",
  ""monto"": ""123.45"",
  ""fecha"": ""2025-01-15"",
  ""hora"": ""14:30"",
  ""numeroOperacion"": ""ABC123456"",
  ""entidadEmisor"": ""Yape|BCP|BBVA|etc"",
  ""numeroDestino"": ""987654321"",
  ""concepto"": ""SOAT QOA"",
  ""esValido"": true|false,
  ""razonInvalido"": ""motivo si no es válido""
}

REGLAS IMPORTANTES:
- Si es Yape/Plin, busca el monto, fecha/hora, y número de operación
- Si es transferencia bancaria, busca CCI, monto, fecha, banco
- Si es depósito, busca número de cuenta, monto, fecha
- Verifica que el concepto mencione ""SOAT"" o ""QOA"" o ""SEGURO""
- Si no puedes leer claramente algún dato crítico, marca esValido: false
- Extrae solo números sin símbolos de moneda para el monto";

        var userPrompt = "Analiza esta imagen de comprobante de pago y extrae la información solicitada:";

        // Llamada al modelo
        var response = await CallVisionModel(systemPrompt, userPrompt, dataUrl);

        return response;
    }

    private async Task<string> CallVisionModel(string systemPrompt, string userPrompt, string imageDataUrl)
    {
        // Ejemplo para Groq con Llama
        var request = new
        {
            model = "meta-llama/llama-4-scout-17b-16e-instruct",
            messages = new object[] 
        {
            new
            {
                role = "system",
                content = systemPrompt
            },
            new
            {
                role = "user",
                content = new object[]
                {
                    new { type = "text", text = userPrompt },
                    new { type = "image_url", image_url = new { url = imageDataUrl } }
                }
            }
        },
            temperature = 0.1,
            max_tokens = 1000
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _settings.Endpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var rawResponse = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(rawResponse);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return content ?? string.Empty;
    }

    private PaymentReceiptData ProcessExtractedText(string extractedJson)
    {
        try
        {
            // Extraer solo el JSON del texto completo
            var jsonContent = ExtractJsonFromText(extractedJson);

            if (string.IsNullOrEmpty(jsonContent))
            {
                return CreateFallbackExtraction(extractedJson);
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
            };

            var extracted = JsonSerializer.Deserialize<ExtractedReceiptData>(jsonContent, options);

            if (extracted == null)
            {
                return CreateFallbackExtraction(extractedJson);
            }

            return new PaymentReceiptData
            {
                IsValid = extracted.EsValido,
                PaymentMethod = extracted.TipoComprobante,
                Amount = ParseAmount(extracted.Monto),
                TransactionId = extracted.NumeroOperacion,
                PaymentDate = ParseDate(extracted.Fecha, extracted.Hora),
                PayerInfo = extracted.EntidadEmisor,
                Concept = extracted.Concepto,
                ErrorMessage = extracted.EsValido ? null : extracted.RazonInvalido
            };
        }
        catch (Exception ex)
        {
            return CreateFallbackExtraction(extractedJson);
        }
    }

    private PaymentReceiptData CreateFallbackExtraction(string text)
    {
        // Fallback usando regex si falla el JSON
        var receiptData = new PaymentReceiptData { IsValid = false };

        // Buscar monto
        var montoMatch = Regex.Match(text, @"S/?\s*(\d+(?:\.\d{2})?)", RegexOptions.IgnoreCase);
        if (montoMatch.Success && decimal.TryParse(montoMatch.Groups[1].Value, out var amount))
        {
            receiptData.Amount = amount;
        }

        // Buscar número de operación
        var operacionMatch = Regex.Match(text, @"(?:operaci[oó]n|transacci[oó]n|ref(?:erencia)?)[:\s]*([A-Z0-9]+)", RegexOptions.IgnoreCase);
        if (operacionMatch.Success)
        {
            receiptData.TransactionId = operacionMatch.Groups[1].Value;
        }

        // Buscar fecha
        var fechaMatch = Regex.Match(text, @"(\d{1,2}[-/]\d{1,2}[-/]\d{4}|\d{4}[-/]\d{1,2}[-/]\d{1,2})");
        if (fechaMatch.Success && DateTime.TryParse(fechaMatch.Groups[1].Value, out var fecha))
        {
            receiptData.PaymentDate = fecha;
        }

        // Determinar si contiene palabras clave válidas
        var validKeywords = new[] { "soat", "qoa", "seguro", "poliza", "póliza" };
        receiptData.IsValid = validKeywords.Any(keyword =>
            text.Contains(keyword, StringComparison.OrdinalIgnoreCase)) &&
            receiptData.Amount > 0;

        if (!receiptData.IsValid)
        {
            receiptData.ErrorMessage = "No pude identificar información válida del comprobante";
        }

        return receiptData;
    }

    private decimal ParseAmount(string? amountStr)
    {
        if (string.IsNullOrEmpty(amountStr)) return 0;

        // Remover símbolos y espacios
        var clean = Regex.Replace(amountStr, @"[^\d.]", "");
        return decimal.TryParse(clean, out var amount) ? amount : 0;
    }

    private DateTime? ParseDate(string? dateStr, string? timeStr)
    {
        if (string.IsNullOrEmpty(dateStr)) return null;

        var fullDateStr = string.IsNullOrEmpty(timeStr) ? dateStr : $"{dateStr} {timeStr}";
        return DateTime.TryParse(fullDateStr, out var date) ? date : null;
    }

    private string ExtractJsonFromText(string text)
    {
        try
        {
            // Buscar el inicio del JSON (primer '{')
            var startIndex = text.IndexOf('{');
            if (startIndex == -1)
            {
                return string.Empty;
            }

            // Buscar el final del JSON (último '}')
            var endIndex = text.LastIndexOf('}');
            if (endIndex == -1 || endIndex <= startIndex)
            {
                return string.Empty;
            }

            // Extraer el substring JSON
            var jsonContent = text.Substring(startIndex, endIndex - startIndex + 1);

            // Validar que sea JSON válido intentando parsearlo
            try
            {
                using var doc = JsonDocument.Parse(jsonContent);
                return jsonContent;
            }
            catch (JsonException)
            {
                return string.Empty;
            }
        }
        catch (Exception ex)
        {
            return string.Empty;
        }
    }
}
