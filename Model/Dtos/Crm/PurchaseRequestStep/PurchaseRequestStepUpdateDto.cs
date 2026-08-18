namespace Model.Dtos.Crm.PurchaseRequestStep
{
    public class PurchaseRequestStepUpdateDto
    {
        public long Id { get; set; }

        public string? Code { get; set; }

        public string? Name { get; set; }

        public string? Description { get; set; }

        public int? OrderNo { get; set; }

        public bool? IsInitial { get; set; }

        public bool? IsFinal { get; set; }

        public bool? IsActive { get; set; }
    }
}