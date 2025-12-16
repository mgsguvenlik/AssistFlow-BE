using Core.Enums;
using Model.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Ykb
{
    [Table("YkbFinalApproval", Schema = "ykb")]
    public class YkbFinalApproval : AuditableWithUserEntity
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = default!;
        public string? Notes { get; set; }

        // Kim-ne-zaman
        public long? DecidedBy { get; set; }
        public FinalApprovalStatus Status { get; set; } = FinalApprovalStatus.Pending;
        public decimal DiscountPercent { get; set; }         // 0..100

        public string? CustomerNote { get; set; }          // 6. adımda YKB’nin açıklaması
        public long? CustomerApprovedBy { get; set; }      // YKB kullanıcı id
        public DateTime? CustomerApprovedAt { get; set; }  // YKB onay zamanı

    }
}
