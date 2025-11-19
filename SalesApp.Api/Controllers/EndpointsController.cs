using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.Attributes;
using SalesApp.DTOs;
using SalesApp.Services;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EndpointsController : ControllerBase
    {
        private readonly IEndpointDiscoveryService _endpointService;

        public EndpointsController(IEndpointDiscoveryService endpointService)
        {
            _endpointService = endpointService;
        }

        [HttpGet]
        public ActionResult<ApiResponse<List<EndpointInfo>>> GetAllEndpoints()
        {
            var endpoints = _endpointService.GetAllEndpoints();

            return Ok(new ApiResponse<List<EndpointInfo>>
            {
                Success = true,
                Data = endpoints,
                Message = "Endpoints retrieved successfully"
            });
        }
    }
}