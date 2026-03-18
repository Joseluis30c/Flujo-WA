using SoatWhatsAppAgent.Core.Interfaces.Services;
using SoatWhatsAppAgent.Core.Models;
using System.Text.RegularExpressions;

namespace SoatWhatsAppAgent.Infrastructure.Services;

public class SimulatedClientApiService : IClientApiService
{
    private static readonly object _lock = new();
    private static readonly Dictionary<string, ClientDto> Clients = new()
    {
        ["12345678"] = new ClientDto
        {
            Dni = "12345678",
            ClientName = "Juan Pérez",
            BirthDate = "1990-01-01",
            Gender = "M",
            Email = null
        },
        ["87654321"] = new ClientDto
        {
            Dni = "87654321",
            ClientName = "María López",
            BirthDate = "1992-05-10",
            Gender = "F",
            Email = "maria.lopez@example.com"
        },
        ["71395213"] = new ClientDto
        {
            Dni = "71395213",
            ClientName = "Jose Chavesta",
            BirthDate = "1988-11-23",
            Gender = "M",
            Email = "jose.chavesta@example.com"
        }
    };

    public Task<(bool exists, string? fullName, string email)> ValidateClientAsync(string dni)
    {
        if (Clients.TryGetValue(dni, out var client))
            return Task.FromResult((true, client.ClientName, client.Email));
        return Task.FromResult((false, (string?)null, (string?)null));
    }

    public Task<(bool created, string? fullName, string? error)> RegisterClientAsync(ClientDto client)
    {
        // Validaciones
        var error = Validate(client);
        if (!string.IsNullOrEmpty(error))
            return Task.FromResult((false, (string?)null, error));

        lock (_lock)
        {
            if (Clients.ContainsKey(client.Dni))
                return Task.FromResult((false, Clients[client.Dni].ClientName, "El DNI ya existe."));

           // Normalizaciones mínimas
            client.Gender = client.Gender.Trim().ToUpperInvariant();     // M/F
            //client.Email = client.Email.Trim().ToLowerInvariant();

            Clients[client.Dni] = client;
            return Task.FromResult((true, client.ClientName, (string?)null));
        }
    }

    public Task<(bool update, string? fullName, string? error)> UpdateClientAsync(ClientDto client)
    {
        lock (_lock)
        {
            if (Clients.ContainsKey(client.Dni))
            {
                //Actualización si el cliente ya existe
                var existing = Clients[client.Dni];

                if (!string.IsNullOrWhiteSpace(client.Email))
                    existing.Email = client.Email.Trim().ToLowerInvariant();

                Clients[client.Dni] = existing; // sobrescribe
                return Task.FromResult((true, existing.ClientName, (string?)null));
            }
            Clients[client.Dni] = client;
            return Task.FromResult((true, client.ClientName, (string?)null));
        }
    }

    private static string? Validate(ClientDto c)
    {
        if (string.IsNullOrWhiteSpace(c.Dni) || c.Dni.Length != 8 || !c.Dni.All(char.IsDigit))
            return "DNI inválido (deben ser 8 dígitos).";

        if (string.IsNullOrWhiteSpace(c.ClientName) || c.ClientName.Trim().Length < 3)
            return "Nombres y apellidos son obligatorios.";

        // Acepta "YYYY-MM-DD", "DD/MM/YYYY" o "DD-MM-YYYY"
        if (!LooksLikeDate(c.BirthDate))
            return "Fecha de nacimiento inválida. Usa formato YYYY-MM-DD.";

        var g = (c.Gender ?? "").Trim().ToUpperInvariant();
        if (g != "M" && g != "F")
            return "Sexo inválido. Usa 'M' o 'F'.";

        //var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        //if (string.IsNullOrWhiteSpace(c.Email) || !emailRegex.IsMatch(c.Email))
        //    return "Correo electrónico inválido.";

        return null;
    }

    private static bool LooksLikeDate(string s)
    {
        if (DateTime.TryParse(s, out _)) return true; 
        return Regex.IsMatch(s ?? "", @"^\d{4}-\d{2}-\d{2}$");
    }
}
