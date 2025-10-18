using Model.Dtos.CustomerProductPrice;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Interfaces
{
    public interface ICustomerProductPriceService
    {
        /// <summary>
        /// Belirtilen ürün ve müşteri için fiyat kayıtlarını getirir.
        /// </summary>
        Task<List<CustomerProductPriceGetDto>> GetByProductAndCustomerAsync(long productId, long customerId);
    }
}
