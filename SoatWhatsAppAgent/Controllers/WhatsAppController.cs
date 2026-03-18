using Microsoft.AspNetCore.Mvc;
using SoatWhatsAppAgent.Core.Interfaces.Services;
using SoatWhatsAppAgent.Core.Models;
using System.Text;

namespace SoatWhatsAppAgent.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WhatsAppController : ControllerBase
{
    private readonly IConversationFlowService _flow;
    private readonly IPaymentService _wa;

    public WhatsAppController(IConversationFlowService flow, IPaymentService wa)
    {
        _flow = flow;
        _wa = wa;
    }

    // Twilio WhatsApp webhook (form-url-encoded)
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook()
    {
        try
        {
            var form = await Request.ReadFormAsync();

            var phone = form["From"].ToString()?.Replace("whatsapp:", "") ?? string.Empty;
            var text = form["Body"].ToString() ?? string.Empty;
            var mediaUrl = form["MediaUrl0"].ToString(); // Primera imagen/media
            var mediaContentType = form["MediaContentType0"].ToString();
            var numMedia = int.TryParse(form["NumMedia"].ToString(), out var num) ? num : 0;

            // Crear mensaje con información de media
            var incomingMessage = new IncomingMessage
            {
                PhoneNumber = phone,
                Text = text,
                HasMedia = numMedia > 0,
                MediaUrl = mediaUrl,
                MediaContentType = mediaContentType
            };

            var reply = await _flow.HandleMessageAsync(incomingMessage);

            // Devolver TwiML
            var xml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
                <Response>
                    <Message>{System.Security.SecurityElement.Escape(reply)}</Message>
                </Response>";

            return Content(xml, "application/xml", Encoding.UTF8);
        }
        catch (Exception ex)
        {
            var errorXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
                <Response>
                    <Message>Lo siento, ocurrió un error procesando tu mensaje. Por favor intenta nuevamente.</Message>
                </Response>";
            return Content(errorXml, "application/xml", Encoding.UTF8);
        }
    }


    // Endpoint de prueba sin Twilio (JSON)
    [HttpPost("test")]
    public async Task<IActionResult> Test([FromBody] IncomingMessageDto dto)
    {
        try
        {
            var incomingMessage = new IncomingMessage
            {
                PhoneNumber = dto.PhoneNumber,
                Text = dto.Text ?? string.Empty,
                HasMedia = !string.IsNullOrEmpty(dto.MediaUrl),
                MediaUrl = dto.MediaUrl,
                MediaContentType = dto.MediaContentType
            };

            var reply = await _flow.HandleMessageAsync(incomingMessage);
            return Ok(new { reply });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = "Error procesando el mensaje" });
        }
    }

    [HttpPost("test-image")]
    public async Task<IActionResult> TestWithImage([FromForm] TestImageDto dto, IFormFile? image)
    {
        try
        {
            string? mediaUrl = null;
            string? mediaContentType = null;

            if (image != null && image.Length > 0)
            {
                // En producción, subirías esto a un storage (AWS S3, Azure Blob, etc.)
                // Por ahora, simulamos con la imagen en memoria
                var fileName = $"test_image_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{image.ContentType.Split('/').LastOrDefault()}";
                mediaUrl = $"data:{image.ContentType};base64,{Convert.ToBase64String(await GetBytesFromFormFile(image))}";
                mediaContentType = image.ContentType;
            }

            var incomingMessage = new IncomingMessage
            {
                PhoneNumber = dto.PhoneNumber,
                Text = dto.Text ?? string.Empty,
                HasMedia = !string.IsNullOrEmpty(mediaUrl),
                MediaUrl = mediaUrl,
                MediaContentType = mediaContentType
            };

            var reply = await _flow.HandleMessageAsync(incomingMessage);
            return Ok(new { reply, hasImage = !string.IsNullOrEmpty(mediaUrl) });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = "Error procesando la imagen" });
        }
    }

    private async Task<byte[]> GetBytesFromFormFile(IFormFile formFile)
    {
        using var memoryStream = new MemoryStream();
        await formFile.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }
}
