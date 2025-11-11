// Model/Concrete/MenuRole.cs
using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete
{
    [Table("MenuRole")]
    public class MenuRole : BaseEntity
    {
        [Key]
        public long Id { get; set; }

        // Şemadaki "ModulId" kolon adıyla eşle
        [Column("ModulId")]
        public long MenuId { get; set; }
        public long RoleId { get; set; }

        public bool HasView { get; set; }
        public bool HasEdit { get; set; }

        // Navigations
        public Menu? Menu { get; set; }
        public Role? Role { get; set; }
    }
}
