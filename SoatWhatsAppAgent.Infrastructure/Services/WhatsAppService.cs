using Microsoft.Extensions.Options;
using SoatWhatsAppAgent.Core.Interfaces.Services;
using SoatWhatsAppAgent.Core.Models;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace SoatWhatsAppAgent.Infrastructure.Services;

public class WhatsAppService : IWhatsAppService
{
    private readonly TwilioSettings _twilio;
    public WhatsAppService(IOptions<TwilioSettings> twilio)
    {
        _twilio = twilio.Value;
        if (!string.IsNullOrWhiteSpace(_twilio.AccountSid))
            TwilioClient.Init(_twilio.AccountSid, _twilio.AuthToken);
    }

    public Task SendMessageAsync(string toPhoneNumber, string message)
    {
        if (string.IsNullOrWhiteSpace(_twilio.AccountSid))
            return Task.CompletedTask;

        MessageResource.Create(
        from: new PhoneNumber(_twilio.FromNumber),
        to: new PhoneNumber($"whatsapp:{toPhoneNumber}"),
        body: message
        );
        return Task.CompletedTask;
    }
}
