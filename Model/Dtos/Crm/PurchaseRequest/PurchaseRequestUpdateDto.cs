using Core.Enums.Crm;

namespace Model.Dtos.Crm.PurchaseRequest
{
    public class PurchaseRequestUpdateDto
    {
        public long Id { get; set; }

        public long? ManagerUserId { get; set; }

        public string? Subject { get; set; }

        public string? Description { get; set; }

        public PurchaseRequestType? RequestType { get; set; }

        public bool? IsOfficePurchase { get; set; }

        public long? CustomerId { get; set; }

        public long? SystemTypeId { get; set; }
    }
}