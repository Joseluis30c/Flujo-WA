using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SoatWhatsAppAgent.Core.Enums;
using SoatWhatsAppAgent.Core.Interfaces.Services;
using SoatWhatsAppAgent.Core.Models;
using SoatWhatsAppAgent.Infrastructure.Data;
using System.Text.RegularExpressions;

namespace SoatWhatsAppAgent.Infrastructure.Services;

public class ConversationFlowService : IConversationFlowService
{
    private readonly IAiResponseService _ai; 
    private readonly IClientApiService _clientApi;
    private readonly IPlateApiService _plateApi;
    private readonly IOfferApiService _offerApi;
    private readonly IPaymentService _payment;
    private readonly IPolicyService _policy;
    private readonly IImageProcessingService _imageProcessing;
    private readonly IMemoryCache _cache;
    private readonly AppDbContext _db;
    private readonly Dictionary<ConversationState, Func<string, ConversationData, Core.Entities.Conversation, AiResult, Task<string>>> _handlers;
    private const int MAX_CONVERSATION_HISTORY = 5; // Últimos 5 intercambios

    public ConversationFlowService(
    IAiResponseService ai,
    IClientApiService clientApi,
    IPlateApiService plateApi,
    IOfferApiService offerApi,
    IPaymentService payment,
    IPolicyService policy,
    IImageProcessingService imageProcessing,
    IMemoryCache cache,
    AppDbContext db)
    {
        _ai = ai;
        _clientApi = clientApi;
        _plateApi = plateApi;
        _offerApi = offerApi;
        _payment = payment;
        _policy = policy;
        _imageProcessing = imageProcessing;
        _cache = cache;
        _db = db;

        _handlers = new()
        {
            { ConversationState.AskForDniAndPlate, (text, c, conv, nlu) => HandleAskForDniAndPlateAsync(text, c, conv, nlu) },
            { ConversationState.ShowOffers, (text, c, conv, nlu) => HandleShowOffersAsync(text, c, conv, nlu) },
            { ConversationState.SelectOffer, (text, c, conv, nlu) => HandleSelectOfferAsync(text, c, conv, nlu) },
            { ConversationState.GeneratePaymentLink, (text, c, conv, nlu) => HandleGeneratePaymentLinkAsync(text, c, conv, nlu) },
            { ConversationState.ConfirmEmission, (text, c, conv, nlu) => HandleConfirmEmissionAsync(text, c, conv, nlu) }
        };
    }

    public async Task<string> HandleMessageAsync(IncomingMessage incomingMessage)
    {
        // 0) cargar/crear sesión desde memoria
        var c = await GetOrCreateConversationDataAsync(incomingMessage.PhoneNumber);

        // 1) Guardar inbound en DB
        var conv = await GetOrCreateConversationEntityAsync(c);
        await SaveInboundMessageAsync(conv.Id, incomingMessage.Text, incomingMessage.MediaUrl);

        // Cargar historial reciente para contexto del modelo
        var conversationHistory = await GetConversationHistoryAsync(conv.Id);
        c.ConversationHistory = conversationHistory;

        // 2) NUEVO: Verificar si es una imagen de comprobante de pago
        if (incomingMessage.HasMedia && incomingMessage.IsImage &&
        c.CurrentState == ConversationState.ConfirmEmission)
        {
            return await HandlePaymentReceiptImageAsync(incomingMessage, c, conv);
        }

        // 3) NLU con historial de contexto (solo para mensajes de texto)
        var nlu = await _ai.ExtractAsync(incomingMessage.Text, c);

        // 4) Actualizar datos capturados
        UpdateCapturedData(c, nlu);

        // 5) Procesar intents especiales
        if (await HandleSpecialIntents(nlu, c, conv) is string specialResponse)
            return specialResponse;

        // 6) Manejar saludo inicial
        if (await HandleGreeting(nlu, c, conv) is string greetingResponse)
            return greetingResponse;

        // 7) Validar y procesar datos de entrada
        if (await HandleDataInput(nlu, c, conv) is string dataResponse)
            return dataResponse;

        // 8) Validar contra APIs
        if (await ValidateAgainstAPIs(nlu, c, conv) is string validationResponse)
            return validationResponse;

        // 9) Flujo por estado
        if (_handlers.TryGetValue(c.CurrentState, out var handler))
            return await handler(incomingMessage.Text, c, conv, nlu);

        // Fallback
        return await ReplyAsync(c, conv, ReturnMessages.EvaluateContext, nlu);
    }

