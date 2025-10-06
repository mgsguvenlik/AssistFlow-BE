using Core.Utilities.Constants;
using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Configuration
{
    public class ConfigurationUpdateDto
    {
        public long Id { get; set; }
        [Required(ErrorMessage = Messages.DefinitionRequired)]
        public string Name { get; set; } = null!;
        [Required(ErrorMessage = Messages.ValueFieldRequired)]
        public string Value { get; set; } = null!;
        public string? Description { get; set; }
    }
}
