using Model.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Concrete
{
    public class RoleForm:SoftDeleteEntity
    {
        public long Id { get; set; }

       

        public bool IsVisible { get; set; }
        public bool Readonly { get; set; }
        public bool IsRequired { get; set; }

        // Navigations
        /// <summary>İlgili form alanı.</summary>
        public long FormFieldId { get; set; }
        public FormField? FormField { get; set; }

        /// <summary>İlgili rol.</summary>
        public long RoleId { get; set; }
        public Role? Role { get; set; }
    }
}
