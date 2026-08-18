using Core.Enums.Crm;

namespace Model.Dtos.Crm.PurchaseRequestHistory
{
    public class PurchaseRequestHistoryGetDto
    {
        public long Id { get; set; }

        public long PurchaseRequestId { get; set; }


        // From Step
        public long? FromStepId { get; set; }

        public string? FromStepCode { get; set; }

        public string? FromStepName { get; set; }


        // To Step
        public long ToStepId { get; set; }

        public string? ToStepCode { get; set; }

        public string? ToStepName { get; set; }


        // Action
        public long PurchaseRequestActionId { get; set; }

        public string? ActionCode { get; set; }

        public string? ActionName { get; set; }


        public string? Description { get; set; }


        public PurchaseRequestStatus PreviousStatus { get; set; }

        public PurchaseRequestStatus NewStatus { get; set; }


        // İşlemi yapan
        public long CreatedUser { get; set; }

        public string? CreatedUserName { get; set; }

        public DateTimeOffset CreatedDate { get; set; }
    }
}