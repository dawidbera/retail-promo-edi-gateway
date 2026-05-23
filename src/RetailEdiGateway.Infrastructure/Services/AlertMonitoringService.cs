using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using RetailEdiGateway.Application.Common.Interfaces;
using RetailEdiGateway.Core.Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RetailEdiGateway.Infrastructure.Services
{
 /// <summary>
 /// Background service to monitor the supply chain data for critical delays,
 /// triggering alerts when ORDRSP responses or DESADV shipping advices are overdue.
 /// </summary>
 public class AlertMonitoringService : BackgroundService
 {
 private readonly IServiceScopeFactory _scopeFactory;
 private readonly IMemoryCache _cache;
 private readonly ILogger<AlertMonitoringService> _logger;

 /// <summary>
 /// Initializes a new instance of the <see cref="AlertMonitoringService"/> class.
 /// </summary>
 public AlertMonitoringService(
 IServiceScopeFactory scopeFactory,
 IMemoryCache cache,
 ILogger<AlertMonitoringService> logger)
 {
 _scopeFactory = scopeFactory;
 _cache = cache;
 _logger = logger;
 }

 /// <inheritdoc />
 protected override async Task ExecuteAsync(CancellationToken stoppingToken)
 {
 _logger.LogInformation("Alert Monitoring background service starting.");

 while (!stoppingToken.IsCancellationRequested)
 {
 try
 {
 await CheckMissingOrdrspAlertsAsync(stoppingToken);
 await CheckMissingDesadvAlertsAsync(stoppingToken);
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "An error occurred during alert monitoring scan.");
 }

 // Scan periodically (every 10 seconds for simulation, normally hourly/daily in production)
 await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
 }

 _logger.LogInformation("Alert Monitoring background service stopping.");
 }

 /// <summary>
 /// Periodically identifies purchase orders in "Sent" status that have had no acknowledgement (ORDRSP) for over 48 hours and dispatches alerts.
 /// </summary>
 /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
 private async Task CheckMissingOrdrspAlertsAsync(CancellationToken cancellationToken)
 {
 using var scope = _scopeFactory.CreateScope();
 var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
 var alertService = scope.ServiceProvider.GetRequiredService<IAlertNotificationService>();

 // Find sent purchase orders that are older than 48 hours and have not received ORDRSP
 var thresholdTime = DateTime.UtcNow.AddHours(-48);

 var overdueOrders = await context.PurchaseOrders
 .Include(o => o.Supplier)
 .Where(o => o.Status == PurchaseOrderStatus.Sent && o.CreatedAt <= thresholdTime)
 .ToListAsync(cancellationToken);

 foreach (var order in overdueOrders)
 {
 string cacheKey = $"Alert_MissingOrdrsp_{order.Id}";
 if (!_cache.TryGetValue(cacheKey, out _))
 {
 string message = $"Purchase Order '{order.ErpOrderNumber}' dispatched to supplier '{order.Supplier.Name}' ({order.Supplier.Code}) at {order.CreatedAt:dd.MM.yyyy HH:mm} remains unacknowledged (Missing ORDRSP) after 48 hours.";
 await alertService.SendAlertAsync("Missing ORDRSP", message);

 // Suppress duplicate alerts for 24 hours
 _cache.Set(cacheKey, true, TimeSpan.FromHours(24));
 }
 }
 }

 /// <summary>
 /// Identifies warehouse delivery slot bookings scheduled in the next 24 hours where the cargo has not been shipped or delivered, raising warning alerts.
 /// </summary>
 /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
 private async Task CheckMissingDesadvAlertsAsync(CancellationToken cancellationToken)
 {
 using var scope = _scopeFactory.CreateScope();
 var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
 var alertService = scope.ServiceProvider.GetRequiredService<IAlertNotificationService>();

 // Find warehouse slots booked within the next 24 hours
 var thresholdTime = DateTime.UtcNow.AddHours(24);

 var activeSlots = await context.WarehouseSlots
 .Include(s => s.PurchaseOrderLine)
 .ThenInclude(l => l.PurchaseOrder)
 .ThenInclude(o => o.Supplier)
 .Where(s => s.Status == WarehouseSlotStatus.Booked && s.BookedTime <= thresholdTime)
 .ToListAsync(cancellationToken);

 foreach (var slot in activeSlots)
 {
 var poLine = slot.PurchaseOrderLine;
 var order = poLine.PurchaseOrder;

 // If the product line or the order itself is not shipped/delivered yet, trigger warning
 if (poLine.Status != PurchaseOrderLineStatus.Shipped && poLine.Status != PurchaseOrderLineStatus.Delivered &&
 order.Status != PurchaseOrderStatus.Shipped && order.Status != PurchaseOrderStatus.Confirmed) // Assuming Confirmed/Mismatched etc are not Shipped
 {
 string cacheKey = $"Alert_MissingDesadv_{slot.Id}";
 if (!_cache.TryGetValue(cacheKey, out _))
 {
 string message = $"No despatch advice (DESADV) received for warehouse slot booked at {slot.BookedTime:dd.MM.yyyy HH:mm} (DC: {slot.DcCode}, Bay: {slot.BayNumber}) for product {poLine.ProductName} ({poLine.ProductCode}) from supplier '{order.Supplier.Name}'. Arrival is less than 24 hours away.";
 await alertService.SendAlertAsync("Missing DESADV", message);

 // Suppress duplicate alerts for 24 hours
 _cache.Set(cacheKey, true, TimeSpan.FromHours(24));
 }
 }
 }
 }
 }
}
