using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.MenuRole
{
    public class MenuRoleUpdateDto
    {
        public long Id { get; set; }
        public long? ModuleId { get; set; }
        public long? RoleId { get; set; }
        public bool? HasView { get; set; }
        public bool? HasEdit { get; set; }
    }
}
