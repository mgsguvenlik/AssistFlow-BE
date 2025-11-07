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
    }
}
