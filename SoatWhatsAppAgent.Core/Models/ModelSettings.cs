namespace SoatWhatsAppAgent.Core.Models;

public class ModelSettings
{
    public string Endpoint { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ModelName { get; set; } = "";
    public double Temperature { get; set; } = 0.3;
}
