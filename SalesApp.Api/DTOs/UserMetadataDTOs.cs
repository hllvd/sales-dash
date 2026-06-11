using System.Collections.Generic;

namespace SalesApp.DTOs
{
    // Field definition requests/responses (admin only)
    public record UserMetadataFieldRequest(
        string Key, 
        string Label, 
        string? GroupLabel, 
        string FieldType,          // "text" or "dropdown"
        string? DropdownOptions,   // JSON serialized string of options
        int DisplayOrder,
        bool IsRequired
    );

    public record UserMetadataFieldResponse(
        int Id, 
        string Key, 
        string Label, 
        string? GroupLabel, 
        string FieldType, 
        string? DropdownOptions, 
        int DisplayOrder,
        bool IsRequired, 
        bool IsActive
    );

    // Value upsert request (per-user)
    public record UserMetadataValueItem(int FieldId, string? Value);
    
    public record UpsertUserMetadataRequest(IList<UserMetadataValueItem> Values);

    // Grouped structure returned within UserResponse for UI consumption
    public record UserMetadataGroupDto(
        string? GroupLabel, 
        List<UserMetadataFieldValueDto> Fields
    );

    public record UserMetadataFieldValueDto(
        int FieldId, 
        string Key, 
        string Label, 
        string FieldType, 
        string? DropdownOptions, 
        bool IsRequired, 
        string? Value
    );
}
