using Core.Enums.Crm;
using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Crm.PurchaseRequest
{
    public class PurchaseRequestCreateDto
    {
        /// <summary>
        /// Şimdilik opsiyonel.
        /// İleride tenant bazlı çalışma aktif edilirse kullanılabilir.
        /// </summary>
        public long? TenantId { get; set; }

        [Required]
        public long ManagerUserId { get; set; }

        [Required]
        [MaxLength(500)]
        public string Subject { get; set; } = string.Empty;

        [MaxLength(4000)]
        public string? Description { get; set; }

        [Required]
        public PurchaseRequestType RequestType { get; set; }

        public bool IsOfficePurchase { get; set; }

        public long? CustomerId { get; set; }

        public long? SystemTypeId { get; set; }
    }
}