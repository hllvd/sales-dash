namespace SalesApp.Services
{
    public interface IAutoMappingService
    {
        Dictionary<string, string> SuggestMappings(List<string> sourceColumns, string entityType, List<string>? templateFields = null);
        Dictionary<string, string> ApplyTemplateMappings(Dictionary<string, string> templateMappings, List<string> sourceColumns);
    }
}
