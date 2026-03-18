namespace SoatWhatsAppAgent.Core.Enums;

// Estados de conversación actualizados
public enum ConversationState
{
    Greeting = 0,
    AskForDniAndPlate = 1,
    ShowOffers = 2,
    SelectOffer = 3,
    GeneratePaymentLink = 4,
    ConfirmEmission = 5,
    Farewell = 6,

}