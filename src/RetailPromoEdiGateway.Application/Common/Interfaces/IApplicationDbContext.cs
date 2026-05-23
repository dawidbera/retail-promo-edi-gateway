using Microsoft.EntityFrameworkCore;
using RetailPromoEdiGateway.Core.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace RetailPromoEdiGateway.Application.Common.Interfaces
{
    /// <summary>
    /// Database context interface to decouple Application logic from Entity Framework Core.
    /// </summary>
    public interface IApplicationDbContext
    {
        /// <summary>
        /// Gets the DbSet for managing campaigns in the data store.
        /// </summary>
        DbSet<Campaign> Campaigns { get; }

        /// <summary>
        /// Gets the DbSet for managing suppliers in the data store.
        /// </summary>
        DbSet<Supplier> Suppliers { get; }

        /// <summary>
        /// Gets the DbSet for managing purchase orders in the data store.
        /// </summary>
        DbSet<PurchaseOrder> PurchaseOrders { get; }

        /// <summary>
        /// Gets the DbSet for managing individual purchase order lines in the data store.
        /// </summary>
        DbSet<PurchaseOrderLine> PurchaseOrderLines { get; }

        /// <summary>
        /// Gets the DbSet for managing inbound/outbound EDI transactions in the data store.
        /// </summary>
        DbSet<EdiTransaction> EdiTransactions { get; }

        /// <summary>
        /// Gets the DbSet for managing warehouse slots in the data store.
        /// </summary>
        DbSet<WarehouseSlot> WarehouseSlots { get; }

        /// <summary>
        /// Saves all changes made in this context asynchronously to the underlying database.
        /// </summary>
        /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
        /// <returns>A task representing the asynchronous save operation, containing the number of state entries written to the database.</returns>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}
