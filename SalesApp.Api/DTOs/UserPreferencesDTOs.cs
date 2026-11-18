namespace SalesApp.DTOs
{
    public class UserPreferencesResponse
    {
        public bool TreatUnpaidActiveAsAwaitingPayment { get; set; }
        public bool IncludeInactiveUsersInFilter { get; set; }
        public bool AutoAssignNewContractsToMatriculaOwner { get; set; }
    }

    public class UpdateUserPreferencesRequest
    {
        public bool? TreatUnpaidActiveAsAwaitingPayment { get; set; }
        public bool? IncludeInactiveUsersInFilter { get; set; }
        public bool? AutoAssignNewContractsToMatriculaOwner { get; set; }
    }
}
