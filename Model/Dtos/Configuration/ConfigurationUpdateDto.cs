using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.Configuration
{
    public class ConfigurationUpdateDto
    {
        public long Id { get; set; }
        [Required(ErrorMessage = "Tanım Alnı zorunlu")]
        public string Name { get; set; } = null!;
        [Required(ErrorMessage = "Değer Alnı zorunlu")]
        public string Value { get; set; } = null!;
        public string? Description { get; set; }
    }
}
