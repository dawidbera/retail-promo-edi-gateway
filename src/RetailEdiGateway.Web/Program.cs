//-
// <copyright file="Program.cs">
// Entry point and bootstrap configuration for the Retail EDI and Logistics Gateway.
// Configures Serilog structured logging, EF Core PostgreSQL context, MediatR CQRS, memory caching,
// custom hosted background services, OpenTelemetry Tracing/Metrics, and HTTP request pipeline middleware.
// </copyright>
//-

using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RetailEdiGateway.Application.Common.Interfaces;
using RetailEdiGateway.Application.Features.Campaigns.Queries;
using RetailEdiGateway.Infrastructure.Persistence;
using RetailEdiGateway.Infrastructure.Services;
using Serilog;
using System;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog structured logging
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("C:\\Logs\\EDIGateway\\log-.txt", rollingInterval: RollingInterval.Day)
    .WriteTo.OpenTelemetry(options =>
    {
        options.Endpoint = "http://localhost:5317";
        options.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.Grpc;
        options.ResourceAttributes = new Dictionary<string, object>
        {
            ["service.name"] = "RetailEdiGateway.Web"
        };
    })
    .CreateLogger();

builder.Host.UseSerilog();

// Database Configuration (PostgreSQL)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
 ?? "Host=localhost;Database=edigateway;Username=admin;Password=adminpassword";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
 options.UseNpgsql(connectionString, b => b.MigrationsAssembly("RetailEdiGateway.Infrastructure")));

// Decoupled DbContext binding for Application Layer
builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

// Cache and parsing service registrations
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ICacheService, InMemoryCacheService>();
builder.Services.AddSingleton<IEdiParser, EdiParser>();
builder.Services.AddSingleton<IAlertNotificationService, ConsoleAlertNotificationService>();

// Register background workers
builder.Services.AddHostedService<OutboxProcessor>();
builder.Services.AddHostedService<AlertMonitoringService>();
builder.Services.AddHostedService<WmsIntegrationProcessor>();

// CQRS MediatR registration
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetCampaignDashboardQuery).Assembly));

// Setup OpenTelemetry Tracing and Metrics
var serviceName = "RetailEdiGateway.Web";
var serviceVersion = "1.0.0";

builder.Services.AddOpenTelemetry()
 .ConfigureResource(resource => resource.AddService(serviceName: serviceName, serviceVersion: serviceVersion))
 .WithTracing(tracing => tracing
 .AddAspNetCoreInstrumentation()
 .AddEntityFrameworkCoreInstrumentation(options => options.SetDbStatementForText = true)
 .AddHttpClientInstrumentation()
 .AddOtlpExporter(opt =>
 {
 opt.Endpoint = new Uri("http://localhost:5317"); // Send to OTel Collector
 }))
 .WithMetrics(metrics => metrics
 .AddAspNetCoreInstrumentation()
 .AddRuntimeInstrumentation()
 .AddProcessInstrumentation()
 .AddPrometheusExporter()); // Exposes /metrics

// Add MVC services and JSON configuration
builder.Services.AddControllersWithViews();
builder.Services.AddHealthChecks()
 .AddDbContextCheck<ApplicationDbContext>("PostgreSQL");

var app = builder.Build();

// Auto-migrate database on startup if running in development (safest fallback helper)
using (var scope = app.Services.CreateScope())
{
 var services = scope.ServiceProvider;
 try
 {
 var context = services.GetRequiredService<ApplicationDbContext>();
 context.Database.EnsureCreated(); // Ensure DB matches mappings and has seed data
 }
 catch (Exception ex)
 {
 Log.Error(ex, "An error occurred while seeding or checking the database.");
 }
}

if (!app.Environment.IsDevelopment())
{
 app.UseExceptionHandler("/Home/Error");
 app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Enable OpenTelemetry Prometheus scraping endpoint
app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.UseAuthorization();

// Expose standard Health Check endpoint
app.MapHealthChecks("/health");

app.MapControllerRoute(
 name: "default",
 pattern: "{controller=Campaigns}/{action=Index}/{id?}");

app.Run();
