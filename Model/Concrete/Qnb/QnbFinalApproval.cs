using Core.Enums;
using Model.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Qnb
{
    [Table("QnbFinalApproval", Schema = "qnb")]
    public class QnbFinalApproval : AuditableWithUserEntity
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = default!;
        public string? Notes { get; set; }

        public long? DecidedBy { get; set; }
        public FinalApprovalStatus Status { get; set; } = FinalApprovalStatus.Pending;
        public decimal DiscountPercent { get; set; }

        public string? CustomerNote { get; set; }
        public long? CustomerApprovedBy { get; set; }
        public DateTime? CustomerApprovedAt { get; set; }
    }
}