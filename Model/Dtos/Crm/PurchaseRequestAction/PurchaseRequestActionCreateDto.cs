using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Crm.PurchaseRequestAction
{
    public class PurchaseRequestActionCreateDto
    {
        [Required]
        public long PurchaseRequestStepId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public long? TargetStepId { get; set; }

        public bool RequiresDescription { get; set; }

        public int OrderNo { get; set; }

        public bool IsActive { get; set; } = true;
    }
}