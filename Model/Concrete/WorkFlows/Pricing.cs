using Core.Enums;
using Microsoft.EntityFrameworkCore;
using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete.WorkFlows
{

    [Index(nameof(RequestNo), IsUnique = true)]
    public class Pricing : AuditableWithUserEntity
    {
        public long Id { get; set; }

        [Required, MaxLength(100)]
        public string RequestNo { get; set; } = string.Empty;

        public PricingStatus Status { get; set; } = PricingStatus.Pending;

        [MaxLength(3)]
        public string Currency { get; set; } = "TRY";

        [MaxLength(1000)]
        public string? Notes { get; set; }

        [Precision(18, 2)]
        public decimal TotalAmount { get; set; } // Items sum (computed server-side)

    }
}
