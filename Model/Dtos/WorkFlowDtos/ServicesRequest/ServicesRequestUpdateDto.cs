using Core.Enums;
using Model.Concrete.WorkFlows;
using Model.Dtos.WorkFlowDtos.ServicesRequestProduct;
using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.WorkFlowDtos.ServicesRequest
{
    public class ServicesRequestUpdateDto
    {
        [Required]
        public long Id { get; set; }

        /// <summary>Unique olmak zorunda. Boş gönderirsen servis tarafında üretmeyi tercih edebilirsin.</summary>
        [MaxLength(100)]
        [Required]
        public required string RequestNo { get; set; }

        [MaxLength(100)]
        public string? OracleNo { get; set; }

        [Required]
        public DateTimeOffset ServicesDate { get; set; }

        public DateTimeOffset? PlannedCompletionDate { get; set; }

        [Required]
        public ServicesCostStatus ServicesCostStatus { get; set; }

        public string? Description { get; set; }
        public bool IsProductRequirement { get; set; }

        public bool IsMailSended { get; set; }
        public bool IsLocationValid { get; set; }

        /// <summary>Müşteri tarafı onaylayan (opsiyonel)</summary>
        public long? CustomerApproverId { get; set; }
        public string? CustomerApproverName { get; set; }

        public long? ApproverTechnicianId { get; set; }
        public string? ApproverTechnician { get; set; }

        /// <summary>Zorunlu ilişkiler</summary>
        [Required] public long CustomerId { get; set; }
        [Required] public long ServiceTypeId { get; set; }
        public List<ServicesRequestProductUpdateDto>? Products { get; set; }

        public WorkFlowPriority Priority { get; set; } = WorkFlowPriority.Normal;

        public long StatuId { get; set; } //Akış taleplerin id si. Ekranda seçilecek.
    }

}
