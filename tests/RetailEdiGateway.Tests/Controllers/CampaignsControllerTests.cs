using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RetailEdiGateway.Application.Features.Campaigns.Queries;
using RetailEdiGateway.Web.Controllers;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RetailEdiGateway.Tests.Controllers
{
    /// <summary>
    /// Unit tests for the <see cref="CampaignsController"/>.
    /// Verifies the interaction with MediatR and view model delivery.
    /// </summary>
    public class CampaignsControllerTests
    {
        private readonly Mock<IMediator> _mockMediator;
        private readonly CampaignsController _controller;

        /// <summary>
        /// Initializes a new instance of the <see cref="CampaignsControllerTests"/> class.
        /// </summary>
        public CampaignsControllerTests()
        {
            _mockMediator = new Mock<IMediator>();
            _controller = new CampaignsController(_mockMediator.Object);
        }

        /// <summary>
        /// Verifies that <see cref="CampaignsController.Index"/> returns a ViewResult with the campaign dashboard list.
        /// </summary>
        [Fact]
        public async Task Index_ReturnsViewWithModel()
        {
            // Arrange
            var dashboardData = new List<CampaignDashboardDto>
            {
                new() { Id = Guid.NewGuid(), Name = "C1" }
            };
            _mockMediator.Setup(m => m.Send(It.IsAny<GetCampaignDashboardQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dashboardData);

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<List<CampaignDashboardDto>>(viewResult.ViewData.Model);
            Assert.Single(model);
        }

        /// <summary>
        /// Verifies that <see cref="CampaignsController.Index"/> handles mediator exceptions gracefully by returning an empty model.
        /// </summary>
        [Fact]
        public async Task Index_OnException_ReturnsEmptyModelWithErrorMessage()
        {
            // Arrange
            _mockMediator.Setup(m => m.Send(It.IsAny<GetCampaignDashboardQuery>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("MediatR Error"));

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<List<CampaignDashboardDto>>(viewResult.ViewData.Model);
            Assert.Empty(model);
            Assert.Equal("Error loading campaigns: MediatR Error", _controller.ViewBag.ErrorMessage);
        }
    }
}
