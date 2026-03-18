using Microsoft.EntityFrameworkCore;
using SoatWhatsAppAgent.Core.Interfaces.Services;
using SoatWhatsAppAgent.Core.Models;
using SoatWhatsAppAgent.Infrastructure.Data;
using SoatWhatsAppAgent.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddMemoryCache();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---------- Configuration bindings ----------
builder.Services.Configure<ModelSettings>(builder.Configuration.GetSection("Groq"));
builder.Services.Configure<TwilioSettings>(builder.Configuration.GetSection("Twilio"));

builder.Services.AddHttpClient<IPaymentService, SimulatedPaymentService>(client =>
{
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});
builder.Services.AddHttpClient<IImageProcessingService, ImageProcessingService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});

// ---------- EF Core (SQL Server) ----------
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// ---------- DI Services ----------
builder.Services.AddSingleton<IAiResponseService, AiResponseService>();
builder.Services.AddSingleton<IClientApiService, SimulatedClientApiService>();
builder.Services.AddSingleton<IPlateApiService, SimulatedPlateApiService>();
builder.Services.AddSingleton<IOfferApiService, SimulatedOfferApiService>();
builder.Services.AddSingleton<IPaymentService, SimulatedPaymentService>();
builder.Services.AddSingleton<IPolicyService, SimulatedPolicyService>();
builder.Services.AddSingleton<IWhatsAppService, WhatsAppService>();
builder.Services.AddScoped<IConversationFlowService, ConversationFlowService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger();
    app.UseSwaggerUI();
//}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Urls.Add($"http://*:{port}");

app.Run();
