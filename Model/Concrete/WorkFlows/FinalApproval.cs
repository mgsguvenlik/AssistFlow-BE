using Core.Enums;
using Microsoft.EntityFrameworkCore;
using Model.Abstractions;

namespace Model.Concrete.WorkFlows
{

    [Index(nameof(RequestNo), IsUnique = true)]
    public class FinalApproval : AuditableWithUserEntity
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = default!;
        public string? Notes { get; set; }

        // Kim-ne-zaman
        public long? DecidedBy { get; set; }
        public FinalApprovalStatus Status { get; set; } = FinalApprovalStatus.Pending;


        // 💡 Yeni alanlar
        public decimal SubTotal { get; set; }               // indirim öncesi
        public decimal DiscountPercent { get; set; }         // 0..100
        public decimal DiscountAmount { get; set; }          // TL/para birimi
        public decimal GrandTotal { get; set; }              // indirim sonrası
    }
}
