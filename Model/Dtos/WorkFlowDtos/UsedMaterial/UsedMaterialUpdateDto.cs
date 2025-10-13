using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.WorkFlowDtos.UsedMaterial
{
    public class UsedMaterialUpdateDto
    {
        public long Id { get; set; }

        [Required, MaxLength(200)]
        public string MaterialName { get; set; }

        [Required, MaxLength(200)]
        public string Brand { get; set; }

        [Required, MaxLength(200)]
        public string Model { get; set; }

        [Required, MaxLength(200)]
        public required string Unit { get; set; }

        [Required, MaxLength(200)]
        public decimal Amount { get; set; }
        public DateTimeOffset CreatedDate { get; set; }

        [Required]
        public long TechnicalServiceId { get; set; }
    }
}
