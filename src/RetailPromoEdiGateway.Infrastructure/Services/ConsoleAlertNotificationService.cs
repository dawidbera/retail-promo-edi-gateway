using Microsoft.Extensions.Logging;
using RetailPromoEdiGateway.Application.Common.Interfaces;
using System.Threading.Tasks;

namespace RetailPromoEdiGateway.Infrastructure.Services
{
    /// <summary>
    /// Log-based implementation of <see cref="IAlertNotificationService"/> for tracking logistics and EDI discrepancies.
    /// </summary>
    public class ConsoleAlertNotificationService : IAlertNotificationService
    {
        private readonly ILogger<ConsoleAlertNotificationService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsoleAlertNotificationService"/> class.
        /// </summary>
        /// <param name="logger">The structured logger instance.</param>
        public ConsoleAlertNotificationService(ILogger<ConsoleAlertNotificationService> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public Task SendAlertAsync(string alertType, string message)
        {
            // Log as structured warnings for Serilog to pickup and write to logs
            _logger.LogWarning("ALERT [{AlertType}]: {AlertMessage}", alertType, message);
            return Task.CompletedTask;
        }
    }
}
