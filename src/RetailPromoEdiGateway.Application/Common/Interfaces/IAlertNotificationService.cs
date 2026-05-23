using System.Threading.Tasks;

namespace RetailPromoEdiGateway.Application.Common.Interfaces
{
    /// <summary>
    /// Service to handle outbound alert notifications (e.g. logs, emails, webhooks) for logistics mismatches.
    /// </summary>
    public interface IAlertNotificationService
    {
        /// <summary>
        /// Sends an alert with a specified type and detailed error message.
        /// </summary>
        /// <param name="alertType">The category of the alert (e.g., "Missing ORDRSP", "Missing DESADV").</param>
        /// <param name="message">The detailed description of the alert occurrence.</param>
        /// <returns>A task that represents the asynchronous notification operation.</returns>
        Task SendAlertAsync(string alertType, string message);
    }
}
