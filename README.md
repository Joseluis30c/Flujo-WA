# 📲 Flujo-WA

![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![C#](https://img.shields.io/badge/C%23-Language-blue)
![Twilio](https://img.shields.io/badge/Twilio-WhatsApp-red)
![AI](https://img.shields.io/badge/AI-Groq-green)
![Status](https://img.shields.io/badge/Status-Active-success)

Sistema de **automatización de conversaciones de WhatsApp utilizando inteligencia artificial**, desarrollado con **.NET 8, Twilio y Groq AI** para crear flujos conversacionales inteligentes.

---
# 📚 Tabla de Contenido

- Descripción
- Objetivo del proyecto
- Tecnologías utilizadas
- Funcionalidades
- Estructura del proyecto
- Cómo ejecutar el proyecto
- Ejemplo de código
- Mejoras futuras
- Autor
- Apoya el proyecto

---

# 🧠 Descripción

**Flujo-WA** es una aplicación backend desarrollada en **.NET 8** que permite automatizar conversaciones en **WhatsApp** utilizando **Twilio** como proveedor de mensajería y **Groq AI** para generar respuestas inteligentes mediante modelos de lenguaje.

El sistema permite crear flujos conversacionales dinámicos capaces de:

- Procesar mensajes entrantes de WhatsApp
- Interpretar preguntas de los usuarios
- Generar respuestas usando inteligencia artificial
- Automatizar procesos de atención o soporte

Este tipo de soluciones se utiliza comúnmente en:

- Atención al cliente automatizada
- Chatbots empresariales
- Automatización de consultas
- Asistentes virtuales

------------------------------------------------------------------------

# 🎯 Objetivo del proyecto

El objetivo de este proyecto es demostrar cómo integrar diferentes tecnologías modernas para crear un **chatbot inteligente para WhatsApp**.

Entre los objetivos principales:

- Integrar **WhatsApp mediante Twilio**
- Utilizar **Groq AI para generación de respuestas**
- Implementar una arquitectura simple y escalable en **.NET 8**
- Servir como **ejemplo educativo para desarrolladores**


------------------------------------------------------------------------

# 🛠 Tecnologías utilizadas

Las principales tecnologías utilizadas en este proyecto son:
- **SQL Server**
- **C#**
- **.NET 8**
- **ASP.NET Core Web API**
- **Twilio API (WhatsApp)**
- **Groq AI API**
- **HTTP Client**
- **JSON**
  
------------------------------------------------------------------------

# ⚙️ Funcionalidades

El proyecto incluye las siguientes funcionalidades:

- Recepción de mensajes desde WhatsApp
- Integración con **Twilio Webhooks**
- Generación de respuestas usando **Groq AI**
- Procesamiento de mensajes entrantes
- Automatización de flujos conversacionales
- Arquitectura modular en .NET 

------------------------------------------------------------------------

# 📂 Estructura del proyecto

    GeneradorHTML_PDF/
    ├── SoatWhatsAppAgent.Core/
    |  ├──Interface/
    |  ├──Models/
    ├── SoatWhatsAppAgent.Infrastructure/
    |  ├──Data/
    |  ├──Services/
    ├── SoatWhatsAppAgent/
    |  ├──Controllers/
    |  ├──Programs.cs
------------------------------------------------------------------------

# ▶️ Cómo ejecutar el proyecto

### 1️⃣ Clonar el repositorio

``` bash
git clone https://github.com/Joseluis30c/Flujo-WA.git
```

### 2️⃣ Configurar variables en appsettings.json

``` json
 {
  "GroqApiKey": "TU_API_KEY",
  "TwilioAccountSid": "TU_SID",
  "TwilioAuthToken": "TU_TOKEN"
}
```

### 3️⃣ Ejecutar el proyecto
``` bash
 dotnet run
```
------------------------------------------------------------------------

# 💡 Ejemplo de código

Ejemplo de controlador que recibe mensajes de Twilio:

``` c#
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
```

------------------------------------------------------------------------

# 🚀 Mejoras futuras

Algunas mejoras que podrían agregarse al proyecto:

-   Dashboard de conversaciones
-   Analítica de uso
-   Integración con CRM
------------------------------------------------------------------------

# 👨‍💻 Autor

**Jose Luis Chavesta Rivas**

GitHub\
https://github.com/Joseluis30c

------------------------------------------------------------------------

# ⭐ Apoya el proyecto

Si este proyecto te resulta útil o interesante:

⭐ Dale una estrella al repositorio en GitHub.
