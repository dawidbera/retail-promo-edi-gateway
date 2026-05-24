using MediatR;
using Microsoft.AspNetCore.Mvc;
using RetailPromoEdiGateway.Application.Features.Campaigns.Queries;
using System;
using System.Threading.Tasks;

namespace RetailPromoEdiGateway.Web.Controllers
{
    /// <summary>
    /// MVC Controller to present the Campaigns dashboard and detailed orders view.
    /// </summary>
    public class CampaignsController : Controller
    {
        private readonly IMediator _mediator;

        /// <summary>
        /// Initializes a new instance of the <see cref="CampaignsController"/> class.
        /// </summary>
        /// <param name="mediator">The CQRS mediator instance.</param>
        public CampaignsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>
        /// Renders the main dashboard page listing all campaigns and their progress.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            try
            {
                var model = await _mediator.Send(new GetCampaignDashboardQuery());
                return View(model);
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Error loading campaigns: {ex.Message}";
                return View(new System.Collections.Generic.List<CampaignDashboardDto>());
            }
        }
    }
}
