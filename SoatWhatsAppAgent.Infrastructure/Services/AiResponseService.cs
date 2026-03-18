using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoatWhatsAppAgent.Core.Enums;
using SoatWhatsAppAgent.Core.Interfaces.Services;
using SoatWhatsAppAgent.Core.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SoatWhatsAppAgent.Infrastructure.Services;

public class AiResponseService : IAiResponseService
{
    private readonly HttpClient _http = new();
    private readonly ModelSettings _settings;
    private readonly ILogger<AiResponseService> _logger;

    public AiResponseService(IOptions<ModelSettings> settings, ILogger<AiResponseService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<AiResult> ExtractAsync(string userMessage, ConversationData context)
    {
        try
        {
            // 1) Construir contexto conversacional para mejor NLU
            var conversationContext = BuildConversationContext(context);

            // 2) Sistema NLU contexto
            var sys = BuildNLUSystemPrompt(conversationContext);
            var prompt = $"Mensaje actual: {userMessage}";

            var json = await ChatAsync(sys, prompt, expectJson: true);

            var result = ParseNLUResult(json) ?? new AiResult();

            // 3) Aplicar fallbacks de regex con mejor lógica contextual
            ApplyRegexFallbacks(userMessage, result, context);

            // 4) Ajustes de intención basados en contexto
            AdjustIntentBasedOnContext(userMessage, result, context);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en ExtractAsync para mensaje: {Message}", userMessage);
            return CreateFallbackResult(userMessage);
        }
    }

    public async Task<string> ComposeAsync(string instruction, ConversationData context, AiResult? nlu = null, object? extra = null)
    {
        try
        {
            var sys = BuildComposeSystemPrompt();

            // Construir contexto con historial
            var ctx = BuildEnrichedContext(context, nlu, extra);

            var user = $"INSTRUCCIÓN: {instruction}\nCONTEXTO_JSON: {JsonSerializer.Serialize(ctx)}";
            var answer = await ChatAsync(sys, user, expectJson: false);

            return string.IsNullOrWhiteSpace(answer) ? "" : answer.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en ComposeAsync para instrucción: {Instruction}", instruction);
            return "Lo siento, ocurrió un error procesando tu solicitud. ¿Puedes intentar nuevamente?";
        }
    }

    private string BuildConversationContext(ConversationData context)
    {
        var contextInfo = new StringBuilder();

        contextInfo.AppendLine($"Estado actual: {context.CurrentState}");

        if (!string.IsNullOrEmpty(context.Dni)) contextInfo.AppendLine($"DNI ya capturado: {context.Dni}");
        if (!string.IsNullOrEmpty(context.Plate)) contextInfo.AppendLine($"Placa ya capturada: {context.Plate}");
        if (!string.IsNullOrEmpty(context.ClientName)) contextInfo.AppendLine($"Cliente: {context.ClientName}");

        if (context.CachedOffers?.Any() == true)
            contextInfo.AppendLine($"Ofertas disponibles: {context.CachedOffers.Count}");

        if (!string.IsNullOrEmpty(context.SelectedOfferId))
            contextInfo.AppendLine($"Oferta seleccionada: {context.SelectedOfferId}");

        // Agregar historial reciente si está disponible
        if (context.ConversationHistory?.Any() == true)
        {
            contextInfo.AppendLine("\nHistorial reciente:");
            foreach (var msg in context.ConversationHistory.TakeLast(6)) // Últimos 3 intercambios
            {
                var role = msg.Role == "user" ? "Usuario" : "Asistente";
                var preview = msg.Content.Length > 50 ? msg.Content[..50] + "..." : msg.Content;
                contextInfo.AppendLine($"{role}: {preview}");
            }
        }

        return contextInfo.ToString();
    }

    private string BuildNLUSystemPrompt(string conversationContext)
    {
        return $@"Eres un NLU avanzado para WhatsApp de venta de SOAT en Perú.
Tienes acceso al contexto conversacional para mejor comprensión.

CONTEXTO CONVERSACIONAL:
{conversationContext}

Extraes intención y entidades. Responde SOLO JSON válido.

Intents prioritarios:
- GREETING: saludos iniciales (""hola"", ""buenas tardes"", ""buenos días"")
- PROVIDE_DNI_PLATE: cuando proporciona DNI (8 dígitos) o Placa (6 caracteres alfanuméricos)
- SELECT_OFFER: cuando elige una opción (números 1-3, nombres de aseguradoras)
- CONFIRM_PAYMENT: confirma pago (""pagué"", ""ya pague"", ""listo"", ""confirmo"")
- PROVIDE_VENDOR_CODE: proporciona código de vendedor (5 dígitos)
- ASK_IDENTITY: pregunta sobre identidad del bot
- RESET: quiere reiniciar proceso
- UNKNOWN: no clasificable o fuera de contexto

Entidades críticas:
- Dni: EXACTAMENTE 8 dígitos (12345678)
- CodVendedor: EXACTAMENTE 5 dígitos (12345)  
- Plate: 6 caracteres (ABC123, ABC-123)
- ClientName: nombres y apellidos completos
- BirthDate: fechas en formato DD/MM/YYYY o DD-MM-YYYY
- Gender: M (masculino) o F (femenino)
- Email: correos electrónicos válidos
- OfferSelection: número de opción (1, 2, 3) o nombre de aseguradora

REGLAS ESTRICTAS:
- 8 dígitos = SIEMPRE DNI, NUNCA CodVendedor
- 5 dígitos = SIEMPRE CodVendedor, NUNCA DNI
- Considera el contexto conversacional para desambiguar
- Si el estado es SelectOffer, prioriza OFFER_SELECTION
- Si el estado es ConfirmEmission, prioriza CONFIRM_PAYMENT

Formato de respuesta JSON:
{{
  ""Intent"": ""INTENT_NAME"",
  ""Entities"": {{
    ""Dni"": ""12345678"",
    ""CodVendedor"": ""12345"",
    ""Plate"": ""ABC123"",
    ""ClientName"": ""Juan Pérez"",
    ""BirthDate"": ""1990-01-15"",
    ""Gender"": ""M"",
    ""Email"": ""juan@email.com"",
    ""OfferSelection"": ""1""
  }}
}}";
    }

    private string BuildComposeSystemPrompt()
    {
        return @"Eres un agente de ventas SOAT experto y amigable para WhatsApp.

REGLAS DE RESPUESTA:
1. Cuando recibas una INSTRUCCIÓN específica, RESPONDE EXACTAMENTE con ese formato
2. Usa el historial conversacional para dar respuestas más contextuales
3. Mantén tono profesional pero cercano y amigable
4. Respuestas concisas y directas (máximo 2-3 líneas)
5. NUNCA inventes datos que no estén en el contexto
6. Siempre en español

DATOS DE LA EMPRESA:
- Nombre: QOA (plataforma digital de SOAT)
- Disponibilidad: 24/7
- Web: https://globaltpa.pe/
- Especialidad: Venta rápida y segura de SOAT

Si no tienes una instrucción específica, responde naturalmente usando el contexto conversacional.";
    }
    private object BuildEnrichedContext(ConversationData context, AiResult? nlu, object? extra)
    {
        return new
        {
            // Datos actuales del contexto
            state = context.CurrentState.ToString(),
            dni = context.Dni,
            plate = context.Plate,
            client = context.ClientName,
            email = context.Email,
            selectedOfferId = context.SelectedOfferId,

            // Ofertas (todas o la seleccionada)
            offers = string.IsNullOrEmpty(context.SelectedOfferId)
                ? context.CachedOffers
                : context.CachedOffers?.Where(o => o.Id == context.SelectedOfferId).ToList(),

            // Link de pago solo si está disponible
            paymentLink = context.CurrentState == ConversationState.Farewell ? "" : context.PaymentLink,

            // Datos del NLU actual
            nlu = new
            {
                intent = nlu?.Intent,
                dni = nlu?.Dni,
                plate = nlu?.Plate,
                codVendedor = nlu?.CodVendedor,
                clientName = nlu?.ClientName,
                email = nlu?.Email,
                offerSelection = nlu?.OfferSelection
            },

            // Historial conversacional reciente
            conversationHistory = context.ConversationHistory?.TakeLast(4).Select(h => new
            {
                role = h.Role,
                content = h.Content.Length > 100 ? h.Content[..100] + "..." : h.Content,
                timestamp = h.Timestamp.ToString("HH:mm")
            }).ToList(),

            // Datos extra específicos del contexto
            extra
        };
    }

    private AiResult? ParseNLUResult(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            json = CleanJsonResponse(json);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var modelOutput = JsonSerializer.Deserialize<ModelOutput>(json, options);

            if (modelOutput?.Entities == null) return null;

            return new AiResult
            {
                Intent = modelOutput.Intent,
                Dni = modelOutput.Entities.Dni,
                Plate = modelOutput.Entities.Plate,
                ClientName = modelOutput.Entities.ClientName,
                BirthDate = modelOutput.Entities.BirthDate,
                Gender = modelOutput.Entities.Gender,
                Email = modelOutput.Entities.Email,
                CodVendedor = modelOutput.Entities.CodVendedor,
                OfferSelection = modelOutput.Entities.OfferSelection,
                RawModelOutput = json
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parseando respuesta JSON del modelo: {Json}", json);
            return null;
        }
    }

    private void ApplyRegexFallbacks(string userMessage, AiResult result, ConversationData context)
    {
        // Fallback para código de vendedor (exactamente 5 dígitos)
        if (string.IsNullOrEmpty(result.CodVendedor))
        {
            var vendorMatch = Regex.Match(userMessage, @"(?<!\d)(\d{5})(?!\d)");
            if (vendorMatch.Success)
                result.CodVendedor = vendorMatch.Groups[1].Value;
        }

        // Fallback para DNI (exactamente 8 dígitos)
        if (string.IsNullOrEmpty(result.Dni))
        {
            var dniMatch = Regex.Match(userMessage, @"(?<!\d)(\d{8})(?!\d)");
            if (dniMatch.Success)
            {
                result.Dni = dniMatch.Groups[1].Value;
                if (result.Intent == "UNKNOWN") result.Intent = "PROVIDE_DNI_PLATE";
            }
        }

        // Fallback para placa (formatos comunes)
        if (string.IsNullOrEmpty(result.Plate))
        {
            var plateMatch = Regex.Match(userMessage.ToUpper(),
                @"\b([A-Z]{3}-?\d{3}|[A-Z]\d[A-Z]-?\d{3}|[A-Z]{2}-?\d{4})\b");
            if (plateMatch.Success)
            {
                result.Plate = plateMatch.Groups[1].Value.Replace("-", "").ToUpper();
                if (result.Intent == "UNKNOWN") result.Intent = "PROVIDE_DNI_PLATE";
            }
        }

        // Fallback para email
        if (string.IsNullOrEmpty(result.Email))
        {
            var emailMatch = Regex.Match(userMessage,
                @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase);
            if (emailMatch.Success)
                result.Email = emailMatch.Value.Trim().ToLowerInvariant();
        }

        // Fallback para fechas (DD/MM/YYYY o DD-MM-YYYY)
        if (string.IsNullOrEmpty(result.BirthDate))
        {
            var dateMatch = Regex.Match(userMessage, @"\b(\d{2})[/-](\d{2})[/-](\d{4})\b");
            if (dateMatch.Success)
                result.BirthDate = $"{dateMatch.Groups[3].Value}-{dateMatch.Groups[2].Value}-{dateMatch.Groups[1].Value}";
        }

        // Fallback para género
        if (string.IsNullOrEmpty(result.Gender))
        {
            var lower = userMessage.ToLower();
            if (lower.Contains("masculino") || Regex.IsMatch(lower, @"\bm\b"))
                result.Gender = "M";
            else if (lower.Contains("femenino") || Regex.IsMatch(lower, @"\bf\b"))
                result.Gender = "F";
        }

        // Fallback para selección de oferta según contexto
        if (string.IsNullOrEmpty(result.OfferSelection) && context.CurrentState == ConversationState.SelectOffer)
        {
            var numberMatch = Regex.Match(userMessage, @"\b([1-3])\b");
            if (numberMatch.Success)
            {
                result.OfferSelection = numberMatch.Groups[1].Value;
                result.Intent = "SELECT_OFFER";
            }
        }
    }

    private void AdjustIntentBasedOnContext(string userMessage, AiResult result, ConversationData context)
    {
        var msgLower = userMessage.ToLower();

        // Patrones para identidad
        var identityPatterns = new[]
        {
            "cómo te llamas", "como te llamas", "quién eres", "quien eres",
            "qué eres", "que eres", "tu nombre", "eres un bot"
        };
        if (identityPatterns.Any(p => msgLower.Contains(p)))
            result.Intent = "ASK_IDENTITY";

        // Patrones para reset
        var resetPatterns = new[]
        {
            "olvida todo", "reset", "empezar de nuevo", "otra póliza",
            "otra poliza", "nueva cotización", "nueva cotizacion"
        };
        if (resetPatterns.Any(p => msgLower.Contains(p)))
            result.Intent = "RESET";

        // Ajustes basados en el estado actual
        switch (context.CurrentState)
        {
            case ConversationState.SelectOffer when !string.IsNullOrEmpty(result.OfferSelection):
                result.Intent = "SELECT_OFFER";
                break;

            case ConversationState.ConfirmEmission:
                var confirmPatterns = new[] { "pague", "pagué", "ya pag", "listo", "confirmo", "si", "ok" };
                if (confirmPatterns.Any(p => msgLower.Contains(p)))
                    result.Intent = "CONFIRM_PAYMENT";
                else if (!string.IsNullOrEmpty(result.CodVendedor))
                    result.Intent = "PROVIDE_VENDOR_CODE";
                break;

            case ConversationState.AskForDniAndPlate:
                if (!string.IsNullOrEmpty(result.Dni) || !string.IsNullOrEmpty(result.Plate))
                    result.Intent = "PROVIDE_DNI_PLATE";
                break;
        }

        // Si hay email en el contexto, podría ser actualización de datos
        if (!string.IsNullOrEmpty(result.Email) && result.Intent == "UNKNOWN")
            result.Intent = "PROVIDE_DNI_PLATE";

        // Fallback final
        if (result.Intent == "UNKNOWN")
            result.Intent = "FALLBACK";
    }

    private AiResult CreateFallbackResult(string userMessage)
    {
        var result = new AiResult { Intent = "FALLBACK" };

        // Intentar extracciones básicas aún en caso de error
        var dniMatch = Regex.Match(userMessage, @"\b(\d{8})\b");
        if (dniMatch.Success)
        {
            result.Dni = dniMatch.Value;
            result.Intent = "PROVIDE_DNI_PLATE";
        }

        var plateMatch = Regex.Match(userMessage.ToUpper(), @"\b([A-Z]{3}\d{3})\b");
        if (plateMatch.Success)
        {
            result.Plate = plateMatch.Value;
            result.Intent = "PROVIDE_DNI_PLATE";
        }

        return result;
    }

    private async Task<string> ChatAsync(string system, string user, bool expectJson)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, _settings.Endpoint);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

            var body = new
            {
                model = _settings.ModelName,
                temperature = expectJson ? 0.1 : _settings.Temperature, // Menos creatividad para JSON
                max_tokens = expectJson ? 500 : 1000,
                messages = new object[]
                {
                    new { role = "system", content = system },
                    new { role = "user", content = user }
                }
            };

            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            var res = await _http.SendAsync(req);
            res.EnsureSuccessStatusCode();

            var raw = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(raw);
            var content = doc.RootElement.GetProperty("choices")[0]
                .GetProperty("message").GetProperty("content").GetString();

            return content ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en llamada a la API del modelo");
            return string.Empty;
        }
    }

    private string CleanJsonResponse(string json)
    {
        // Eliminar marcas de código
        json = json.Replace("```json", "").Replace("```", "").Trim();

        // Reemplazar comillas simples por dobles (algunos modelos las usan)
        json = Regex.Replace(json, @"'([^']*)':", "\"$1\":");
        json = Regex.Replace(json, @":\s*'([^']*)'", ": \"$1\"");

        return json;
    }

}