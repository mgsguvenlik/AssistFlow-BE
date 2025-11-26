using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.CustomerProductPrice
{
    public class CustomerProductPriceUpdateDto
    {
        [Required]
        public long Id { get; set; }

        [Range(1, long.MaxValue, ErrorMessage = "Geçerli bir müşteri seçiniz.")]
        public long? CustomerId { get; set; }

        [Range(1, long.MaxValue, ErrorMessage = "Geçerli bir ürün seçiniz.")]
        public long? ProductId { get; set; }

        [Required]
        public decimal? Price { get; set; }

        [MaxLength(10)]
        public string? CurrencyCode { get; set; }

        [StringLength(150)]
        public string? Name { get; set; }
    }
}
