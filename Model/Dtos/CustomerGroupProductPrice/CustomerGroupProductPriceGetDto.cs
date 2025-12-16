using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.CustomerGroupProductPrice
{
    public class CustomerGroupProductPriceGetDto
    {
        public long Id { get; set; }
        public long CustomerGroupId { get; set; }
        public long ProductId { get; set; }
        public decimal Price { get; set; }
        public string? CurrencyCode { get; set; }
        public string? Name { get; set; }
        public string? CustomerGroupName { get; set; }   // map: CustomerGroup.GroupName
        public string? ProductCode { get; set; }         // map: Product.ProductCode
        public string? ProductDescription { get; set; }  // map: Product.Description
    }
}
