using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Crm.PurchaseRequestStep
{
    public class PurchaseRequestStepCreateDto
    {
        [Required]
        [MaxLength(100)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int OrderNo { get; set; }

        public bool IsInitial { get; set; }

        public bool IsFinal { get; set; }

        public bool IsActive { get; set; } = true;
    }
}