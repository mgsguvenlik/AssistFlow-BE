using Core.Enums.Crm;

namespace Model.Dtos.Crm.PurchaseRequestHistory
{
    public class PurchaseRequestHistoryCreateDto
    {
        public long PurchaseRequestId { get; set; }

        public long? FromStepId { get; set; }

        public long ToStepId { get; set; }

        public long PurchaseRequestActionId { get; set; }

        public string? Description { get; set; }

        public PurchaseRequestStatus PreviousStatus { get; set; }

        public PurchaseRequestStatus NewStatus { get; set; }
    }
}