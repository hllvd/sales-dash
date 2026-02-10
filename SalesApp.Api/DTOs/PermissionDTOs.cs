using System.Collections.Generic;

namespace SalesApp.DTOs
{
    public class PermissionMatrixResponse
    {
        public List<RoleMatrixDto> Roles { get; set; } = new();
        public List<EndpointMatrixDto> Endpoints { get; set; } = new();
        public List<PermissionAssignmentDto> Permissions { get; set; } = new();
    }

    public class RoleMatrixDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class EndpointMatrixDto
    {
        public string Controller { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string HttpMethod { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
    }

    public class PermissionAssignmentDto
    {
        public int RoleId { get; set; }
        public string ControllerName { get; set; } = string.Empty;
        public string ActionName { get; set; } = string.Empty;
    }

    public class PermissionAssignRequest
    {
        public int RoleId { get; set; }
        public string ControllerName { get; set; } = string.Empty;
        public string ActionName { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
    }
}
