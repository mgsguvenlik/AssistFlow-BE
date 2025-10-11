using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.CustomerGroupProductPrice
{
    public class CustomerGroupProductPriceCreateDto
    {
        [Required, Range(1, long.MaxValue, ErrorMessage = "Geçerli bir grup seçiniz.")]
        public long CustomerGroupId { get; set; }

        [Required, Range(1, long.MaxValue, ErrorMessage = "Geçerli bir ürün seçiniz.")]
        public long ProductId { get; set; }

        [Required, Range(typeof(decimal), "0.0", "79228162514264337593543950335",
            ErrorMessage = "Fiyat 0 veya pozitif olmalıdır.")]
        public decimal Price { get; set; }

        [MaxLength(10)]
        public string? CurrencyCode { get; set; }

        [StringLength(150)]
        public string? Name { get; set; }
    }
}
