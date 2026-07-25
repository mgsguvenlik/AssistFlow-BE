using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbAccounting
{
    public class YkbAccountingServiceReportDto
    {
        public string RequestNo { get; set; } = string.Empty;

        public long? CustomerId { get; set; }

        public string? CustomerName { get; set; }

        public decimal? TotalAmount { get; set; }

        public string? Currency { get; set; }

        public DateTime? CustomerApprovedAt { get; set; }

        /// <summary>
        /// Muhasebe işlemi yapıldı mı?
        /// </summary>
        public bool IsProcessed { get; set; }

        public DateTime? ProcessedAt { get; set; }

        public long? ProcessedBy { get; set; }

        public string? ProcessedByName { get; set; }

        public List<YkbAccountingProductDto> Products { get; set; } = new();
    }

    public class YkbAccountingProductDto
    {
        public long ProductId { get; set; }

        public string? ProductCode { get; set; }

        public string? ProductName { get; set; }

        public decimal Quantity { get; set; }

        /// <summary>
        /// Ürünün birim fiyatı
        /// </summary>
        public decimal UnitPrice { get; set; }

        /// <summary>
        /// Quantity * UnitPrice
        /// </summary>
        public decimal TotalPrice { get; set; }

        public string? Currency { get; set; }
    }

    public class YkbAccountingStatusDto
    {
        public string RequestNo { get; set; } = string.Empty;

        public bool IsProcessed { get; set; }

        public DateTime? ProcessedAt { get; set; }

        public long? ProcessedBy { get; set; }

        public string? ProcessedByName { get; set; }
    }

    public class YkbAccountingReportQueryParams
    {
        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 20;

        /// <summary>
        /// Talep No veya müşteri adı.
        /// </summary>
        public string? Search { get; set; }

        /// <summary>
        /// null = hepsi
        /// true = yapılanlar
        /// false = yapılmayanlar
        /// </summary>
        public bool? IsProcessed { get; set; }

        public DateTime? CustomerApprovedFrom { get; set; }

        public DateTime? CustomerApprovedTo { get; set; }

        public void Normalize(int maxPageSize = 200)
        {
            if (Page <= 0)
                Page = 1;

            if (PageSize <= 0)
                PageSize = 20;

            if (PageSize > maxPageSize)
                PageSize = maxPageSize;

            Search = Search?.Trim();
        }
    }
}
