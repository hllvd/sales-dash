using SalesApp.DTOs;

namespace SalesApp.Services
{
    public interface IEndpointDiscoveryService
    {
        List<EndpointInfo> GetAllEndpoints();
    }
}