    #region Handlers por estado
    private async Task<ConversationData> GetOrCreateConversationDataAsync(string phoneNumber)
    {
        if (_cache.TryGetValue(phoneNumber, out ConversationData? cached) && cached != null)
            return cached;

        var c = new ConversationData { PhoneNumber = phoneNumber };
        _cache.Set(phoneNumber, c, TimeSpan.FromMinutes(30));
        return c;
    }

    private async Task<Core.Entities.Conversation> GetOrCreateConversationEntityAsync(ConversationData c)
    {
        var conv = await _db.Conversations.FirstOrDefaultAsync(x => x.PhoneNumber == c.PhoneNumber);
        if (conv == null)
        {
            conv = new Core.Entities.Conversation
            {
                PhoneNumber = c.PhoneNumber,
                State = c.CurrentState,
                CreatedUtc = DateTime.UtcNow,
                LastInteractionUtc = DateTime.UtcNow
            };
            _db.Conversations.Add(conv);
            await _db.SaveChangesAsync();
        }
        else
        {
            conv.InteractionCount++;
            conv.LastInteractionUtc = DateTime.UtcNow;
        }
        return conv;
    }

    private async Task SaveInboundMessageAsync(Guid conversationId, string message, string? mediaUrl = null)
    {
        var newMessage = new Core.Entities.Message
        {
            ConversationId = conversationId,
            Direction = "inbound",
            Text = message,
            CreatedUtc = DateTime.UtcNow,
            MediaUrl = mediaUrl,
            HasMedia = !string.IsNullOrEmpty(mediaUrl)
        };

        _db.Messages.Add(newMessage);
        await _db.SaveChangesAsync();
    }

    private async Task<List<ConversationMessage>> GetConversationHistoryAsync(Guid conversationId)
    {
        var messages = await _db.Messages
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedUtc)
            .Take(MAX_CONVERSATION_HISTORY * 2)
            .OrderBy(m => m.CreatedUtc)
            .Select(m => new ConversationMessage
            {
                Role = m.Direction == "inbound" ? "user" : "assistant",
                Content = m.Text,
                Timestamp = m.CreatedUtc
            })
            .ToListAsync();

