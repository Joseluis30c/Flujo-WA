using Microsoft.AspNetCore.Mvc;
using SoatWhatsAppAgent.Core.Interfaces.Services;
using SoatWhatsAppAgent.Infrastructure.Services;
using System.Text;

namespace SoatWhatsAppAgent.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IOfferApiService _offerService;
    private readonly ILogger<PaymentController> _logger;
    private readonly string _baseUrl;

    public PaymentController(
        IPaymentService paymentService,
        IOfferApiService offerService,
        ILogger<PaymentController> logger,
        IConfiguration configuration)
    {
        _paymentService = paymentService;
        _offerService = offerService;
        _logger = logger;
        _baseUrl = configuration["PaymentSettings:BaseUrl"] ?? "https://localhost:7074";
    }

    // Endpoint para mostrar la página de checkout
    [HttpGet("checkout/{paymentId}")]
    public async Task<IActionResult> Checkout(string paymentId)
    {
        try
        {
            paymentId = paymentId.TrimEnd('.');

            // Obtener información del pago
            var paymentInfo = (_paymentService as SimulatedPaymentService)?.GetPaymentInfo(paymentId);

            if (paymentInfo == null)
            {
                return NotFound("Pago no encontrado");
            }

            // Obtener información de la oferta
            var offers = await _offerService.GetOffersAsync(paymentInfo.Dni, paymentInfo.Plate);
            var selectedOffer = offers?.FirstOrDefault(o => o.Id == paymentInfo.OfferId);

            if (selectedOffer == null)
            {
                return NotFound("Oferta no encontrada");
            }

            // Actualizar información del pago con datos de la oferta
            paymentInfo.Amount = selectedOffer.Price;
            paymentInfo.ProductName = selectedOffer.Description;
            paymentInfo.VendorName = selectedOffer.Vendor;

            // Generar HTML dinámico
            var html = GeneratePaymentHtml(paymentInfo, paymentId);

            return Content(html, "text/html", Encoding.UTF8);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando página de pago para {PaymentId}", paymentId);
            return BadRequest("Error cargando la página de pago");
        }
    }

    // Endpoint para procesar el pago (llamado desde JavaScript)
    [HttpPost("process/{paymentId}")]
    public async Task<IActionResult> ProcessPayment(string paymentId, [FromBody] ProcessPaymentRequest request)
    {
        try
        {
            paymentId = paymentId.TrimEnd('.');
            var paymentInfo = (_paymentService as SimulatedPaymentService)?.GetPaymentInfo(paymentId);

            if (paymentInfo == null)
            {
                return NotFound(new { success = false, message = "Pago no encontrado" });
            }

            if (paymentInfo.IsPaid)
            {
                return BadRequest(new { success = false, message = "Este pago ya fue procesado" });
            }

            // Simular procesamiento (2 segundos de delay)
            await Task.Delay(2000);

            // Marcar como pagado
            var paymentLink = $"{_baseUrl}/api/payment/checkout/{paymentId}";
            _paymentService.MarkAsPaid(paymentLink);

            // Generar datos de transacción
            var transactionId = $"TXN-{DateTime.UtcNow:yyyyMMddHHmmss}-{paymentId[..6]}";

            return Ok(new
            {
                success = true,
                transactionId,
                amount = paymentInfo.Amount,
                method = request.PaymentMethod,
                date = DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm:ss"),
                message = "Pago procesado exitosamente"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando pago {PaymentId}", paymentId);
            return StatusCode(500, new { success = false, message = "Error procesando el pago" });
        }
    }

    private string GeneratePaymentHtml(PaymentInfo payment, string paymentId)
    {
        paymentId = paymentId.TrimEnd('.');
        var baseUrl = $"{_baseUrl}";

        return $@"<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Pago SOAT - {payment.VendorName}</title>
    <link href=""https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/css/bootstrap.min.css"" rel=""stylesheet"">
    <link rel=""stylesheet"" href=""https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.0.0/css/all.min.css"">
    <style>
        .payment-container {{
            max-width: 500px;
            margin: 50px auto;
            padding: 30px;
            border: 1px solid #ddd;
            border-radius: 15px;
            box-shadow: 0 5px 15px rgba(0,0,0,0.1);
            background: white;
        }}
        .payment-method {{
            border: 2px solid #e9ecef;
            border-radius: 10px;
            padding: 15px;
            margin: 10px 0;
            cursor: pointer;
            transition: all 0.3s;
        }}
        .payment-method:hover {{
            border-color: #007bff;
            background-color: #f8f9fa;
        }}
        .payment-method.selected {{
            border-color: #007bff;
            background-color: #e7f3ff;
        }}
        .method-icon {{
            font-size: 24px;
            margin-right: 10px;
            color: #6c757d;
        }}
        .btn-payment {{
            width: 100%;
            padding: 12px;
            font-size: 16px;
            font-weight: bold;
        }}
        .hidden {{
            display: none;
        }}
        .success-animation {{
            animation: successPulse 2s infinite;
        }}
        @keyframes successPulse {{
            0% {{ transform: scale(1); }}
            50% {{ transform: scale(1.05); }}
            100% {{ transform: scale(1); }}
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <!-- PANTALLA PRINCIPAL -->
        <div id=""mainScreen"" class=""payment-container"">
            <div class=""text-center mb-4"">
                <h2><i class=""fas fa-shield-alt""></i> Pago SOAT</h2>
                <div class=""alert alert-info"">
                    <i class=""fas fa-info-circle""></i> <strong>MODO PRUEBA</strong> - Pago simulado
                </div>
            </div>

            <!-- Información del Producto -->
            <div class=""card mb-4"">
                <div class=""card-body"">
                    <h5 class=""card-title"">{payment.VendorName}</h5>
                    <p class=""card-text"">{payment.ProductName}</p>
                    <div class=""mb-2"">
                        <small class=""text-muted"">
                            <i class=""fas fa-id-card""></i> DNI: {payment.Dni}<br>
                            <i class=""fas fa-car""></i> Placa: {payment.Plate}
                        </small>
                    </div>
                    <div class=""d-flex justify-content-between align-items-center"">
                        <span class=""h4 text-success mb-0"">S/ {payment.Amount:F2}</span>
                        <span class=""badge bg-primary"">SOAT</span>
                    </div>
                </div>
            </div>

            <!-- Métodos de Pago -->
            <div class=""mb-4"">
                <h5>Selecciona método de pago:</h5>
                
                <div class=""payment-method"" onclick=""selectMethod('card')"">
                    <div class=""d-flex align-items-center"">
                        <i class=""method-icon fas fa-credit-card""></i>
                        <div>
                            <h6 class=""mb-1"">Tarjeta de Crédito/Débito</h6>
                            <small class=""text-muted"">Visa, Mastercard, Amex</small>
                        </div>
                    </div>
                </div>

                <div class=""payment-method"" onclick=""selectMethod('yape')"">
                    <div class=""d-flex align-items-center"">
                        <i class=""method-icon fas fa-mobile-alt""></i>
                        <div>
                            <h6 class=""mb-1"">Yape</h6>
                            <small class=""text-muted"">Pago rápido con Yape</small>
                        </div>
                    </div>
                </div>

                <div class=""payment-method"" onclick=""selectMethod('transfer')"">
                    <div class=""d-flex align-items-center"">
                        <i class=""method-icon fas fa-university""></i>
                        <div>
                            <h6 class=""mb-1"">Transferencia Bancaria</h6>
                            <small class=""text-muted"">BBVA, BCP, Interbank</small>
                        </div>
                    </div>
                </div>

                <div class=""payment-method"" onclick=""selectMethod('plin')"">
                    <div class=""d-flex align-items-center"">
                        <i class=""method-icon fas fa-bolt""></i>
                        <div>
                            <h6 class=""mb-1"">Plin</h6>
                            <small class=""text-muted"">Pago con Plin</small>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Botones de Acción -->
            <div class=""d-grid gap-2"">
                <button id=""payButton"" class=""btn btn-success btn-payment"" onclick=""processPayment()"" disabled>
                    <i class=""fas fa-lock""></i> PAGAR AHORA - S/ {payment.Amount:F2}
                </button>
                <button class=""btn btn-outline-danger"" onclick=""cancelPayment()"">
                    <i class=""fas fa-times""></i> Cancelar Pago
                </button>
            </div>

            <div class=""mt-3 text-center"">
                <small class=""text-muted"">
                    <i class=""fas fa-shield-alt""></i> Pago 100% seguro - QOA
                </small>
            </div>
        </div>

        <!-- PANTALLA DE PAGO EXITOSO -->
        <div id=""successScreen"" class=""payment-container hidden"">
            <div class=""text-center"">
                <div class=""success-animation"">
                    <i class=""fas fa-check-circle text-success"" style=""font-size: 80px;""></i>
                </div>
                <h2 class=""text-success mt-3"">¡Pago Exitoso!</h2>
                <p class=""lead"">Tu pago ha sido procesado correctamente</p>
                
                <div class=""card mt-4"">
                    <div class=""card-body"">
                        <h5>Resumen de Transacción</h5>
                        <div class=""text-start"">
                            <p><strong>Producto:</strong> {payment.ProductName}</p>
                            <p><strong>Aseguradora:</strong> {payment.VendorName}</p>
                            <p><strong>Monto:</strong> S/ {payment.Amount:F2}</p>
                            <p><strong>Método:</strong> <span id=""paymentMethodUsed""></span></p>
                            <p><strong>ID Transacción:</strong> <span id=""transactionId""></span></p>
                            <p><strong>Fecha:</strong> <span id=""transactionDate""></span></p>
                        </div>
                    </div>
                </div>

                <div class=""alert alert-warning mt-3"">
                    <i class=""fas fa-whatsapp""></i> Vuelve a WhatsApp para continuar con la emisión de tu SOAT
                </div>

                <div class=""mt-4"">
                    <button class=""btn btn-outline-secondary"" onclick=""downloadReceipt()"">
                        <i class=""fas fa-download""></i> Descargar comprobante
                    </button>
                </div>
            </div>
        </div>

        <!-- PANTALLA DE PAGO CANCELADO -->
        <div id=""cancelScreen"" class=""payment-container hidden"">
            <div class=""text-center"">
                <i class=""fas fa-times-circle text-danger"" style=""font-size: 80px;""></i>
                <h2 class=""text-danger mt-3"">Pago Cancelado</h2>
                <p class=""lead"">El proceso de pago ha sido cancelado</p>
                <p>Puedes volver a WhatsApp para intentarlo nuevamente</p>

                <div class=""mt-4"">
                    <button class=""btn btn-outline-secondary"" onclick=""window.close()"">
                        <i class=""fas fa-times""></i> Cerrar ventana
                    </button>
                </div>
            </div>
        </div>
    </div>

    <script>
        const PAYMENT_ID = '{paymentId}';
        const API_BASE_URL = '{baseUrl}';
        let selectedMethod = '';

        function selectMethod(method) {{
            selectedMethod = method;
            
            document.querySelectorAll('.payment-method').forEach(el => {{
                el.classList.remove('selected');
            }});
            
            event.currentTarget.classList.add('selected');
            document.getElementById('payButton').disabled = false;
        }}

        async function processPayment() {{
            if (!selectedMethod) {{
                alert('Por favor selecciona un método de pago');
                return;
            }}

            showLoading();
            
            try {{
                const response = await fetch(`${{API_BASE_URL}}/api/payment/process/${{PAYMENT_ID}}`, {{
                    method: 'POST',
                    headers: {{
                        'Content-Type': 'application/json'
                    }},
                    body: JSON.stringify({{ paymentMethod: selectedMethod }})
                }});

                const data = await response.json();

                if (data.success) {{
                    showSuccessScreen(data);
                }} else {{
                    alert('Error: ' + data.message);
                    resetPayButton();
                }}
            }} catch (error) {{
                console.error('Error procesando pago:', error);
                alert('Error procesando el pago. Por favor intenta nuevamente.');
                resetPayButton();
            }}
        }}

        function showLoading() {{
            const button = document.getElementById('payButton');
            button.innerHTML = '<i class=""fas fa-spinner fa-spin""></i> Procesando pago...';
            button.disabled = true;
        }}

        function resetPayButton() {{
            const button = document.getElementById('payButton');
            button.innerHTML = '<i class=""fas fa-lock""></i> PAGAR AHORA - S/ {payment.Amount:F2}';
            button.disabled = false;
        }}

        function showSuccessScreen(data) {{
            document.getElementById('paymentMethodUsed').textContent = getMethodName(data.method);
            document.getElementById('transactionId').textContent = data.transactionId;
            document.getElementById('transactionDate').textContent = data.date;
            
            document.getElementById('mainScreen').classList.add('hidden');
            document.getElementById('successScreen').classList.remove('hidden');
        }}

        function cancelPayment() {{
            document.getElementById('mainScreen').classList.add('hidden');
            document.getElementById('cancelScreen').classList.remove('hidden');
        }}

        function downloadReceipt() {{
            const receiptContent = `
═══════════════════════════════════════
        COMPROBANTE DE PAGO SOAT
═══════════════════════════════════════

Producto: {payment.ProductName}
Aseguradora: {payment.VendorName}
Monto: S/ {payment.Amount:F2}

DNI: {payment.Dni}
Placa: {payment.Plate}

ID Transacción: ${{document.getElementById('transactionId').textContent}}
Fecha: ${{document.getElementById('transactionDate').textContent}}
Método: ${{document.getElementById('paymentMethodUsed').textContent}}

═══════════════════════════════════════
       QOA - Plataforma Digital
    https://globaltpa.pe/
═══════════════════════════════════════
            `;

            const blob = new Blob([receiptContent], {{ type: 'text/plain' }});
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `comprobante_soat_${{PAYMENT_ID}}.txt`;
            a.click();
            window.URL.revokeObjectURL(url);
        }}

        function getMethodName(method) {{
            const methods = {{
                'card': 'Tarjeta de Crédito/Débito',
                'yape': 'Yape',
                'transfer': 'Transferencia Bancaria',
                'plin': 'Plin'
            }};
            return methods[method] || 'Método de Pago';
        }}

        console.log('✅ Sistema de pago cargado - Payment ID:', PAYMENT_ID);
    </script>
</body>
</html>";
    }
}

public class ProcessPaymentRequest
{
    public string PaymentMethod { get; set; } = "";
}