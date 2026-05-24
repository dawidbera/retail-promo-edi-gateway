using MediatR;
using Microsoft.AspNetCore.Mvc;
using RetailPromoEdiGateway.Application.Features.Edi.Commands;
using System.Threading.Tasks;

namespace RetailPromoEdiGateway.Web.Controllers.Api
{
    /// <summary>
    /// API Controller for receiving and processing EDIFACT payloads from integrated suppliers.
    /// </summary>
    [ApiController]
    [Route("api/v1/edi")]
    public class EdiApiController : ControllerBase
    {
        private readonly IMediator _mediator;

        /// <summary>
        /// Initializes a new instance of the <see cref="EdiApiController"/> class.
        /// </summary>
        /// <param name="mediator">The CQRS mediator service.</param>
        public EdiApiController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>
        /// Supplier EDI endpoint to post inbound EDI messages (e.g. ORDRSP, DESADV).
        /// </summary>
        [HttpPost]
        [Route("inbound")]
        public async Task<IActionResult> ProcessInboundEdi([FromBody] ProcessInboundEdiRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Payload))
            {
                return BadRequest("Invalid EDI payload.");
            }

            var command = new ProcessInboundEdiCommand
            {
                Payload = request.Payload,
                MessageType = request.MessageType,
                SupplierCode = request.SupplierCode
            };

            var response = await _mediator.Send(command);

            if (!response.Success)
            {
                return UnprocessableEntity(response);
            }

            return Ok(response);
        }
    }

    /// <summary>
    /// Request payload representing an inbound EDI message.
    /// </summary>
    public class ProcessInboundEdiRequest
    {
        /// <summary>
        /// Gets or sets the raw EDIFACT content payload.
        /// </summary>
        public string Payload { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the EDI message type, e.g. "ORDRSP" or "DESADV".
        /// </summary>
        public string MessageType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the unique code identifying the sending supplier.
        /// </summary>
        public string SupplierCode { get; set; } = string.Empty;
    }
}
