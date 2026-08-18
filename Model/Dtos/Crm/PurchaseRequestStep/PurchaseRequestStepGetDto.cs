namespace Model.Dtos.Crm.PurchaseRequestStep
{
    public class PurchaseRequestStepGetDto
    {
        public long Id { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int OrderNo { get; set; }

        public bool IsInitial { get; set; }

        public bool IsFinal { get; set; }

        public bool IsActive { get; set; }
    }
}