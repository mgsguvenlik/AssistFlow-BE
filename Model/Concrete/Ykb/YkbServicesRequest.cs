using Core.Enums;
using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Ykb
{
    [Table("YkbServicesRequest", Schema = "ykb")]
    public class YkbServicesRequest : AuditableWithUserEntity
    {
        public long Id { get; set; }

        [Required, MaxLength(100)]
        public string RequestNo { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? OracleNo { get; set; }

        public DateTimeOffset ServicesDate { get; set; }          // zorunlu
        public DateTimeOffset? PlannedCompletionDate { get; set; } // opsiyonel - Planlanan Tamamlanma Tarihi
        public ServicesCostStatus ServicesCostStatus { get; set; }
        public string? Description { get; set; }
        public bool IsProductRequirement { get; set; }
        public YkbWorkFlowStep? YkbWorkFlowStep { get; set; } // gönderim adımı FK/lookup (opsiyonel)
        public long? WorkFlowStepId { get; set; } // gönderim adımı FK/lookup (opsiyonel)
        public bool IsMailSended { get; set; }

        // (İsterseniz navigation’ları sonra ekleyin)
        [ForeignKey(nameof(CustomerApproverId))]
        public ProgressApprover? CustomerApprover { get; set; }  // müşteri tarafı onaylayan (opsiyonel)
        public long? CustomerApproverId { get; set; }  // müşteri tarafı onaylayan (opsiyonel)

        [ForeignKey(nameof(CustomerId))]
        public Customer? Customer { get; set; } = default!;
        public long? CustomerId { get; set; }

        [ForeignKey(nameof(ServiceTypeId))]
        public ServiceType? ServiceType { get; set; } = default!;
        public long? ServiceTypeId { get; set; }       // ServiceType FK
        public WorkFlowPriority Priority { get; set; } = WorkFlowPriority.Normal;
        public ServicesRequestStatus ServicesRequestStatus { get; set; }

    }
}
