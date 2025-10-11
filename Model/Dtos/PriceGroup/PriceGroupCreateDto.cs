using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.PriceGroup
{
    public class PriceGroupCreateDto
    {
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Price 0'dan küçük olamaz.")]
        public decimal Price { get; set; }
        [Range(1, long.MaxValue)]
        [Required]
        public long ProductId { get; set; }
    }

}
