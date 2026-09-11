namespace SalesApp.DTOs
{
    public class UserPreferencesResponse
    {
        public bool TreatUnpaidActiveAsAwaitingPayment { get; set; }
    }

    public class UpdateUserPreferencesRequest
    {
        public bool TreatUnpaidActiveAsAwaitingPayment { get; set; }
    }
}
