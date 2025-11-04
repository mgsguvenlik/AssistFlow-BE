using Core.Enums;
using Model.Dtos.WorkFlowDtos.ServicesRequestProduct;
using Model.Dtos.WorkFlowDtos.WorkFlowReviewLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.WorkFlowDtos.Pricing
{
    public class PricingGetDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = string.Empty;
        public PricingStatus Status { get; set; } = PricingStatus.Pending;

        public string Currency { get; set; } = "TRY";
        public string? Notes { get; set; }
        public decimal TotalAmount { get; set; }

        // Audit
        public DateTimeOffset CreatedDate { get; set; }
        public long CreatedUser { get; set; }
        public DateTimeOffset? UpdatedDate { get; set; }
        public long? UpdatedUser { get; set; }
        public List<ServicesRequestProductGetDto> Products { get; set; } = new();
        public List<WorkFlowReviewLogDto> ReviewLogs { get; set; } = new();


    }
}
