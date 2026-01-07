namespace SalesApp.Models
{
    /// <summary>
    /// Contract type enumeration
    /// </summary>
    public enum ContractType
    {
        /// <summary>
        /// Lar - Default type
        /// </summary>
        Lar = 0,
        
        /// <summary>
        /// Motores type
        /// </summary>
        Motores = 1
    }
    
    /// <summary>
    /// Helper class for ContractType string conversion
    /// </summary>
    public static class ContractTypeExtensions
    {
        public static string ToApiString(this ContractType type)
        {
            return type switch
            {
                ContractType.Lar => "lar",
                ContractType.Motores => "motores",
                _ => throw new ArgumentException($"Unknown contract type: {type}")
            };
        }
        
        public static ContractType? FromApiString(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            
            return value.ToLower() switch
            {
                "lar" => ContractType.Lar,
                "motores" => ContractType.Motores,
                _ => throw new ArgumentException($"Invalid contract type: {value}. Must be 'lar' or 'motores'")
            };
        }
        
        public static string? ToApiString(int? contractTypeValue)
        {
            if (!contractTypeValue.HasValue) return null;
            return ((ContractType)contractTypeValue.Value).ToApiString();
        }
        
        public static int? FromApiStringToInt(string? value)
        {
            var enumValue = FromApiString(value);
            return enumValue.HasValue ? (int)enumValue.Value : null;
        }
    }
}
