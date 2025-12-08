namespace SalesApp.Models
{
    /// <summary>
    /// Contract status enumeration
    /// </summary>
    public enum ContractStatus
    {
        /// <summary>
        /// Customer is paying regularly
        /// </summary>
        Active,
        
        /// <summary>
        /// 1 month late
        /// </summary>
        Late1,
        
        /// <summary>
        /// 2 months late
        /// </summary>
        Late2,
        
        /// <summary>
        /// 3 months late
        /// </summary>
        Late3,
        
        /// <summary>
        /// Inactive/defaulted
        /// </summary>
        Defaulted
    }
}
