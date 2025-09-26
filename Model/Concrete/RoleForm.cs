using System.ComponentModel.DataAnnotations;

namespace Model.Concrete
{
    public class RoleForm : AbandonedMutexException
    {
        [Key]
        public long Id { get; set; }
        public bool IsVisible { get; set; }
        public bool Readonly { get; set; }
        public bool IsRequired { get; set; }

        /// <summary>İlgili form alanı.</summary>
        public long FormFieldId { get; set; }
        public FormField? FormField { get; set; }

        /// <summary>İlgili rol.</summary>
        public long RoleId { get; set; }
        public Role? Role { get; set; }
    }
}
