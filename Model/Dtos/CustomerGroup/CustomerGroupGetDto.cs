using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.CustomerGroup
{
    public class CustomerGroupGetDto
    {
        public long Id { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}
