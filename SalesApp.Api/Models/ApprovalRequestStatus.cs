namespace SalesApp.Models
{
    /// <summary>
    /// Constants for ApprovalRequest Statuses
    /// </summary>
    public static class ApprovalRequestStatus
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Later = "Later";
    }

    /// <summary>
    /// Constants and helper methods for ApprovalRequest Actions
    /// </summary>
    public static class ApprovalRequestAction
    {
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Later = "Later";

        public static bool IsApprove(string? action)
        {
            if (string.IsNullOrWhiteSpace(action)) return false;
            var normalized = action.Trim().ToUpperInvariant();
            return normalized == "APPROVED" || normalized == "YES" || normalized == "APROVAR";
        }

        public static bool IsReject(string? action)
        {
            if (string.IsNullOrWhiteSpace(action)) return false;
            var normalized = action.Trim().ToUpperInvariant();
            return normalized == "REJECTED" || normalized == "NO" || normalized == "REJEITAR";
        }

        public static bool IsLater(string? action)
        {
            if (string.IsNullOrWhiteSpace(action)) return false;
            var normalized = action.Trim().ToUpperInvariant();
            return normalized == "LATER" || normalized == "DEPOIS";
        }
    }

    /// <summary>
    /// Constants for ApprovalRequest Types
    /// </summary>
    public static class ApprovalRequestType
    {
        public const string ChangeParentEmail = "ChangeParentEmail";
        public const string RequestMatricula = "RequestMatricula";
        public const string AdminRequestMatricula = "AdminRequestMatricula";
        public const string RequestAdminRole = "RequestAdminRole";
    }
}
