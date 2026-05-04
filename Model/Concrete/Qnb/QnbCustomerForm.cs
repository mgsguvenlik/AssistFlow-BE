using Core.Enums;
using Core.Enums.Qnb;
using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Qnb
{
    [Table("QnbCustomerForm", Schema = "qnb")]
    public class QnbCustomerForm : AuditableWithUserEntity
    {
        public long Id { get; set; }

        [Required, MaxLength(100)]
        public string RequestNo { get; set; } = string.Empty;

        /// <summary>QNB Servis Takip Numarasý.</summary>
        public string? QnbServiceTrackNo { get; set; }

        public DateTime ServicesDate { get; set; }
        public DateTime? PlannedCompletionDate { get; set; }

        public long CustomerId { get; set; }
        public Customer? Customer { get; set; }

        [ForeignKey(nameof(CustomerApproverId))]
        public ProgressApprover? CustomerApprover { get; set; }
        public long? CustomerApproverId { get; set; }

        public string? Description { get; set; }

        public QnbCustomerFormStatus Status { get; set; }
        public WorkFlowPriority Priority { get; set; } = WorkFlowPriority.Normal;
    }
}