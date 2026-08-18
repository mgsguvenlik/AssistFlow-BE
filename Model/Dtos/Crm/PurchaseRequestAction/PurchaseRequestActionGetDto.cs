namespace Model.Dtos.Crm.PurchaseRequestAction
{
    public class PurchaseRequestActionGetDto
    {
        public long Id { get; set; }

        public long PurchaseRequestStepId { get; set; }

        public string? PurchaseRequestStepCode { get; set; }

        public string? PurchaseRequestStepName { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public long? TargetStepId { get; set; }

        public string? TargetStepCode { get; set; }

        public string? TargetStepName { get; set; }

        public bool RequiresDescription { get; set; }

        public int OrderNo { get; set; }

        public bool IsActive { get; set; }
    }
}