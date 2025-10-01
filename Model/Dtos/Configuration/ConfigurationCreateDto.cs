using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Configuration
{
    public class ConfigurationCreateDto
    {
        [Required(ErrorMessage = "Tanım Alnı zorunlu")]
        public string Name { get; set; } = null!;
        [Required(ErrorMessage = "Değer Alnı zorunlu")]
        public string Value { get; set; } = null!;
        public string? Description { get; set; }
    }
}
