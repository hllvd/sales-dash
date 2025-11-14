namespace SalesApp.DTOs
{
    public class EndpointInfo
    {
        public string Controller { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string HttpMethod { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
        public List<string> RequiredRoles { get; set; } = new();
        public string AuthorizationType { get; set; } = string.Empty;
    }
}