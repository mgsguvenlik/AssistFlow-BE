using Core.Enums;
using Model.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Ekb
{
    [Table("EkbFinalApproval", Schema = "ekb")]
    public class EkbFinalApproval : AuditableWithUserEntity
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = default!;
        public string? Notes { get; set; }

        // Kim-ne-zaman
        public long? DecidedBy { get; set; }
        public FinalApprovalStatus Status { get; set; } = FinalApprovalStatus.Pending;
        public decimal DiscountPercent { get; set; }         // 0..100

        public string? CustomerNote { get; set; }          // 6. adımda EKB’nin açıklaması
        public long? CustomerApprovedBy { get; set; }      // EKB kullanıcı id
        public DateTime? CustomerApprovedAt { get; set; }  // EKB onay zamanı

    }
}
