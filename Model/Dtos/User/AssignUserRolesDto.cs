using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.User
{
    public class AssignUserRolesDto
    {
        public long UserId { get; set; }
        public List<long> RoleIds { get; set; } = new();
    }
}
