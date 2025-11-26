using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Enums
{
    public enum CapturedPriceSource
    {
        Standard = 0,   // Product.Price
        Customer = 1,   // CustomerProductPrice
        Group = 2       // CustomerGroupProductPrice
    }
}
