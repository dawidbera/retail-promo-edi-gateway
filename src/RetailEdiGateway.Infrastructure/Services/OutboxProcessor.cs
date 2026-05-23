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
 /// Background service to implement the Outbox pattern. 
 /// Periodically processes pending outbound EDI messages (e.g., ORDERS) and simulates transmission to suppliers.
 /// </summary>
 public class OutboxProcessor : BackgroundService
 {
 private readonly IServiceScopeFactory _scopeFactory;
 private readonly ILogger<OutboxProcessor> _logger;

 /// <summary>
 /// Initializes a new instance of the <see cref="OutboxProcessor"/> class.
 /// </summary>
 public OutboxProcessor(IServiceScopeFactory scopeFactory, ILogger<OutboxProcessor> logger)
 {
 _scopeFactory = scopeFactory;
 _logger = logger;
 }

 /// <inheritdoc />
 protected override async Task ExecuteAsync(CancellationToken stoppingToken)
 {
 _logger.LogInformation("Outbox Processor background service starting.");

 while (!stoppingToken.IsCancellationRequested)
 {
 try
 {
 await ProcessPendingTransactionsAsync(stoppingToken);
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "An error occurred while processing the outbound EDI queue.");
 }

 // Poll every 10 seconds (configurable for performance)
 await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
 }

 _logger.LogInformation("Outbox Processor background service stopping.");
 }

 /// <summary>
 /// Scans for and processes pending outbound EDI messages in batches, simulating dispatch to third-party suppliers.
 /// </summary>
 /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
 private async Task ProcessPendingTransactionsAsync(CancellationToken cancellationToken)
 {
 using var scope = _scopeFactory.CreateScope();
 var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

 // Retrieve pending outbound transactions
 var pendingTransactions = await context.EdiTransactions
 .Where(t => t.Direction == "OUTBOUND" && t.Status == "PENDING")
 .OrderBy(t => t.ProcessedAt)
 .Take(20) // process in batches
 .ToListAsync(cancellationToken);

 if (!pendingTransactions.Any())
 {
 return;
 }

 _logger.LogInformation("Found {Count} pending outbound EDI transactions to dispatch.", pendingTransactions.Count);

 foreach (var transaction in pendingTransactions)
 {
 try
 {
 transaction.RetryCount++;
 _logger.LogInformation("Attempting outbound EDI transmission for transaction {Id} (Attempt {Attempt}/3). Type: {Type}", 
 transaction.Id, transaction.RetryCount, transaction.MessageType);

 // Simulate outbound network call or transmission queue write
 // We fail simulated transmissions that have "SIMULATE_FAILURE" in the payload
 if (transaction.Payload.Contains("SIMULATE_FAILURE"))
 {
 throw new InvalidOperationException("External EDI partner connection failed.");
 }

 // Successful transmission
 transaction.Status = "SUCCESS";
 transaction.ErrorMessage = null;
 transaction.ProcessedAt = DateTime.UtcNow;
 _logger.LogInformation("Outbound EDI transaction {Id} successfully transmitted.", transaction.Id);
 }
 catch (Exception ex)
 {
 _logger.LogWarning("Failed transmission attempt for transaction {Id}. Error: {Error}", transaction.Id, ex.Message);
 transaction.ErrorMessage = ex.Message;
 
 if (transaction.RetryCount >= 3)
 {
 transaction.Status = "FAILED";
 _logger.LogError("Transaction {Id} has failed after maximum attempts. Marked as FAILED.", transaction.Id);
 }
 }
 }

 await context.SaveChangesAsync(cancellationToken);
 }
 }
}
