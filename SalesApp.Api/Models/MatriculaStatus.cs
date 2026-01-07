namespace SalesApp.Models
{
    /// <summary>
    /// Matricula status enumeration
    /// </summary>
    public enum MatriculaStatus
    {
        /// <summary>
        /// Matricula is active and valid
        /// </summary>
        Active,
        
        /// <summary>
        /// Matricula request is pending approval
        /// </summary>
        Pending,
        
        /// <summary>
        /// Matricula has been deactivated
        /// </summary>
        Inactive,
        
        /// <summary>
        /// Matricula request was approved
        /// </summary>
        Approved,
        
        /// <summary>
        /// Matricula request was rejected
        /// </summary>
        Rejected
    }

    /// <summary>
    /// Extension methods for MatriculaStatus enum
    /// </summary>
    public static class MatriculaStatusExtensions
    {
        /// <summary>
        /// Converts MatriculaStatus enum to API string representation (lowercase)
        /// </summary>
        public static string ToApiString(this MatriculaStatus status)
        {
            return status.ToString().ToLowerInvariant();
        }

        /// <summary>
        /// Converts API string to MatriculaStatus enum (case-insensitive)
        /// </summary>
        public static MatriculaStatus FromApiString(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Matricula status cannot be null or empty");

            return value.Trim().ToLowerInvariant() switch
            {
                "active" => MatriculaStatus.Active,
                "pending" => MatriculaStatus.Pending,
                "inactive" => MatriculaStatus.Inactive,
                "approved" => MatriculaStatus.Approved,
                "rejected" => MatriculaStatus.Rejected,
                _ => throw new ArgumentException($"Invalid matricula status: {value}. Valid values are: active, pending, inactive, approved, rejected")
            };
        }

        /// <summary>
        /// Converts database string value to MatriculaStatus enum
        /// </summary>
        public static MatriculaStatus? FromDatabaseString(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            try
            {
                return FromApiString(value);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Validates if a string is a valid matricula status
        /// </summary>
        public static bool IsValid(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            try
            {
                FromApiString(value);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
