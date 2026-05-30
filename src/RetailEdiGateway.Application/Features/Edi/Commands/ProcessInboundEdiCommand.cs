using MediatR;
using Microsoft.EntityFrameworkCore;
using RetailEdiGateway.Application.Common.Interfaces;
using RetailEdiGateway.Core.Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RetailEdiGateway.Application.Features.Edi.Commands
{
    /// <summary>
    /// Command to process incoming EDI files (ORDRSP / DESADV) from suppliers.
    /// </summary>
    public class ProcessInboundEdiCommand : IRequest<EdiProcessResponseDto>
    {
        /// <summary>
        /// Gets or sets the raw EDI payload string (EDIFACT/XML format).
        /// </summary>
        public string Payload { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the message type, which should be either "ORDRSP" or "DESADV".
        /// </summary>
        public string MessageType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the code of the supplier that transmitted the message.
        /// </summary>
        public string SupplierCode { get; set; } = string.Empty;
    }

    /// <summary>
    /// Data Transfer Object representing the result of the EDI payload processing.
    /// </summary>
    public class EdiProcessResponseDto
    {
        /// <summary>
        /// Gets or sets a value indicating whether the EDI processing was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets a descriptive message of the outcome or error if failed.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the unique ID of the created or updated EDI transaction.
        /// </summary>
        public Guid TransactionId { get; set; }

        /// <summary>
        /// Gets or sets the purchase order number referenced in the transaction if successfully linked.
        /// </summary>
        public string? OrderNumber { get; set; }
    }

    /// <summary>
    /// Handler to parse the EDI file, update database records, check for discrepancies, and register alerts.
    /// </summary>
    public class ProcessInboundEdiCommandHandler : IRequestHandler<ProcessInboundEdiCommand, EdiProcessResponseDto>
    {
        private readonly IApplicationDbContext _context;
        private readonly IEdiParser _ediParser;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessInboundEdiCommandHandler"/> class.
        /// </summary>
        /// <param name="context">The decoupled application database context.</param>
        /// <param name="ediParser">The EDI parser service.</param>
        public ProcessInboundEdiCommandHandler(IApplicationDbContext context, IEdiParser ediParser)
        {
            _context = context;
            _ediParser = ediParser;
        }

        /// <summary>
        /// Handles the processing of inbound supplier EDI messages, parsing content, updating DB entities, and performing discrepancy checks.
        /// </summary>
        /// <param name="request">The incoming command enclosing the EDI transaction details.</param>
        /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
        /// <returns>A DTO describing processing success, transaction logs, and reference PO.</returns>
        public async Task<EdiProcessResponseDto> Handle(ProcessInboundEdiCommand request, CancellationToken cancellationToken)
        {
            // Verify supplier exists
            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.Code == request.SupplierCode, cancellationToken);

            if (supplier == null)
            {
                return new EdiProcessResponseDto
                {
                    Success = false,
                    Message = $"Supplier with code '{request.SupplierCode}' not found."
                };
            }

            // Create initial pending EDI transaction log
            var transaction = new EdiTransaction
            {
                MessageType = ParseMessageType(request.MessageType),
                Direction = EdiDirection.Inbound,
                Payload = request.Payload,
                Status = EdiTransactionStatus.Pending,
                ProcessedAt = DateTime.UtcNow
            };

            _context.EdiTransactions.Add(transaction);
            await _context.SaveChangesAsync(cancellationToken);

            try
            {
                if (transaction.MessageType == EdiMessageType.Ordrsp)
                {
                    var parseResult = _ediParser.ParseOrdrsp(request.Payload);
                    transaction.PurchaseOrder = await _context.PurchaseOrders
                        .Include(o => o.Campaign)
                        .Include(o => o.Lines)
                        .FirstOrDefaultAsync(o => o.ErpOrderNumber == parseResult.ErpOrderNumber, cancellationToken);

                    if (transaction.PurchaseOrder == null)
                    {
                        transaction.Status = EdiTransactionStatus.Failed;
                        await _context.SaveChangesAsync(cancellationToken);
                        return new EdiProcessResponseDto
                        {
                            Success = false,
                            Message = $"Purchase order '{parseResult.ErpOrderNumber}' referenced in EDI was not found in system.",
                            TransactionId = transaction.Id
                        };
                    }

                    bool hasMismatch = false;

                    foreach (var parsedLine in parseResult.Lines)
                    {
                        var poLine = transaction.PurchaseOrder.Lines
                            .FirstOrDefault(l => l.ProductCode == parsedLine.ProductCode);

                        if (poLine != null)
                        {
                            poLine.ConfirmedQty = parsedLine.ConfirmedQty;
                            poLine.ConfirmedDate = parsedLine.ConfirmedDate;

                            // Validation checks for constraints
                            bool qtyMismatch = parsedLine.ConfirmedQty < poLine.OrderedQty;
                            bool dateMismatch = parsedLine.ConfirmedDate > transaction.PurchaseOrder.Campaign.DeliveryDeadline;

                            if (qtyMismatch || dateMismatch)
                            {
                                poLine.Status = PurchaseOrderLineStatus.Mismatched;
                                hasMismatch = true;
                            }
                            else
                            {
                                poLine.Status = PurchaseOrderLineStatus.Confirmed;
                            }
                        }
                    }

                    // Update PO status
                    transaction.PurchaseOrder.Status = hasMismatch ? PurchaseOrderStatus.Mismatched : PurchaseOrderStatus.Confirmed;
                    transaction.Status = EdiTransactionStatus.Success;

                    await _context.SaveChangesAsync(cancellationToken);

                    return new EdiProcessResponseDto
                    {
                        Success = true,
                        Message = hasMismatch 
                            ? "EDI parsed successfully. Discrepancies detected and flagged as mismatched."
                            : "EDI parsed successfully. Supplier confirmation matches ordered quantities.",
                        TransactionId = transaction.Id,
                        OrderNumber = parseResult.ErpOrderNumber
                    };
                }
                else if (transaction.MessageType == EdiMessageType.Desadv)
                {
                    var parseResult = _ediParser.ParseDesadv(request.Payload);
                    transaction.PurchaseOrder = await _context.PurchaseOrders
                        .Include(o => o.Campaign)
                        .Include(o => o.Lines)
                        .FirstOrDefaultAsync(o => o.ErpOrderNumber == parseResult.ErpOrderNumber, cancellationToken);

                    if (transaction.PurchaseOrder == null)
                    {
                        transaction.Status = EdiTransactionStatus.Failed;
                        await _context.SaveChangesAsync(cancellationToken);
                        return new EdiProcessResponseDto
                        {
                            Success = false,
                            Message = $"Purchase order '{parseResult.ErpOrderNumber}' referenced in DESADV was not found.",
                            TransactionId = transaction.Id
                        };
                    }

                    foreach (var parsedLine in parseResult.Lines)
                    {
                        var poLine = transaction.PurchaseOrder.Lines
                            .FirstOrDefault(l => l.ProductCode == parsedLine.ProductCode);

                        if (poLine != null)
                        {
                            poLine.Status = PurchaseOrderLineStatus.Shipped;
                        }
                    }

                    transaction.PurchaseOrder.Status = PurchaseOrderStatus.Shipped;
                    transaction.Status = EdiTransactionStatus.Success;

                    await _context.SaveChangesAsync(cancellationToken);

                    return new EdiProcessResponseDto
                    {
                        Success = true,
                        Message = $"DESADV processed successfully. SSCC: {parseResult.SSCC}, carrier: {parseResult.CarrierName}.",
                        TransactionId = transaction.Id,
                        OrderNumber = parseResult.ErpOrderNumber
                    };
                }
                else
                {
                    transaction.Status = EdiTransactionStatus.Failed;
                    await _context.SaveChangesAsync(cancellationToken);
                    return new EdiProcessResponseDto
                    {
                        Success = false,
                        Message = $"Unsupported EDI message type: '{request.MessageType}'",
                        TransactionId = transaction.Id
                    };
                }
            }
            catch (Exception ex)
            {
                transaction.Status = EdiTransactionStatus.Failed;
                await _context.SaveChangesAsync(cancellationToken);
                return new EdiProcessResponseDto
                {
                    Success = false,
                    Message = $"Error processing EDI message: {ex.Message}",
                    TransactionId = transaction.Id
                };
            }
        }

        /// <summary>
        /// Parses the message type string into the <see cref="EdiMessageType"/> enum.
        /// </summary>
        /// <param name="messageType">The string representation of the EDI message type.</param>
        /// <returns>The corresponding <see cref="EdiMessageType"/> value.</returns>
        private static EdiMessageType ParseMessageType(string messageType)
        {
            return messageType.ToUpperInvariant() switch
            {
                "ORDERS" => EdiMessageType.Orders,
                "ORDRSP" => EdiMessageType.Ordrsp,
                "DESADV" => EdiMessageType.Desadv,
                _ => EdiMessageType.Unknown
            };
        }
    }
}
