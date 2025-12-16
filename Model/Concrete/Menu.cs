using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete
{
    public class Menu : BaseEntity
    {
        [Key]
        public long Id { get; set; }
        [MaxLength(200)]
        public string Name { get; set; } = null!;
        [MaxLength(1000)]
        public string? Description { get; set; }

        public ICollection<MenuRole> MenuRoles { get; set; } = new List<MenuRole>();
    }
}
