namespace Model.Dtos.Crm.PurchaseRequestTask
{
    public class PurchaseRequestTaskCreateDto
    {
        public long PurchaseRequestId { get; set; }

        public long PurchaseRequestStepId { get; set; }

        public long? AssignedUserId { get; set; }

        public long? AssignedRoleId { get; set; }
    }
}