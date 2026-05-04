using Core.Enums;
using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Qnb
{
    [Table("QnbServicesRequest", Schema = "qnb")]
    public class QnbServicesRequest : AuditableWithUserEntity
    {
        public long Id { get; set; }

        [Required, MaxLength(100)]
        public string RequestNo { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? QnbServiceTrackNo { get; set; }

        public DateTimeOffset ServicesDate { get; set; }
        public DateTimeOffset? PlannedCompletionDate { get; set; }
        public ServicesCostStatus ServicesCostStatus { get; set; }
        public string? Description { get; set; }
        public bool IsProductRequirement { get; set; }

        public QnbWorkFlowStep? QnbWorkFlowStep { get; set; }
        public long? WorkFlowStepId { get; set; }
        public bool IsMailSended { get; set; }

        [ForeignKey(nameof(CustomerApproverId))]
        public ProgressApprover? CustomerApprover { get; set; }
        public long? CustomerApproverId { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public Customer? Customer { get; set; } = default!;
        public long? CustomerId { get; set; }

        [ForeignKey(nameof(ServiceTypeId))]
        public ServiceType? ServiceType { get; set; } = default!;
        public long? ServiceTypeId { get; set; }

        public WorkFlowPriority Priority { get; set; } = WorkFlowPriority.Normal;
        public ServicesRequestStatus ServicesRequestStatus { get; set; }
    }
}