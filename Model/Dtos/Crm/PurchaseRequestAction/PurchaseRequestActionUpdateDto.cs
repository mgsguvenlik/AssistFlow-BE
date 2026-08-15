namespace Model.Dtos.Crm.PurchaseRequestAction
{
    public class PurchaseRequestActionUpdateDto
    {
        public long Id { get; set; }

        public long? PurchaseRequestStepId { get; set; }

        public string? Code { get; set; }

        public string? Name { get; set; }

        public string? Description { get; set; }

        public long? TargetStepId { get; set; }

        public bool? RequiresDescription { get; set; }

        public int? OrderNo { get; set; }

        public bool? IsActive { get; set; }
    }
}