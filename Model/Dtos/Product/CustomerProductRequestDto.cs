using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Product
{
    public class CustomerProductRequestDto
    {
        [Required]
        public long CustomerId { get; set; }

        [Required]
        public List<long> ProductIds { get; set; } = new();
    }
}
