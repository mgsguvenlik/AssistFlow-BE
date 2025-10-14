using Core.Enums;
using Model.Concrete.WorkFlows;
using Model.Dtos.WorkFlowDtos.ServicesRequestProduct;
using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.WorkFlowDtos.ServicesRequest
{
    public class ServicesRequestCreateDto
    {
        /// <summary>Unique olmak zorunda. Boş gönderirsen servis tarafında üretmeyi tercih edebilirsin.</summary>
        [MaxLength(100)]
        public string? RequestNo { get; set; }

        [MaxLength(100)]
        public string? OracleNo { get; set; }

        [Required]
        public DateTimeOffset ServicesDate { get; set; }

        public DateTimeOffset? PlannedCompletionDate { get; set; }

        [Required]
        public ServicesCostStatus ServicesCostStatus { get; set; }

        public string? Description { get; set; }
        public bool IsProductRequirement { get; set; }

        public bool IsSended { get; set; }

        /// <summary>Gönderim statüsü (opsiyonel). Entity’de WorkFlowStatus navigation + SendedStatusId FK var.</summary>
        public long? SendedStatusId { get; set; }

        public bool IsReview { get; set; }
        public bool IsMailSended { get; set; }

        /// <summary>Müşteri tarafı onaylayan (opsiyonel)</summary>
        public long? CustomerApproverId { get; set; }

        /// <summary>Zorunlu ilişkiler</summary>
        [Required] public long CustomerId { get; set; }
        [Required] public long ServiceTypeId { get; set; }
        public List<ServicesRequestProductCreateDto>? Products { get; set; }

        public WorkFlowPriority Priority { get; set; } = WorkFlowPriority.Normal;

        public  long StatuId { get; set; } //Akış taleplerin id si. Ekranda seçilecek.
    }
}
