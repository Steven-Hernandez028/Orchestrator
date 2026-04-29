using Microsoft.EntityFrameworkCore;
using Orchestrator.Application.Services;
using Orchestrator.Core.Interfaces;
using Orchestrator.Infrastructure.Data;
using Orchestrator.Infrastructure.Data.Repositories;
using Orchestrator.Infrastructure.Mqtt;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<OrchestratorDbContext>(options =>
        options.UseInMemoryDatabase("InMemoryTest"));
}
else
{
    builder.Services.AddDbContext<OrchestratorDbContext>(options =>
        options.UseSqlite("Data Source=orchestrator.db"));
}

// MQTT
builder.Services.AddSingleton<MqttPublisher>();
builder.Services.AddScoped<IMqttPublisher>(sp => sp.GetRequiredService<MqttPublisher>());
builder.Services.AddScoped<MqttTopicRouter>();
builder.Services.AddScoped<IOrchestrationService, OrchestrationService>();
builder.Services.AddHostedService<MqttBrokerService>();
builder.Services.AddHostedService<MqttClientService>();

// Repositories
builder.Services.AddScoped<IDeviceRepository, DeviceRepository>();
builder.Services.AddScoped<IScriptRepository, ScriptRepository>();
builder.Services.AddScoped<ILogRepository, LogRepository>();
builder.Services.AddScoped<IMetricRepository, MetricRepository>();

// Services
builder.Services.AddScoped<IOrchestrationService, OrchestrationService>();

// Logging
builder.Services.AddLogging(config =>
{
    config.ClearProviders();
    config.AddConsole();
});

var app = builder.Build();

// Migrate database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