        return messages;
    }

    private void UpdateCapturedData(ConversationData c, AiResult nlu)
    {
        if (!string.IsNullOrWhiteSpace(nlu.Dni)) c.Dni = nlu.Dni;
        if (!string.IsNullOrWhiteSpace(nlu.Plate)) c.Plate = nlu.Plate;
        if (!string.IsNullOrWhiteSpace(nlu.ClientName)) c.ClientName = nlu.ClientName;
        if (!string.IsNullOrWhiteSpace(nlu.Email)) c.Email = nlu.Email?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(nlu.BirthDate)) c.BirthDate = NormalizeDate(nlu.BirthDate);
        if (!string.IsNullOrWhiteSpace(nlu.Gender)) c.Gender = NormalizeGender(nlu.Gender);
    }

    private async Task<string?> HandleSpecialIntents(AiResult nlu, ConversationData c, Core.Entities.Conversation conv)
    {
        return nlu.Intent switch
        {
            "RESET" => await ResetAsync(c, conv, nlu),
            "FALLBACK" => await ReplyAsync(c, conv, ReturnMessages.EvaluateContext, nlu),
            "ASK_IDENTITY" => await ReplyAsync(c, conv, ReturnMessages.AskIdentity, nlu),
            _ => null
        };
    }

    private async Task<string?> HandleGreeting(AiResult nlu, ConversationData c, Core.Entities.Conversation conv)
    {
        if (nlu.Intent == "GREETING" || (c.CurrentState == ConversationState.Greeting && !c.HasGreeted))
        {
            c.CurrentState = ConversationState.AskForDniAndPlate;
            c.HasGreeted = true;
            await PersistAsync(c, conv, ReturnMessages.Greeting);
            return ReturnMessages.Greeting;
        }
        return null;
    }

    private async Task<string?> HandleDataInput(AiResult nlu, ConversationData c, Core.Entities.Conversation conv)
    {
        if (nlu.Intent == "PROVIDE_DNI_PLATE")
        {
            if (string.IsNullOrEmpty(c.Dni) || string.IsNullOrEmpty(c.Plate))
            {
                c.CurrentState = ConversationState.AskForDniAndPlate;
                c.SelectedOfferId = null;
                conv.SelectedOfferId = null;
                c.CachedOffers = null;

                var validationError = ValidateBasicInputs(nlu);
                if (validationError != null)
                    return await ReplyAsync(c, conv, validationError, nlu);

                var missing = DetermineMissingData(c);
                if (missing != null)
                    return await ReplyAsync(c, conv, missing, nlu);
            }
        }
        return null;
    }

    private async Task<string?> ValidateAgainstAPIs(AiResult nlu, ConversationData c, Core.Entities.Conversation conv)
    {
        // Validar cliente
        if (!string.IsNullOrWhiteSpace(c.Dni))
        {
            var clientValidation = await ValidateClientAsync(c, conv, nlu);
            if (clientValidation != null) return clientValidation;
        }

        // Validar placa
        if (!string.IsNullOrWhiteSpace(c.Plate))
        {
            var plateValidation = await ValidatePlateAsync(c, conv);
            if (plateValidation != null) return plateValidation;
        }

        return null;
    }

    private async Task<string?> ValidateClientAsync(ConversationData c, Core.Entities.Conversation conv, AiResult nlu)
    {
        var (clientExists, fullName, email) = await _clientApi.ValidateClientAsync(c.Dni!);

        if (!string.IsNullOrWhiteSpace(nlu.Email)) email = nlu.Email.Trim().ToLowerInvariant();

        if (!clientExists)
        {
            return await HandleNewClientRegistration(c, conv, nlu);
        }
        //else if (string.IsNullOrWhiteSpace(email))
        //{
        //    await PersistAsync(c, conv, ReturnMessages.RequeridEmail);
        //    return ReturnMessages.RequeridEmail;
        //}
        else
        {
            return await HandleExistingClient(c, conv, nlu, fullName, email);
        }
    }

    private async Task<string?> HandleNewClientRegistration(ConversationData c, Core.Entities.Conversation conv, AiResult nlu)
    {
        c.ClientName = null;
        conv.ClientName = null;

        UpdateClientRegistrationData(c, nlu);

        var missingFields = GetMissingRegistrationFields(c);
        if (missingFields.Any())
        {
            return await ReplyAsync(
                c, conv,
                $"Indicale que para registrarlo envíe en UN solo mensaje: {string.Join(", ", missingFields)}. " +
                "Ejemplo: 'Soy QOA TPA, nací 21-09-2021, M'.",
                nlu
            );
        }

        var (created, savedName, error) = await _clientApi.RegisterClientAsync(new ClientDto
        {
            Dni = c.Dni!,
            ClientName = c.ClientName!,
            BirthDate = c.BirthDate!,
            Gender = c.Gender!,
            Email = c.Email!
        });

        if (!created)
        {
            return await ReplyAsync(
                c, conv,
                $"No pude registrarte: {error}. Por favor corrige ese dato y vuelve a enviarlo en un solo mensaje.",
                nlu
            );
        }

        c.ClientName = savedName ?? c.ClientName;
        conv.ClientName = c.ClientName;
        c.CurrentState = ConversationState.ShowOffers;
        return null;
    }

    private async Task<string?> HandleExistingClient(ConversationData c, Core.Entities.Conversation conv, AiResult nlu, string fullName, string email)
    {
        c.ClientName = fullName;
        c.Email = email;
        conv.ClientName = fullName;

        // Si el usuario envió un correo nuevo, actualizarlo
        if (!string.IsNullOrWhiteSpace(nlu.Email))
        {
            c.Email = nlu.Email.Trim().ToLowerInvariant();
            await _clientApi.UpdateClientAsync(new ClientDto
            {
                Dni = c.Dni!,
                ClientName = c.ClientName!,
                BirthDate = c.BirthDate ?? string.Empty,
                Gender = c.Gender ?? string.Empty,
                Email = c.Email
            });
        }
        return null;
    }

    private async Task<string?> ValidatePlateAsync(ConversationData c, Core.Entities.Conversation conv)
    {
        var (plateValid, _, _) = await _plateApi.ValidatePlateAsync(c.Plate!);
        if (!plateValid)
        {
            await PersistAsync(c, conv, ReturnMessages.FailedPlate);
            return ReturnMessages.FailedPlate;
        }
        return null;
    }

    private void UpdateClientRegistrationData(ConversationData c, AiResult nlu)
    {
        if (!string.IsNullOrWhiteSpace(nlu.ClientName)) c.ClientName = nlu.ClientName.Trim();
        if (!string.IsNullOrWhiteSpace(nlu.BirthDate)) c.BirthDate = NormalizeDate(nlu.BirthDate!);
        if (!string.IsNullOrWhiteSpace(nlu.Gender)) c.Gender = NormalizeGender(nlu.Gender!);
        if (!string.IsNullOrWhiteSpace(nlu.Email)) c.Email = nlu.Email.Trim().ToLowerInvariant();
    }

    private string? DetermineMissingData(ConversationData c)
    {
        return (string.IsNullOrEmpty(c.Dni), string.IsNullOrEmpty(c.Plate)) switch
        {
            (true, true) => "Pide DNI y placa, de forma amable y breve.",
            (true, false) => "Pide solo DNI, de forma amable y breve.",
            (false, true) => "Pide solo placa, de forma amable y breve.",
            _ => null
        };
    }

    #endregion

    #region Handlers por estado

    private async Task<string> HandleAskForDniAndPlateAsync(string msg, ConversationData c, Core.Entities.Conversation conv, AiResult nlu)
    {
        if (c.CurrentState == ConversationState.AskForDniAndPlate || c.CurrentState == ConversationState.Greeting)
        {
            c.CurrentState = ConversationState.ShowOffers;
            return await HandleShowOffersAsync(msg, c, conv, nlu);
        }
        return ReturnMessages.FailedContext;
    }

    private async Task<string> HandleShowOffersAsync(string msg, ConversationData c, Core.Entities.Conversation conv, AiResult nlu)
    {
        if (c.CachedOffers?.Any() != true)
        {
            c.CachedOffers = await _offerApi.GetOffersAsync(c.Dni!, c.Plate!);
        }

        if (c.CachedOffers?.Any() != true)
            return await ReplyAsync(c, conv, ReturnMessages.FailedToGetOffers, nlu);

        var simplifiedOffers = c.CachedOffers
            .Select(o => new { o.Vendor, o.Description, o.Price })
            .ToList();

        var modelText = await ReplyAsync(c, conv, ReturnMessages.ShowOfert, nlu, new { simplifiedOffers });

        if (!string.IsNullOrWhiteSpace(modelText))
            c.CurrentState = ConversationState.SelectOffer;

        return modelText;
    }

    private async Task<string> HandleSelectOfferAsync(string msg, ConversationData c, Core.Entities.Conversation conv, AiResult nlu)
    {
        if (msg.ToLower().Contains("recomienda"))
        {
            return await ReplyAsync(c, conv, "El usuario pidió recomendación. Sugiere la oferta con menor precio como la más conveniente.", nlu);
        }

        if (string.IsNullOrEmpty(c.SelectedOfferId))
        {
            c.SelectedOfferId = ExtractOfferSelection(msg, c.CachedOffers);
        }

        var chosen = c.CachedOffers?.FirstOrDefault(o => string.Equals(o.Id, c.SelectedOfferId, StringComparison.OrdinalIgnoreCase));
        if (chosen == null)
        {
            c.SelectedOfferId = null;
            return await ReplyAsync(c, conv, ReturnMessages.FailedOffertSelection, nlu);
        }

        c.CurrentState = ConversationState.GeneratePaymentLink;
        return await HandleGeneratePaymentLinkAsync(msg, c, conv, nlu);
    }

    private async Task<string> HandleGeneratePaymentLinkAsync(string msg, ConversationData c, Core.Entities.Conversation conv, AiResult nlu)
    {
        c.PaymentLink = await _payment.GeneratePaymentLink(c.SelectedOfferId!, c.Dni!, c.Plate!);
        var modelText = await ReplyAsync(c, conv, ReturnMessages.PaymentLink, nlu);

        if (!string.IsNullOrWhiteSpace(modelText))
            c.CurrentState = ConversationState.ConfirmEmission;

        return modelText;
    }

    private async Task<string> HandleConfirmEmissionAsync(string msg, ConversationData c, Core.Entities.Conversation conv, AiResult nlu)
    {
        var userConfirms = IsPaymentConfirmation(msg);
        var hasReceiptData = c.PaymentReceiptData != null;

        // Si ya tenemos datos del comprobante procesado, saltar validación manual
        if (!hasReceiptData && !userConfirms && !c.HasConfirmPay)
        {
            return await ReplyAsync(c, conv, ReturnMessages.FailedConfirmationPay, nlu);
        }

        c.HasConfirmPay = true;

        // Si no tenemos datos del comprobante, usar validación tradicional
        //if (!hasReceiptData)
        //{
        //    _payment.MarkAsPaid(c.PaymentLink);
        //}
        _payment.MarkAsPaid(c.PaymentLink);
        var paid = await _payment.IsPaymentConfirmedAsync(c.PaymentLink!);
        
        if (!paid)
        {
            return await ReplyAsync(c, conv, ReturnMessages.FailedToPayment, nlu);
        }

        if (string.IsNullOrWhiteSpace(nlu.Email))
        {
            var codeMessage = "";
            if (hasReceiptData)
            {
                codeMessage += $"\n\n🎉 *¡Listo!* Tu pago fue validado con éxito.\r\n\r\n*✅ COMPROBANTE DE PAGO VALIDADO*\n" +
                              $"• Monto: S/ {c.PaymentReceiptData!.Amount:F2}\n" +
                              $"• Método: {c.PaymentReceiptData.PaymentMethod}\n" +
                              $"• N° Operación: {c.PaymentReceiptData.TransactionId}\n\n";
            }

            codeMessage += ReturnMessages.RequeridEmail;
            await PersistAsync(c, conv, codeMessage);
            return codeMessage;
        }

        var policyNumber = await _policy.IssuePolicy(c.Dni!, c.Plate!, c.SelectedOfferId!);
        c.PolicyIssued = true;

        // Contexto enriquecido para el mensaje final
        var contextData = new
        {
            policyNumber,
            receiptInfo = hasReceiptData ? new
            {
                amount = c.PaymentReceiptData.Amount,
                method = c.PaymentReceiptData.PaymentMethod,
                transactionId = c.PaymentReceiptData.TransactionId
            } : null
        };

        var issued = await ReplyAsync(c, conv, ReturnMessages.ShowSale, nlu, contextData);

        // Reset después de completar
        await ResetContextAsync(c, conv);
        return issued;
    }

    #endregion

    #region Helpers
    private string? ExtractOfferSelection(string msg, List<OfferDto>? offers)
    {
        // Primero intentar extraer dígito
        var firstDigit = new string(msg.Where(char.IsDigit).ToArray());
        if (!string.IsNullOrEmpty(firstDigit) && offers != null && offers.Count >= int.Parse(firstDigit))
            return firstDigit;

        // Luego intentar por nombre
        if (offers != null)
        {
            string Normalize(string text) => new string(text.ToLower()
                .Replace("la ", "").Replace("el ", "")
                .Normalize(System.Text.NormalizationForm.FormD)
                .Where(c => char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                .ToArray());

            var normalizedMsg = Normalize(msg);
            var chosenByName = offers.FirstOrDefault(o =>
            {
                var vendor = Normalize(o.Vendor);
                var description = Normalize(o.Description);
                return vendor.Contains(normalizedMsg) || normalizedMsg.Contains(vendor) ||
                       description.Contains(normalizedMsg) || normalizedMsg.Contains(description);
            });

            return chosenByName?.Id;
        }

        return null;
    }

    private static bool IsPaymentConfirmation(string msg)
    {
        var confirmWords = new[] { "pague", "pagué", "ya pague", "ya pagué", "listo", "ok", "he pagado", "confirmo", "confirmar", "si" };
        var lower = msg.ToLower();
        return confirmWords.Any(w => lower.Contains(w));
    }

    private string? ValidateBasicInputs(AiResult nlu)
    {
        if (!string.IsNullOrEmpty(nlu.Dni) && nlu.Dni.Length != 8)
            return ReturnMessages.FailedPlate;

        if (!string.IsNullOrEmpty(nlu.Plate) && nlu.Plate.Length != 6)
            return ReturnMessages.FailedPlate;

        return null;
    }

    private async Task<string> ReplyAsync(ConversationData c, Core.Entities.Conversation conv, string prompt, AiResult nlu, object? extra = null)
    {
        var text = await _ai.ComposeAsync((c.HasGreeted ? "No saludar. " : string.Empty) + prompt, c, nlu, extra);
        c.HasGreeted = true;
        await PersistAsync(c, conv, text);
        return text;
    }

    private async Task<string> ResetAsync(ConversationData c, Core.Entities.Conversation conv, AiResult nlu)
    {
        await ResetContextAsync(c, conv);
        await PersistAsync(c, conv, ReturnMessages.Greeting);
        return ReturnMessages.Greeting;
    }

    private async Task PersistAsync(ConversationData c, Core.Entities.Conversation conv, string outbound)
    {
        // Guardar en memoria
        _cache.Set(c.PhoneNumber, c, TimeSpan.FromMinutes(30));

        // Actualizar conversación
        conv.State = c.CurrentState;
        conv.Dni = c.Dni;
        conv.Plate = c.Plate;
        conv.SelectedOfferId = c.SelectedOfferId;
        conv.PaymentLink = c.PaymentLink;
        conv.PolicyIssued = c.PolicyIssued;
        conv.ClientName = c.ClientName;
        conv.LastInteractionUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Guardar mensaje outbound
        _db.Messages.Add(new Core.Entities.Message
        {
            ConversationId = conv.Id,
            Direction = "outbound",
            Text = outbound,
            CreatedUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    private async Task ResetContextAsync(ConversationData c, Core.Entities.Conversation conv)
    {
        var phoneNumber = c.PhoneNumber;
        c = new ConversationData { PhoneNumber = phoneNumber };
        _cache.Set(phoneNumber, c, TimeSpan.FromMinutes(30));

        conv.State = ConversationState.AskForDniAndPlate;
        conv.Dni = null;
        conv.Plate = null;
        conv.SelectedOfferId = null;
        conv.PaymentLink = null;
        conv.PolicyIssued = false;
        conv.ClientName = null;
        conv.LastInteractionUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    private static string NormalizeGender(string g)
    {
        g = g.Trim().ToLowerInvariant();
        return g.StartsWith("m") ? "M" : g.StartsWith("f") ? "F" : g.ToUpperInvariant();
    }

    private static string? NormalizeDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;

        if (DateTime.TryParse(s, out var dt))
            return dt.ToString("yyyy-MM-dd");

        s = s.Trim();

        // Formato YYYY-MM-DD
        var m = Regex.Match(s, @"^(\d{4})[-/](\d{2})[-/](\d{2})$");
        if (m.Success) return $"{m.Groups[1].Value}-{m.Groups[2].Value}-{m.Groups[3].Value}";

        // Formato DD-MM-YYYY
        m = Regex.Match(s, @"^(\d{2})[-/](\d{2})[-/](\d{4})$");
        if (m.Success) return $"{m.Groups[3].Value}-{m.Groups[2].Value}-{m.Groups[1].Value}";

        return s;
    }

    private static List<string> GetMissingRegistrationFields(ConversationData c)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(c.ClientName)) missing.Add("nombres y apellidos");
        if (string.IsNullOrWhiteSpace(c.BirthDate)) missing.Add("fecha de nacimiento (DD-MM-YYYY)");
        if (string.IsNullOrWhiteSpace(c.Gender)) missing.Add("sexo (M/F)");
        return missing;
    }

    private async Task<string> HandlePaymentReceiptImageAsync(IncomingMessage message, ConversationData c, Core.Entities.Conversation conv)
    {
        try
        {
            // 1. Procesar imagen con IA
            var receiptData = await _imageProcessing.ExtractPaymentDataFromImageAsync(
                message.MediaUrl!,
                message.MediaContentType!);

            // 2. Validar datos extraídos
            //if (!receiptData.IsValid)
            //{
            //    var errorMessage = receiptData.ErrorMessage ?? "No pude validar el comprobante de pago";
            //    return await ReplyAsync(c, conv,
            //        $"❌ {errorMessage}. Por favor, envía una imagen más clara del comprobante o confirma tu pago escribiendo 'Ya pagué'.",
            //        new AiResult { Intent = "INVALID_RECEIPT" });
            //}

            // 3. Verificar que coincida con los datos de la transacción
            //var selectedOffer = c.CachedOffers?.FirstOrDefault(o => o.Id == c.SelectedOfferId);

            //// 4. Validar monto (con tolerancia del 5%)
            //var expectedAmount = selectedOffer.Price;
            //var tolerance = expectedAmount * 0.05m; // 5% de tolerancia
            //var amountDifference = Math.Abs(receiptData.Amount - expectedAmount);

            //if (amountDifference > tolerance)
            //{
            //    return await ReplyAsync(c, conv,
            //        $"❌ El monto del comprobante (S/ {receiptData.Amount:F2}) no coincide con el precio de tu SOAT (S/ {expectedAmount:F2}). " +
            //        "Por favor, verifica el comprobante y envía la imagen correcta.",
            //        new AiResult { Intent = "AMOUNT_MISMATCH" });
            //}

            // 5. Validar concepto/descripción (opcional, con keywords)
            //var hasValidConcept = string.IsNullOrEmpty(receiptData.Concept) ||
            //                     IsValidPaymentConcept(receiptData.Concept);

            //if (!hasValidConcept)
            //{
            //    return await ReplyAsync(c, conv,
            //        "⚠️ El concepto del pago no parece corresponder a un SOAT. " +
            //        "¿Estás seguro que es el comprobante correcto? Escribe 'Confirmar' si es correcto.",
            //        new AiResult { Intent = "CONCEPT_WARNING" });
            //}

            // 6. Marcar como pagado y guardar datos del comprobante
            //_payment.MarkAsPaid(c.PaymentLink!);

            // 7. Guardar información del comprobante en el contexto
            c.PaymentReceiptData = receiptData;
            //c.HasConfirmPay = true;

            // 8. Proceder con la confirmación automática
            var fakeNlu = new AiResult
            {
                Intent = "CONFIRM_PAYMENT",
                //CodVendedor = "00000" // Valor temporal, el usuario puede cambiarlo después
            };

            return await HandleConfirmEmissionAsync("Comprobante validado", c, conv, fakeNlu);
        }
        catch (Exception ex)
        {
            return await ReplyAsync(c, conv,
                "❌ Ocurrió un error procesando la imagen. Por favor, intenta enviar la imagen nuevamente o confirma tu pago escribiendo 'Ya pagué'.",
                new AiResult { Intent = "IMAGE_PROCESSING_ERROR" });
        }
    }

    #endregion
}
