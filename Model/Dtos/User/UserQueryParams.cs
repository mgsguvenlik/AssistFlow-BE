using Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.User
{
    public class UserQueryParams : QueryParams
    {
        public string? City { get; set; }
        public string? District { get; set; }
        public long? RoleId { get; set; }
        public bool? IsActive { get; set; }
    }
}
