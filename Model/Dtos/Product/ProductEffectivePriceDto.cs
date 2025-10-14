using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.Product
{
    public class ProductEffectivePriceDto
    {
        public long ProductId { get; set; }
        public string? ProductCode { get; set; }
        public string? Description { get; set; }
        public decimal? BasePrice { get; set; }
        public string? BaseCurrency { get; set; }
        public decimal EffectivePrice { get; set; }
        public string? EffectiveCurrency { get; set; }
    }
}
