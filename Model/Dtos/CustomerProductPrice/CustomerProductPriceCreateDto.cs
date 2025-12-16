using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.CustomerProductPrice
{
    public class CustomerProductPriceCreateDto
    {
        [Required, Range(1, long.MaxValue, ErrorMessage = "Geçerli bir müşteri seçiniz.")]
        public long CustomerId { get; set; }

        [Required, Range(1, long.MaxValue, ErrorMessage = "Geçerli bir ürün seçiniz.")]
        public long ProductId { get; set; }

        [Required]
        public decimal Price { get; set; }

        [MaxLength(10)]
        public string? CurrencyCode { get; set; }

        [StringLength(150)]
        public string? Name { get; set; }
    }
}
