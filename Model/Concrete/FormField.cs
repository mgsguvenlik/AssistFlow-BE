using Model.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Concrete
{
    public class FormField : AuditableWithUserEntity
    {
        public long Id { get; set; }

        /// <summary>Form alanı adı (örn: Talep No, Oracle No).</summary>
        public string Name { get; set; } = null!;

        /// <summary>Alan tipi (örn: input, integer, textarea, radio).</summary>
        public string Type { get; set; } = null!;

        // Navigations
        public ICollection<RoleForm> RoleForms { get; set; } = new List<RoleForm>();
    }

}
