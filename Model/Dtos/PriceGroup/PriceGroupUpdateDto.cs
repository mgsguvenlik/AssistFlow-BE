using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.PriceGroup
{
    public class PriceGroupUpdateDto
    {
        [Required]
        public long Id { get; set; }

        [MaxLength(100)]
        public string? Name { get; set; }

        public string? Description { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Price 0'dan küçük olamaz.")]
        public decimal? Price { get; set; }

        [Range(1, long.MaxValue)]
        [Required]
        public long ProductId { get; set; }
    }
}
